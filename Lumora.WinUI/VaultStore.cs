using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Konscious.Security.Cryptography;

namespace Lumora.WinUI;

// Coffre local C# pur — zéro dépendance Rust, zéro IPC, souverain.
//
// Philosophie : le fichier "vault.lumora" appartient à l'utilisateur. Il est
// autonome et portable — il ne dépend que du mot de passe maître (celui du
// profil). On peut le copier sur une clé USB, un NAS, le restaurer après un
// formatage : il s'ouvrira partout du moment qu'on connaît le mot de passe.
//
// Mode "aes256" (par défaut) : clé dérivée par Argon2id à partir du mot de passe,
//   chiffrement AES-256-CBC. Portable, indépendant de la machine.
// Mode "dpapi" (confort/legacy) : chiffré par Windows, lié au compte Windows.
//   NON portable — proposé seulement en secours.
//
// Contrepartie assumée : pas de récupération. Mot de passe perdu = données perdues.
internal sealed class VaultStore
{
    // Paramètres Argon2id (coût mémoire fort = résistance GPU/ASIC).
    private const int Argon2MemoryKib   = 65536; // 64 Mio
    private const int Argon2Iterations  = 3;
    private const int Argon2Parallelism = 4;

    // Ancien KDF (lecture des coffres créés avant Argon2id).
    private const int LegacyPbkdf2Iterations = 100_000;

    private const int KeySize   = 32;
    private const int IvSize    = 16; // AES-CBC (lecture des anciens coffres)
    private const int GcmNonce  = 12;
    private const int GcmTag     = 16;

    private static readonly byte[] VaultEntropy = Encoding.UTF8.GetBytes("Lumora.Vault.v1");
    private static readonly byte[] LegacyNovaVaultEntropy = Encoding.UTF8.GetBytes("NovaBrowser.Vault.v1");
    private static readonly byte[] LegacyPulseVaultEntropy = Encoding.UTF8.GetBytes("PulseBrowser.Vault.v1");
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    // AAD (donnee authentifiee additionnelle) par blob : lie chaque ciphertext au
    // role precis pour lequel il a ete chiffre, pour qu'un blob GCM valide ne
    // puisse pas etre rejoue silencieusement a la place d'un autre (ex. un blob
    // "cartes" substitue au blob "identifiants"). Les coffres ecrits avant ce
    // correctif n'avaient aucune AAD (equivalent a une AAD vide) : DecryptGcm et
    // TryUnwrapKey retentent avec une AAD vide en repli pour rester lisibles,
    // Save() reecrit toujours avec l'AAD des ce prochain enregistrement.
    private static readonly byte[] AadVaultData          = Encoding.UTF8.GetBytes("Lumora.Vault.Data.v1");
    private static readonly byte[] AadVaultCards          = Encoding.UTF8.GetBytes("Lumora.Vault.Cards.v1");
    private static readonly byte[] AadRecoveryData        = Encoding.UTF8.GetBytes("Lumora.Vault.RecoveryData.v1");
    private static readonly byte[] AadRecoveryCards       = Encoding.UTF8.GetBytes("Lumora.Vault.RecoveryCards.v1");
    private static readonly byte[] AadPinWrap             = Encoding.UTF8.GetBytes("Lumora.Vault.PinWrap.v1");
    private static readonly byte[] AadRecoveryWrap        = Encoding.UTF8.GetBytes("Lumora.Vault.RecoveryWrap.v1");
    private static readonly byte[] AadRecoveryVaultWrap   = Encoding.UTF8.GetBytes("Lumora.Vault.RecoveryVaultWrap.v1");

    private readonly string _vaultFile;
    private VaultHeader _header = new();
    private List<VaultCredential> _credentials = new();
    // Portefeuille : cartes de paiement, même clé et même chiffrement que les
    // identifiants, mais blob séparé dans l'en-tête (rétro-compatible : un
    // ancien coffre n'a simplement pas le champ "cards").
    private List<VaultPaymentCard> _cards = new();
    private byte[]? _unlockedKey;
    private byte[]? _recoveryDataKey;

    public VaultStore(string vaultFile)
    {
        _vaultFile = vaultFile;
        Load();
    }

    public bool HasMasterPassword => _header.Mode == "aes256";

    // Verrouillé = mode AES activé ET clé non encore fournie (Unlock() pas appelé)
    public bool IsLocked => HasMasterPassword && _unlockedKey is null && _recoveryDataKey is null;

    public List<VaultCredential> ListCredentials()
    {
        if (IsLocked) return new();
        return _credentials.ToList();
    }

    // ── Portefeuille (cartes de paiement) ────────────────────────────────────

    public List<VaultPaymentCard> ListCards()
    {
        if (IsLocked) return new();
        return _cards.ToList();
    }

    // Ajout ou mise à jour par Id (un Id vide = nouvelle carte).
    public void UpsertCard(VaultPaymentCard card)
    {
        if (IsLocked) return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var id = string.IsNullOrWhiteSpace(card.Id) ? Guid.NewGuid().ToString("N") : card.Id;
        var idx = _cards.FindIndex(c => c.Id == id);
        var stored = new VaultPaymentCard
        {
            Id = id, Label = card.Label, Holder = card.Holder,
            Number = card.Number, ExpMonth = card.ExpMonth, ExpYear = card.ExpYear,
            Note = card.Note,
            CreatedAt = idx >= 0 ? _cards[idx].CreatedAt : now,
            UpdatedAt = now
        };
        if (idx >= 0) _cards[idx] = stored;
        else _cards.Add(stored);
        Save();
    }

    public void DeleteCard(string id)
    {
        if (IsLocked) return;
        if (_cards.RemoveAll(c => c.Id == id) > 0) Save();
    }

    private static string TombstoneKey(string origin, string username) =>
        (origin ?? string.Empty).ToLowerInvariant() + "|" + (username ?? string.Empty).ToLowerInvariant();

    private static string TombstoneKeyForId(string id) => "id:" + id;

    // Assigne un Id stable à toute entrée qui n'en a pas encore (coffres migrés
    // depuis une version sans multi-compte). Retourne true si des Id ont été générés.
    private bool EnsureCredentialIds()
    {
        var changed = false;
        for (var i = 0; i < _credentials.Count; i++)
        {
            var c = _credentials[i];
            if (!string.IsNullOrEmpty(c.Id)) continue;
            _credentials[i] = new VaultCredential
            {
                Id = Guid.NewGuid().ToString("N"),
                Origin = c.Origin, Username = c.Username, Password = c.Password,
                Label = c.Label, LoginUrl = c.LoginUrl,
                CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt,
                TotpSecret = c.TotpSecret, TotpDigits = c.TotpDigits, TotpPeriod = c.TotpPeriod
            };
            changed = true;
        }
        return changed;
    }

    public void Upsert(string origin, string username, string password, string loginUrl = "")
    {
        if (IsLocked) return;
        // Ajout/màj explicite : lever une éventuelle suppression persistante.
        _header.Deleted.Remove(TombstoneKey(origin, username));
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var idx = _credentials.FindIndex(c =>
            c.Origin.Equals(origin, StringComparison.OrdinalIgnoreCase) &&
            c.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
        {
            var old = _credentials[idx];
            _credentials[idx] = new VaultCredential
            {
                Id = string.IsNullOrEmpty(old.Id) ? Guid.NewGuid().ToString("N") : old.Id,
                Origin = origin, Username = username, Password = password,
                Label = old.Label,
                LoginUrl = string.IsNullOrWhiteSpace(loginUrl) ? old.LoginUrl : loginUrl,
                CreatedAt = old.CreatedAt, UpdatedAt = now,
                TotpSecret = old.TotpSecret, TotpDigits = old.TotpDigits, TotpPeriod = old.TotpPeriod
            };
        }
        else
        {
            _credentials.Add(new VaultCredential
            {
                Id = Guid.NewGuid().ToString("N"),
                Origin = origin, Username = username, Password = password,
                LoginUrl = loginUrl ?? string.Empty,
                CreatedAt = now, UpdatedAt = now
            });
        }
        Save();
    }

    // Définit (ou efface) le nom personnalisé d'un identifiant.
    public void SetLabel(string origin, string username, string label)
    {
        if (IsLocked) return;
        var idx = _credentials.FindIndex(c =>
            c.Origin.Equals(origin, StringComparison.OrdinalIgnoreCase) &&
            c.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;
        SetLabelAt(idx, label);
    }

    // Variante par Id, utilisée par le panneau coffre pour rester fiable quand
    // plusieurs comptes partagent le même (origin, username) apparent.
    public void SetLabelById(string id, string label)
    {
        if (IsLocked) return;
        var idx = _credentials.FindIndex(c => c.Id == id);
        if (idx < 0) return;
        SetLabelAt(idx, label);
    }

    private void SetLabelAt(int idx, string label)
    {
        var old = _credentials[idx];
        _credentials[idx] = new VaultCredential
        {
            Id = old.Id, Origin = old.Origin, Username = old.Username, Password = old.Password,
            Label = label?.Trim() ?? string.Empty, LoginUrl = old.LoginUrl,
            CreatedAt = old.CreatedAt, UpdatedAt = old.UpdatedAt,
            TotpSecret = old.TotpSecret, TotpDigits = old.TotpDigits, TotpPeriod = old.TotpPeriod
        };
        Save();
    }

    // Définit (secret non vide) ou efface (secret vide/null) le TOTP associé à
    // un identifiant. Digits/Period par défaut (6/30) si non précisés.
    public void SetTotpById(string id, string? secret, int digits = 6, int period = 30)
    {
        if (IsLocked) return;
        var idx = _credentials.FindIndex(c => c.Id == id);
        if (idx < 0) return;

        var old = _credentials[idx];
        _credentials[idx] = new VaultCredential
        {
            Id = old.Id, Origin = old.Origin, Username = old.Username, Password = old.Password,
            Label = old.Label, LoginUrl = old.LoginUrl,
            CreatedAt = old.CreatedAt, UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            TotpSecret = secret?.Trim() ?? string.Empty,
            TotpDigits = digits, TotpPeriod = period
        };
        Save();
    }

    public void Delete(string origin, string username)
    {
        if (IsLocked) return;
        _credentials.RemoveAll(c =>
            c.Origin.Equals(origin, StringComparison.OrdinalIgnoreCase) &&
            c.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        // Suppression persistante : empêche la resynchro depuis Chromium de le réimporter.
        var key = TombstoneKey(origin, username);
        if (!_header.Deleted.Contains(key)) _header.Deleted.Add(key);
        Save();
    }

    // Variante par Id, utilisée par le panneau coffre pour ne supprimer qu'un
    // seul compte quand plusieurs comptes partagent le même (origin, username).
    public void DeleteById(string id)
    {
        if (IsLocked) return;
        if (_credentials.RemoveAll(c => c.Id == id) == 0) return;
        var key = TombstoneKeyForId(id);
        if (!_header.Deleted.Contains(key)) _header.Deleted.Add(key);
        Save();
    }

    // ── Couplage au mot de passe du profil ────────────────────────────────────

    // Point d'entrée unique du couplage coffre ↔ mot de passe de profil.
    // - Coffre neuf (aucun mot de passe maître) : on l'active avec ce mot de passe.
    // - Coffre verrouillé : on tente de le déverrouiller.
    // - Coffre déjà ouvert : rien à faire.
    // Retourne false uniquement si le déverrouillage échoue (mauvais mot de passe).
    public bool EnsureUnlockedWith(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        if (!HasMasterPassword)
        {
            SetMasterPassword(password);
            return true;
        }
        if (_unlockedKey is not null) return true;
        return Unlock(password);
    }

    // Déverrouille le coffre AES ; retourne false si le mot de passe est incorrect
    public bool Unlock(string password)
    {
        if (!HasMasterPassword || _header.Salt is null) return false;
        var salt = Convert.FromBase64String(_header.Salt);
        var key  = DeriveKey(password, salt, _header);
        if (!TryDecrypt(_header.Data ?? string.Empty, key, _header.DataCipher, AadVaultData, out List<VaultCredential> creds)) return false;
        _unlockedKey  = key;
        _credentials  = creds;
        LoadCardsWithKey(key);
        // Migration en mémoire seule : Unlock() ne doit jamais écrire sur disque de
        // son propre chef (cf. VaultMigrationTests, un coffre CBC hérité doit rester
        // lisible tel quel tant qu'aucune écriture explicite n'a eu lieu). Les Id se
        // persisteront au prochain Save() déclenché par une vraie opération d'écriture.
        EnsureCredentialIds();
        return true;
    }

    // Décrypte le blob du portefeuille avec la clé du coffre. Un blob de cartes
    // illisible ne doit pas empêcher l'ouverture des identifiants : on trace et
    // on repart d'un portefeuille vide.
    private void LoadCardsWithKey(byte[] key)
    {
        if (string.IsNullOrEmpty(_header.Cards))
        {
            _cards = new();
            return;
        }

        if (TryDecrypt(_header.Cards, key, "gcm", AadVaultCards, out List<VaultPaymentCard> cards))
            _cards = cards;
        else
        {
            WinUiRuntimeTrace.Write("Vault cards blob unreadable, starting empty");
            _cards = new();
        }
    }

    public void Lock()
    {
        if (!HasMasterPassword) return;
        ZeroKey();
    }

    // Active (ou re-chiffre) le coffre AES + définit le mot de passe maître.
    // Utilisé aussi pour le changement de mot de passe (re-clé du fichier).
    public void SetMasterPassword(string password)
    {
        var deleted = _header.Deleted.ToList();
        var recoverySalt = _header.RecoverySalt;
        var recoveryWrappedKey = _header.RecoveryWrappedKey;
        var recoveryDataKey = TryGetRecoveryDataKey();
        var salt = RandomNumberGenerator.GetBytes(32);
        var key  = DeriveKey(password, salt, ArgonHeaderDefaults());
        _header  = new VaultHeader
        {
            Mode        = "aes256",
            Kdf         = "argon2id",
            Salt        = Convert.ToBase64String(salt),
            Argon2Mem   = Argon2MemoryKib,
            Argon2Iter  = Argon2Iterations,
            Argon2Par   = Argon2Parallelism,
            Deleted     = deleted,
            RecoverySalt = recoverySalt,
            RecoveryWrappedKey = recoveryWrappedKey
        };
        _unlockedKey = key;
        _recoveryDataKey = recoveryDataKey;
        Save();
    }

    // Vérifie sans déverrouiller (pour confirmation avant changement / désactivation)
    public bool VerifyMasterPassword(string password)
    {
        if (!HasMasterPassword || _header.Salt is null) return false;
        var salt = Convert.FromBase64String(_header.Salt);
        return TryDecrypt(_header.Data ?? string.Empty, DeriveKey(password, salt, _header), _header.DataCipher, AadVaultData, out List<VaultCredential> _);
    }

    // Repasse en mode DPAPI (confort) — coffre doit être déverrouillé avant l'appel
    public void ClearMasterPassword()
    {
        if (IsLocked) return;
        _header = new VaultHeader { Mode = "dpapi", Kdf = "none" };
        Save();
        ZeroKey();
    }

    // ── Déverrouillage par code PIN ───────────────────────────────────────────

    // Active le déverrouillage par PIN. Le coffre doit être déverrouillé (clé en mémoire).
    public void EnablePinUnlock(string pin)
    {
        if (_unlockedKey is null || string.IsNullOrEmpty(pin)) return;
        var salt   = RandomNumberGenerator.GetBytes(32);
        var pinKey = DeriveKey(pin, salt, ArgonHeaderDefaults());

        var wrapped = WrapKey(_unlockedKey, pinKey, AadPinWrap);

        var dpapi = ProtectedData.Protect(wrapped, VaultEntropy, DataProtectionScope.CurrentUser);
        _header.PinSalt       = Convert.ToBase64String(salt);
        _header.PinWrappedKey = Convert.ToBase64String(dpapi);
        Save();
    }

    public void DisablePinUnlock()
    {
        _header.PinSalt       = null;
        _header.PinWrappedKey = null;
        Save();
    }

    // Déverrouille le coffre à partir du PIN. Retourne false si PIN incorrect ou emballage
    // périmé (ex. après un changement de mot de passe).
    public bool UnlockWithPin(string pin)
    {
        if (!HasMasterPassword || _header.PinSalt is null || _header.PinWrappedKey is null) return false;
        try
        {
            var salt    = Convert.FromBase64String(_header.PinSalt);
            var pinKey  = DeriveKey(pin, salt, _header);
            var dpapi   = Convert.FromBase64String(_header.PinWrappedKey);
            var wrapped = UnprotectVaultBytes(dpapi);

            if (!TryUnwrapKey(Convert.ToBase64String(wrapped), pinKey, AadPinWrap, out var vaultKey)) return false;

            if (!TryDecrypt(_header.Data ?? string.Empty, vaultKey, _header.DataCipher, AadVaultData, out List<VaultCredential> creds)) return false;
            _unlockedKey = vaultKey;
            _credentials = creds;
            LoadCardsWithKey(vaultKey);
            EnsureCredentialIds(); // idem Unlock() : pas d'écriture forcée à l'ouverture
            return true;
        }
        catch { return false; }
    }

    // ── Recuperation locale ─────────────────────────────────────────────────

    public bool SetRecoveryKey(string recoveryKey)
    {
        if (_unlockedKey is null || string.IsNullOrWhiteSpace(recoveryKey)) return false;

        var recoveryDataKey = RandomNumberGenerator.GetBytes(KeySize);
        var recoverySalt = RandomNumberGenerator.GetBytes(32);
        var recoveryWrapKey = DeriveKey(NormalizeRecoveryKey(recoveryKey), recoverySalt, ArgonHeaderDefaults());

        _header.RecoverySalt = Convert.ToBase64String(recoverySalt);
        _header.RecoveryWrappedKey = Convert.ToBase64String(WrapKey(recoveryDataKey, recoveryWrapKey, AadRecoveryWrap));
        _header.RecoveryVaultWrappedKey = Convert.ToBase64String(WrapKey(recoveryDataKey, _unlockedKey, AadRecoveryVaultWrap));
        _recoveryDataKey = recoveryDataKey;
        Save();
        return true;
    }

    public bool UnlockWithRecoveryKey(string recoveryKey)
    {
        if (_header.RecoverySalt is null ||
            _header.RecoveryWrappedKey is null ||
            _header.RecoveryData is null ||
            string.IsNullOrWhiteSpace(recoveryKey))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(_header.RecoverySalt);
            var recoveryWrapKey = DeriveKey(NormalizeRecoveryKey(recoveryKey), salt, _header);
            if (!TryUnwrapKey(_header.RecoveryWrappedKey, recoveryWrapKey, AadRecoveryWrap, out var recoveryDataKey))
            {
                return false;
            }

            if (!TryDecrypt(_header.RecoveryData, recoveryDataKey, _header.RecoveryDataCipher, AadRecoveryData, out List<VaultCredential> creds))
            {
                CryptographicOperations.ZeroMemory(recoveryDataKey);
                return false;
            }

            _recoveryDataKey = recoveryDataKey;
            _credentials = creds;
            // Copie de secours du portefeuille (absente sur les anciens coffres).
            if (!string.IsNullOrEmpty(_header.RecoveryCards) &&
                TryDecrypt(_header.RecoveryCards, recoveryDataKey, "gcm", AadRecoveryCards, out List<VaultPaymentCard> cards))
                _cards = cards;
            else
                _cards = new();
            EnsureCredentialIds(); // pas de _unlockedKey ici : persistance différée au prochain Save() avec clé
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── Export / Import (maîtrise du fichier par l'utilisateur) ────────────────

    // Renvoie une copie en clair des identifiants (coffre déverrouillé requis).
    public IReadOnlyList<VaultCredential> ExportClear()
    {
        if (IsLocked) return Array.Empty<VaultCredential>();
        return _credentials.ToList();
    }

    // Fusionne des identifiants importés (upsert par origine+utilisateur). Renvoie le nombre traité.
    public int ImportClear(IEnumerable<(string origin, string username, string password)> items)
    {
        if (IsLocked) return 0;
        var count = 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var (origin, username, password) in items)
        {
            if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(password)) continue;
            // Respecter une suppression persistante : ne pas réimporter ce que l'utilisateur a supprimé.
            if (_header.Deleted.Contains(TombstoneKey(origin, username))) continue;
            var idx = _credentials.FindIndex(c =>
                c.Origin.Equals(origin, StringComparison.OrdinalIgnoreCase) &&
                c.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                _credentials[idx] = new VaultCredential
                {
                    Id = string.IsNullOrEmpty(_credentials[idx].Id) ? Guid.NewGuid().ToString("N") : _credentials[idx].Id,
                    Origin = _credentials[idx].Origin, Username = _credentials[idx].Username,
                    Password = password, Label = _credentials[idx].Label,
                    LoginUrl = _credentials[idx].LoginUrl,
                    CreatedAt = _credentials[idx].CreatedAt, UpdatedAt = now,
                    TotpSecret = _credentials[idx].TotpSecret,
                    TotpDigits = _credentials[idx].TotpDigits, TotpPeriod = _credentials[idx].TotpPeriod
                };
            else
                _credentials.Add(new VaultCredential
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Origin = origin, Username = username, Password = password,
                    CreatedAt = now, UpdatedAt = now
                });
            count++;
        }
        if (count > 0) Save();
        return count;
    }

    public int ImportClear(IEnumerable<(string origin, string username, string password, string loginUrl, string label)> items)
    {
        if (IsLocked) return 0;
        var count = 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var (origin, username, password, loginUrl, label) in items)
        {
            if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(password)) continue;
            if (_header.Deleted.Contains(TombstoneKey(origin, username))) continue;

            var cleanLoginUrl = loginUrl?.Trim() ?? string.Empty;
            var cleanLabel = label?.Trim() ?? string.Empty;
            var idx = _credentials.FindIndex(c =>
                c.Origin.Equals(origin, StringComparison.OrdinalIgnoreCase) &&
                c.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                var old = _credentials[idx];
                _credentials[idx] = new VaultCredential
                {
                    Id = string.IsNullOrEmpty(old.Id) ? Guid.NewGuid().ToString("N") : old.Id,
                    Origin = old.Origin,
                    Username = old.Username,
                    Password = password,
                    Label = string.IsNullOrWhiteSpace(cleanLabel) ? old.Label : cleanLabel,
                    LoginUrl = string.IsNullOrWhiteSpace(cleanLoginUrl) ? old.LoginUrl : cleanLoginUrl,
                    CreatedAt = old.CreatedAt,
                    UpdatedAt = now,
                    TotpSecret = old.TotpSecret, TotpDigits = old.TotpDigits, TotpPeriod = old.TotpPeriod
                };
            }
            else
            {
                _credentials.Add(new VaultCredential
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Origin = origin,
                    Username = username,
                    Password = password,
                    Label = cleanLabel,
                    LoginUrl = cleanLoginUrl,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            count++;
        }
        if (count > 0) Save();
        return count;
    }

    // ── Chargement ────────────────────────────────────────────────────────────

    private void Load()
    {
        try
        {
            if (!File.Exists(_vaultFile)) return;
            var outer = File.ReadAllText(_vaultFile, Encoding.UTF8);
            _header = JsonSerializer.Deserialize<VaultHeader>(outer, JsonOpts) ?? new VaultHeader();

            if (_header.Mode == "aes256")
                return; // credentials chargés dans Unlock()

            if (_header.Data is null) return;
            var cipher = Convert.FromBase64String(_header.Data);
            var plain  = UnprotectVaultBytes(cipher);
            _credentials = JsonSerializer.Deserialize<List<VaultCredential>>(plain, JsonOpts) ?? new();

            if (!string.IsNullOrEmpty(_header.Cards))
            {
                var cardsPlain = UnprotectVaultBytes(Convert.FromBase64String(_header.Cards));
                _cards = JsonSerializer.Deserialize<List<VaultPaymentCard>>(cardsPlain, JsonOpts) ?? new();
            }

            EnsureCredentialIds(); // pas d'écriture forcée au chargement, cf. Unlock()
        }
        catch (Exception error)
        {
            // Un coffre illisible (corruption, DPAPI d'un autre compte) ne doit pas
            // planter le démarrage, mais l'échec doit être traçable.
            WinUiRuntimeTrace.Write($"Vault load skipped: {error.GetType().Name}");
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(_credentials, JsonOpts);
            var cardsJson = JsonSerializer.SerializeToUtf8Bytes(_cards, JsonOpts);
            if (_header.Mode == "aes256" && _unlockedKey is not null)
            {
                _header.Data = Convert.ToBase64String(EncryptGcm(json, _unlockedKey, AadVaultData));
                _header.DataCipher = "gcm";
                _header.Cards = Convert.ToBase64String(EncryptGcm(cardsJson, _unlockedKey, AadVaultCards));
            }
            else if (_header.Mode == "aes256")
                _header.Data ??= string.Empty;
            else
            {
                _header.Data = Convert.ToBase64String(
                    ProtectedData.Protect(json, VaultEntropy, DataProtectionScope.CurrentUser));
                _header.Cards = Convert.ToBase64String(
                    ProtectedData.Protect(cardsJson, VaultEntropy, DataProtectionScope.CurrentUser));
            }

            SaveRecoveryPayload(json, cardsJson);
            var dir = Path.GetDirectoryName(_vaultFile);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_vaultFile, JsonSerializer.Serialize(_header, JsonOpts), Encoding.UTF8);
            // json/cardsJson contiennent tous les secrets en clair (mots de passe,
            // TOTP, numeros de carte...) : effaces des qu'ils ne servent plus,
            // meme principe que dans TryDecrypt.
            CryptographicOperations.ZeroMemory(json);
            CryptographicOperations.ZeroMemory(cardsJson);
        }
        catch (Exception error)
        {
            // Échec d'écriture (disque plein, verrou) : tracer pour diagnostiquer une
            // perte silencieuse plutôt que de l'ignorer.
            WinUiRuntimeTrace.Write($"Vault save skipped: {error.GetType().Name}");
        }
    }

    // ── Crypto ────────────────────────────────────────────────────────────────

    // Déchiffre un blob du coffre. cipher = valeur de l'en-tête "data_cipher" :
    // "gcm" pour les coffres actuels (AES-256-GCM authentifié), "cbc" pour les
    // anciens (AES-256-CBC, lus tels quels puis réécrits en GCM au prochain Save).
    private static bool TryDecrypt<T>(string dataBase64, byte[] key, string cipher, byte[] aad, out List<T> result)
    {
        result = new();
        try
        {
            if (string.IsNullOrEmpty(dataBase64)) return true; // coffre vide = valide
            var bytes = Convert.FromBase64String(dataBase64);
            var plain = cipher == "gcm" ? DecryptGcm(bytes, key, aad) : DecryptAes(bytes, key);
            try
            {
                result = JsonSerializer.Deserialize<List<T>>(plain, JsonOpts) ?? new();
            }
            finally
            {
                // Le JSON dechiffre contient les secrets en clair (mots de passe,
                // TOTP...) : reduit la fenetre pendant laquelle ce tampon
                // intermediaire reste en memoire, meme si les `string` qui en
                // sont issues (VaultCredential.Password) ne peuvent pas etre
                // effacees de la meme facon (immutables en C#).
                CryptographicOperations.ZeroMemory(plain);
            }
            return true;
        }
        catch { return false; }
    }

    private static byte[] UnprotectVaultBytes(byte[] cipher)
    {
        try
        {
            return ProtectedData.Unprotect(cipher, VaultEntropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            try
            {
                return ProtectedData.Unprotect(cipher, LegacyNovaVaultEntropy, DataProtectionScope.CurrentUser);
            }
            catch (CryptographicException)
            {
                return ProtectedData.Unprotect(cipher, LegacyPulseVaultEntropy, DataProtectionScope.CurrentUser);
            }
        }
    }

    // AES-256-GCM authentifié : nonce(12) ‖ tag(16) ‖ ciphertext. AAD toujours
    // posee a l'ecriture (voir les constantes Aad* plus haut).
    private static byte[] EncryptGcm(byte[] plaintext, byte[] key, byte[] aad)
    {
        var nonce  = RandomNumberGenerator.GetBytes(GcmNonce);
        var cipher = new byte[plaintext.Length];
        var tag    = new byte[GcmTag];
        using (var aes = new AesGcm(key, GcmTag))
            aes.Encrypt(nonce, plaintext, cipher, tag, aad);

        var result = new byte[GcmNonce + GcmTag + cipher.Length];
        Buffer.BlockCopy(nonce,  0, result, 0,                 GcmNonce);
        Buffer.BlockCopy(tag,    0, result, GcmNonce,          GcmTag);
        Buffer.BlockCopy(cipher, 0, result, GcmNonce + GcmTag, cipher.Length);
        return result;
    }

    // Retente avec une AAD vide si l'AAD attendue echoue : couvre les blobs GCM
    // ecrits avant l'introduction de l'AAD (equivalent a une AAD vide), qui
    // doivent rester lisibles. Reecrits avec l'AAD au prochain Save().
    private static byte[] DecryptGcm(byte[] data, byte[] key, byte[] aad)
    {
        if (data.Length < GcmNonce + GcmTag) throw new CryptographicException("Vault data too short");
        var nonce  = data[..GcmNonce];
        var tag    = data[GcmNonce..(GcmNonce + GcmTag)];
        var cipher = data[(GcmNonce + GcmTag)..];
        var plain  = new byte[cipher.Length];
        using var aes = new AesGcm(key, GcmTag);
        try
        {
            aes.Decrypt(nonce, cipher, tag, plain, aad);
        }
        catch (CryptographicException) when (aad.Length > 0)
        {
            aes.Decrypt(nonce, cipher, tag, plain);
        }
        return plain;
    }

    // Lecture seule des coffres hérités (AES-256-CBC : IV(16) ‖ ciphertext).
    private static byte[] DecryptAes(byte[] data, byte[] key)
    {
        if (data.Length < IvSize) throw new CryptographicException("Vault data too short");
        var iv     = data[..IvSize];
        var cipher = data[IvSize..];
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key     = key;
        aes.IV      = iv;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(cipher, 0, cipher.Length);
    }

    private void SaveRecoveryPayload(byte[] json, byte[] cardsJson)
    {
        var recoveryDataKey = TryGetRecoveryDataKey();
        if (recoveryDataKey is null) return;

        _header.RecoveryData = Convert.ToBase64String(EncryptGcm(json, recoveryDataKey, AadRecoveryData));
        _header.RecoveryDataCipher = "gcm";
        _header.RecoveryCards = Convert.ToBase64String(EncryptGcm(cardsJson, recoveryDataKey, AadRecoveryCards));
        if (_unlockedKey is not null)
        {
            _header.RecoveryVaultWrappedKey = Convert.ToBase64String(WrapKey(recoveryDataKey, _unlockedKey, AadRecoveryVaultWrap));
        }
    }

    private byte[]? TryGetRecoveryDataKey()
    {
        if (_recoveryDataKey is not null)
        {
            return _recoveryDataKey;
        }

        if (_unlockedKey is null || _header.RecoveryVaultWrappedKey is null)
        {
            return null;
        }

        return TryUnwrapKey(_header.RecoveryVaultWrappedKey, _unlockedKey, AadRecoveryVaultWrap, out var recoveryDataKey)
            ? recoveryDataKey
            : null;
    }

    private static byte[] WrapKey(byte[] keyToWrap, byte[] wrappingKey, byte[] aad)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[keyToWrap.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(wrappingKey, 16))
            aes.Encrypt(nonce, keyToWrap, cipher, tag, aad);

        var wrapped = new byte[nonce.Length + cipher.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, wrapped, 0, nonce.Length);
        Buffer.BlockCopy(cipher, 0, wrapped, nonce.Length, cipher.Length);
        Buffer.BlockCopy(tag, 0, wrapped, nonce.Length + cipher.Length, tag.Length);
        return wrapped;
    }

    // Meme repli AAD-vide qu'DecryptGcm, pour les cles emballees avant ce
    // correctif (PIN, cle de recuperation).
    private static bool TryUnwrapKey(string wrappedBase64, byte[] wrappingKey, byte[] aad, out byte[] key)
    {
        key = Array.Empty<byte>();
        try
        {
            var wrapped = Convert.FromBase64String(wrappedBase64);
            if (wrapped.Length < 12 + 16 + 1) return false;

            var nonce = wrapped[..12];
            var tag = wrapped[^16..];
            var cipher = wrapped[12..^16];
            var plain = new byte[cipher.Length];
            using (var aes = new AesGcm(wrappingKey, 16))
            {
                try
                {
                    aes.Decrypt(nonce, cipher, tag, plain, aad);
                }
                catch (CryptographicException) when (aad.Length > 0)
                {
                    aes.Decrypt(nonce, cipher, tag, plain);
                }
            }
            key = plain;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeRecoveryKey(string recoveryKey) =>
        new(recoveryKey
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

    // Dérivation de clé selon le KDF indiqué par l'en-tête (Argon2id par défaut,
    // PBKDF2 conservé pour lire les anciens coffres).
    private static byte[] DeriveKey(string password, byte[] salt, VaultHeader header)
    {
        if (header.Kdf == "pbkdf2")
        {
            using var rfc = new Rfc2898DeriveBytes(
                password, salt, LegacyPbkdf2Iterations, HashAlgorithmName.SHA256);
            return rfc.GetBytes(KeySize);
        }

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt                = salt,
            MemorySize          = header.Argon2Mem  > 0 ? header.Argon2Mem  : Argon2MemoryKib,
            Iterations          = header.Argon2Iter > 0 ? header.Argon2Iter : Argon2Iterations,
            DegreeOfParallelism = header.Argon2Par  > 0 ? header.Argon2Par  : Argon2Parallelism
        };
        return argon2.GetBytes(KeySize);
    }

    private static VaultHeader ArgonHeaderDefaults() => new()
    {
        Kdf        = "argon2id",
        Argon2Mem  = Argon2MemoryKib,
        Argon2Iter = Argon2Iterations,
        Argon2Par  = Argon2Parallelism
    };

    private void ZeroKey()
    {
        if (_unlockedKey is not null)
        {
            CryptographicOperations.ZeroMemory(_unlockedKey);
            _unlockedKey = null;
        }

        if (_recoveryDataKey is not null)
        {
            CryptographicOperations.ZeroMemory(_recoveryDataKey);
            _recoveryDataKey = null;
        }
    }
}

internal sealed class VaultHeader
{
    [JsonPropertyName("version")]    public int     Version    { get; set; } = 2;
    [JsonPropertyName("mode")]       public string  Mode       { get; set; } = "dpapi";
    // Défaut "pbkdf2" : un fichier sans champ "kdf" est un ancien coffre PBKDF2.
    // Les nouveaux fichiers écrivent toujours "kdf" explicitement (argon2id / none).
    [JsonPropertyName("kdf")]        public string  Kdf        { get; set; } = "pbkdf2";
    [JsonPropertyName("salt")]       public string? Salt       { get; set; }
    [JsonPropertyName("argon2_mem")] public int     Argon2Mem  { get; set; }
    [JsonPropertyName("argon2_iter")]public int     Argon2Iter { get; set; }
    [JsonPropertyName("argon2_par")] public int     Argon2Par  { get; set; }
    [JsonPropertyName("data")]       public string? Data       { get; set; }
    // Chiffrement du blob "data" / "recovery_data". "gcm" = AES-256-GCM authentifié
    // (défaut à l'écriture). Absent = ancien coffre en AES-256-CBC → lu en "cbc".
    [JsonPropertyName("data_cipher")] public string DataCipher { get; set; } = "cbc";

    // Déverrouillage optionnel par code PIN : la clé du coffre est emballée (AES-GCM
    // avec une clé dérivée du PIN par Argon2id), puis protégée par DPAPI. Ainsi le PIN
    // seul ne suffit pas hors de la session Windows.
    [JsonPropertyName("pin_salt")] public string? PinSalt       { get; set; }
    [JsonPropertyName("pin_key")]  public string? PinWrappedKey { get; set; }

    // Recuperation locale : une cle de donnees de secours chiffre une copie du
    // coffre. Elle est emballée par la cle de recuperation utilisateur et, quand
    // le coffre est ouvert, par la cle du coffre pour pouvoir maintenir la copie
    // a jour sans stocker la cle de recuperation en clair.
    [JsonPropertyName("recovery_salt")]      public string? RecoverySalt            { get; set; }
    [JsonPropertyName("recovery_key")]       public string? RecoveryWrappedKey      { get; set; }
    [JsonPropertyName("recovery_vault_key")] public string? RecoveryVaultWrappedKey { get; set; }
    [JsonPropertyName("recovery_data")]      public string? RecoveryData            { get; set; }
    // Chiffrement du blob "recovery_data", indépendant de "data_cipher" car les deux
    // blobs peuvent être réécrits à des instants différents (déverrouillage par clé
    // de secours). Absent = ancien coffre CBC.
    [JsonPropertyName("recovery_data_cipher")] public string RecoveryDataCipher { get; set; } = "cbc";

    // Portefeuille : cartes de paiement, blob séparé chiffré avec la même clé que
    // "data". Toujours en GCM (le portefeuille n'a jamais existé en CBC). Absent
    // sur les coffres antérieurs à 0.45 → portefeuille vide.
    [JsonPropertyName("cards")]          public string? Cards         { get; set; }
    [JsonPropertyName("recovery_cards")] public string? RecoveryCards { get; set; }

    // Suppressions persistantes (clés "origin|username") pour ne pas réimporter depuis Chromium.
    [JsonPropertyName("deleted")]  public List<string> Deleted  { get; set; } = new();
}
