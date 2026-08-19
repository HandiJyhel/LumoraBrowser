using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;

namespace Lumora.WinUI;

// Leve a l'export si le coffre est en mode "mot de passe maitre" (aes256) et
// verrouille (mot de passe pas encore entre cette session) : l'export est
// bloque plutot que de continuer silencieusement sans le coffre - retour
// utilisateur explicite du 2026-08-13 ("j'aurais tout perdu... c'est pas
// logique" a propos d'un coffre absent d'une sauvegarde presentee comme
// complete), voir MEMORY.md.
public sealed class VaultLockedForBackupException : Exception
{
    public VaultLockedForBackupException()
        : base("Le coffre est verrouillé. Déverrouillez-le avant de créer une sauvegarde.") { }
}

// Format de sauvegarde Lumora (extension .lum depuis le 2026-08-14, session
// "Compte et ouverture" ; .lumorabackup/.novabackup restent acceptes en
// import pour les sauvegardes deja existantes - voir MainWindow.SettingsStorage.cs).
// Le format binaire ci-dessous est totalement indifferent au nom du fichier,
// seul le magic "NOVABAK" suivant est verifie a la lecture :
//   [0-6]   Magic "NOVABAK"
//   [7]     Version
//   [8-23]  Salt (16 octets)
//   Version 0x02 (actuelle) : clé Argon2id, payload AES-256-GCM authentifié.
//     [24-35] Nonce (12) ‖ [36-51] Tag (16) ‖ [52+] ciphertext
//   Version 0x01 (héritée, lue seulement) : clé PBKDF2-SHA256 100k, AES-256-CBC.
//     [24-39] IV (16) ‖ [40+] ciphertext
//
// Le zip contient tout ce qui fait le profil, décrypté du DPAPI machine
// (fichiers .lumora) au moment de l'export : navigation, groupes d'onglets,
// web apps + icônes, favicons, flux RSS, clés d'accès, avatar, paramètres,
// ET le compte (nom + hash de mot de passe/PIN/clé de récupération, fichier
// profile.lumora - inclus depuis le 2026-08-15, voir TryReadAccountFile plus
// bas : un bug d'entropie DPAPI le faisait échouer silencieusement avant).
// Le coffre (mots de passe/cartes) est TOUJOURS inclus (2026-08-13, retour
// utilisateur : un coffre absent d'une sauvegarde "complète" est une
// catastrophe silencieuse pour l'utilisateur qui la découvre après avoir
// formaté). Son propre chiffrement (DPAPI ou mot de passe maître) est retiré
// à l'export - la protection vient désormais uniquement de l'enveloppe
// .lumorabackup elle-même (Argon2id + AES-256-GCM ci-dessus), exactement
// comme pour les favoris/notes/etc. juste au-dessus. Un coffre en mode "mot
// de passe maître" encore verrouillé au moment de l'export bloque l'export
// entier (VaultLockedForBackupException) plutôt que de continuer sans lui.

internal static class LumoraBackup
{
    // CredentialCount/CardCount : nombre d'éléments du coffre effectivement
    // inclus, pour que l'appelant (bouton "Exporter") puisse confirmer à
    // l'utilisateur que "oui, vos mots de passe sont bien dans ce fichier".
    public readonly record struct ExportResult(int CredentialCount, int CardCount);

    private static readonly byte[] Magic = "NOVABAK"u8.ToArray();
    private const byte FormatVersion = 0x02;
    private const byte LegacyFormatVersion = 0x01;
    private const int SaltSize = 16;
    private const int IvSize = 16;      // AES-CBC hérité (import v1)
    private const int GcmNonce = 12;
    private const int GcmTag = 16;
    private const int KeySize = 32;     // AES-256
    private const int LegacyPbkdf2Iterations = 100_000;

    // Argon2id — aligné sur le coffre principal (voir VaultStore).
    private const int Argon2MemoryKib = 65536; // 64 Mio
    private const int Argon2Iterations = 3;
    private const int Argon2Parallelism = 4;

    public static ExportResult Export(string destPath, string password, LumoraProfilePaths profile, VaultStore vault)
    {
        if (vault.IsLocked)
            throw new VaultLockedForBackupException();

        var credentials = vault.ListCredentials();
        var cards = vault.ListCards();

        using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifest = $"{{\"format\":2,\"created_at\":\"{DateTime.UtcNow:O}\"}}";
            WriteEntry(zip, "manifest.json", manifest);

            WriteProfileEntry(zip, profile.BookmarksFile, "navigation/bookmarks.txt");
            WriteProfileEntry(zip, profile.HistoryFile, "navigation/history.txt");
            WriteProfileEntry(zip, profile.NotesFile, "navigation/notes.txt");
            WriteProfileEntry(zip, profile.AnnotationsFile, "navigation/annotations.txt");
            WriteProfileEntry(zip, profile.TabsFile, "navigation/tabs.txt");
            WriteProfileEntry(zip, profile.UiSettingsFile, "navigation/ui-settings.txt");
            WriteProfileEntry(zip, profile.SavedTabGroupsFile, "navigation/saved-tab-groups.txt");
            WriteProfileEntry(zip, profile.SiteRelocationsFile, "navigation/site-relocations.txt");
            WriteProfileEntry(zip, profile.SemanticIndexFile, "navigation/semantic-index.txt");
            WriteProfileEntry(zip, profile.DownloadsFile, "navigation/downloads.txt");
            WriteProfileEntry(zip, profile.PasskeysFile, "navigation/passkeys.txt");
            WriteProfileEntry(zip, profile.WebAppsFile, "navigation/webapps.txt");
            WriteProfileEntry(zip, profile.RssFeedsFile, "navigation/rss-feeds.txt");

            WriteDirectoryEntries(zip, profile.FaviconsDir, "navigation/favicons");
            WriteDirectoryEntries(zip, profile.WebAppIconsDir, "navigation/webapp-icons");

            // profile.lumora (identite du compte : nom + hash de mot de passe) n'est
            // PAS chiffre comme les autres fichiers du profil (LumoraFile, entropie
            // "Lumora.WinUI.v1") mais directement par UserProfile.Save (DPAPI brut,
            // SANS entropie) - lire avec LumoraFile ici echouait silencieusement
            // (TryReadAllText avale l'exception), donc le compte n'a jamais ete
            // inclus dans AUCUNE sauvegarde jusqu'ici (bug trouve le 2026-08-15).
            var profileContent = TryReadAccountFile(profile.ProfileFile);
            if (profileContent is not null)
                WriteEntry(zip, "profile.txt", profileContent);

            var avatarPath = ProfileAvatarResolver.Find(profile.ProfileDir);
            if (avatarPath is not null)
                WriteBinaryEntry(zip, "avatar" + Path.GetExtension(avatarPath), File.ReadAllBytes(avatarPath));

            // Coffre : contenu déchiffré (voir commentaire d'en-tête du fichier),
            // protégé uniquement par l'enveloppe .lumorabackup à partir d'ici -
            // même traitement que bookmarks.txt/notes.txt/etc. juste au-dessus.
            WriteEntry(zip, "vault-credentials.json", JsonSerializer.Serialize(credentials));
            WriteEntry(zip, "vault-cards.json", JsonSerializer.Serialize(cards));
        }

        var salt  = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(GcmNonce);
        var key   = DeriveKey(password, salt);

        var plaintext = zipStream.ToArray();
        var cipher    = new byte[plaintext.Length];
        var tag       = new byte[GcmTag];
        using (var aes = new AesGcm(key, GcmTag))
            aes.Encrypt(nonce, plaintext, cipher, tag);

        using var file = new BinaryWriter(File.Open(destPath, FileMode.Create, FileAccess.Write));
        file.Write(Magic);
        file.Write(FormatVersion);
        file.Write(salt);
        file.Write(nonce);
        file.Write(tag);
        file.Write(cipher);

        return new ExportResult(credentials.Count, cards.Count);
    }

    public static void Import(string srcPath, string password, LumoraProfilePaths profile, VaultStore vault)
    {
        using var file = new BinaryReader(File.OpenRead(srcPath));

        var magic = file.ReadBytes(Magic.Length);
        if (!magic.SequenceEqual(Magic))
            throw new InvalidDataException("Ce fichier n'est pas une sauvegarde Lumora valide.");

        var version = file.ReadByte();
        if (version != FormatVersion && version != LegacyFormatVersion)
            throw new InvalidDataException($"Version de sauvegarde non supportee ({version}).");

        var salt = file.ReadBytes(SaltSize);

        byte[] zipBytes;
        try
        {
            zipBytes = version == FormatVersion
                ? DecryptV2(file, password, salt)
                : DecryptV1Legacy(file, password, salt);
        }
        catch
        {
            throw new CryptographicException("Mot de passe incorrect ou sauvegarde corrompue.");
        }

        try
        {
            using var zipStream = new MemoryStream(zipBytes);
            using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);

            Directory.CreateDirectory(profile.NavigationDir);

            ReadProfileEntry(zip, "navigation/bookmarks.txt", profile.BookmarksFile);
            ReadProfileEntry(zip, "navigation/history.txt", profile.HistoryFile);
            ReadProfileEntry(zip, "navigation/notes.txt", profile.NotesFile);
            ReadProfileEntry(zip, "navigation/annotations.txt", profile.AnnotationsFile);
            ReadProfileEntry(zip, "navigation/tabs.txt", profile.TabsFile);
            ReadProfileEntry(zip, "navigation/ui-settings.txt", profile.UiSettingsFile);
            ReadProfileEntry(zip, "navigation/saved-tab-groups.txt", profile.SavedTabGroupsFile);
            ReadProfileEntry(zip, "navigation/site-relocations.txt", profile.SiteRelocationsFile);
            ReadProfileEntry(zip, "navigation/semantic-index.txt", profile.SemanticIndexFile);
            ReadProfileEntry(zip, "navigation/downloads.txt", profile.DownloadsFile);
            ReadProfileEntry(zip, "navigation/passkeys.txt", profile.PasskeysFile);
            ReadProfileEntry(zip, "navigation/webapps.txt", profile.WebAppsFile);
            ReadProfileEntry(zip, "navigation/rss-feeds.txt", profile.RssFeedsFile);

            ReadDirectoryEntries(zip, "navigation/favicons/", profile.FaviconsDir);
            ReadDirectoryEntries(zip, "navigation/webapp-icons/", profile.WebAppIconsDir);

            var profileEntry = zip.GetEntry("profile.txt");
            if (profileEntry is not null)
            {
                using var reader = new StreamReader(profileEntry.Open(), Encoding.UTF8);
                WriteAccountFile(profile.ProfileFile, reader.ReadToEnd());
            }

            var avatarEntry = zip.Entries.FirstOrDefault(entry =>
                entry.FullName.StartsWith("avatar.", StringComparison.Ordinal));
            if (avatarEntry is not null)
            {
                Directory.CreateDirectory(profile.ProfileDir);
                foreach (var ext in ProfileAvatarResolver.Extensions)
                {
                    var existing = Path.Combine(profile.ProfileDir, "avatar" + ext);
                    if (File.Exists(existing)) File.Delete(existing);
                }
                using var destStream = File.Create(Path.Combine(profile.ProfileDir, avatarEntry.FullName));
                using var avatarStream = avatarEntry.Open();
                avatarStream.CopyTo(destStream);
            }

            var credentialsEntry = zip.GetEntry("vault-credentials.json");
            if (credentialsEntry is not null)
            {
                // Format actuel (2026-08-13) : credentials/cartes en clair,
                // reconstruits dans le coffre cible via RestoreFromBackup (mode
                // DPAPI ambient d'un profil neuf le plus souvent - voir son
                // commentaire d'en-tete pour le cas d'un import dans un profil
                // deja existant et verrouille).
                using var credReader = new StreamReader(credentialsEntry.Open(), Encoding.UTF8);
                var credentials = JsonSerializer.Deserialize<List<VaultCredential>>(credReader.ReadToEnd()) ?? new();

                var cards = new List<VaultPaymentCard>();
                var cardsEntry = zip.GetEntry("vault-cards.json");
                if (cardsEntry is not null)
                {
                    using var cardsReader = new StreamReader(cardsEntry.Open(), Encoding.UTF8);
                    cards = JsonSerializer.Deserialize<List<VaultPaymentCard>>(cardsReader.ReadToEnd()) ?? new();
                }

                vault.RestoreFromBackup(credentials, cards);
            }
            else
            {
                // Repli de compatibilite : sauvegardes creees avant ce format
                // (jusqu'a 0.93.35.0-dev), qui ne contenaient le coffre que s'il
                // etait deja en mode "mot de passe maitre" a l'export. Copie
                // brute historique, inchangee.
                var legacyVaultEntry = zip.GetEntry("vault.lumora");
                if (legacyVaultEntry is not null)
                {
                    using var reader = new StreamReader(legacyVaultEntry.Open(), Encoding.UTF8);
                    var dir = Path.GetDirectoryName(profile.VaultFile);
                    if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(profile.VaultFile, reader.ReadToEnd(), Encoding.UTF8);
                }
            }
        }
        catch (InvalidDataException)
        {
            throw new CryptographicException("Mot de passe incorrect ou sauvegarde corrompue.");
        }
    }

    private static byte[] DecryptV2(BinaryReader file, string password, byte[] salt)
    {
        var nonce     = file.ReadBytes(GcmNonce);
        var tag       = file.ReadBytes(GcmTag);
        var remaining = (int)(file.BaseStream.Length - file.BaseStream.Position);
        var cipher    = file.ReadBytes(remaining);

        var key   = DeriveKey(password, salt);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, GcmTag);
        aes.Decrypt(nonce, cipher, tag, plain); // lève si mot de passe incorrect / altération
        return plain;
    }

    private static byte[] DecryptV1Legacy(BinaryReader file, string password, byte[] salt)
    {
        var iv        = file.ReadBytes(IvSize);
        var remaining = (int)(file.BaseStream.Length - file.BaseStream.Position);
        var encrypted = file.ReadBytes(remaining);

        var key = DeriveKeyLegacy(password, salt);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV  = iv;
        using var decryptor = aes.CreateDecryptor();
        using var buf = new MemoryStream();
        using (var cs = new CryptoStream(new MemoryStream(encrypted), decryptor, CryptoStreamMode.Read))
            cs.CopyTo(buf);
        return buf.ToArray();
    }

    // profile.lumora suit le chiffrement de UserProfile.Save/Load (DPAPI CurrentUser,
    // entropie null) - volontairement DIFFERENT de LumoraFile (entropie
    // "Lumora.WinUI.v1") utilise par tous les autres fichiers du profil. Ne pas
    // fusionner avec LumoraFile.TryReadAllText/WriteAllText : ce serait re-casser
    // ce meme bug dans l'autre sens.
    private static string? TryReadAccountFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var cipher = File.ReadAllBytes(path);
            var plain  = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch { return null; }
    }

    private static void WriteAccountFile(string path, string content)
    {
        var plain  = Encoding.UTF8.GetBytes(content);
        var cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        var dir    = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, cipher);
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt                = salt,
            MemorySize          = Argon2MemoryKib,
            Iterations          = Argon2Iterations,
            DegreeOfParallelism = Argon2Parallelism
        };
        return argon2.GetBytes(KeySize);
    }

    private static byte[] DeriveKeyLegacy(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            Encoding.UTF8.GetBytes(password), salt, LegacyPbkdf2Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize);
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static void WriteProfileEntry(ZipArchive zip, string path, string entry)
    {
        var content = LumoraFile.TryReadAllText(path);
        if (content is not null)
            WriteEntry(zip, entry, content);
    }

    private static void ReadProfileEntry(ZipArchive zip, string entryName, string destinationPath)
    {
        var zipEntry = zip.GetEntry(entryName);
        if (zipEntry is null) return;
        using var reader = new StreamReader(zipEntry.Open(), Encoding.UTF8);
        LumoraFile.WriteAllText(destinationPath, reader.ReadToEnd());
    }

    // Fichiers binaires (favicons, icônes de web apps, avatar) : copiés tels
    // quels, aucun n'est protégé par DPAPI (contrairement aux .lumora).
    private static void WriteBinaryEntry(ZipArchive zip, string name, byte[] content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static void WriteDirectoryEntries(ZipArchive zip, string dirPath, string zipDirPrefix)
    {
        if (!Directory.Exists(dirPath)) return;
        foreach (var filePath in Directory.GetFiles(dirPath))
            WriteBinaryEntry(zip, $"{zipDirPrefix}/{Path.GetFileName(filePath)}", File.ReadAllBytes(filePath));
    }

    private static void ReadDirectoryEntries(ZipArchive zip, string zipDirPrefix, string destDir)
    {
        var entries = zip.Entries
            .Where(entry => entry.FullName.StartsWith(zipDirPrefix, StringComparison.Ordinal))
            .ToList();
        if (entries.Count == 0) return;

        Directory.CreateDirectory(destDir);
        foreach (var entry in entries)
        {
            var destPath = Path.Combine(destDir, Path.GetFileName(entry.FullName));
            using var destStream = File.Create(destPath);
            using var entryStream = entry.Open();
            entryStream.CopyTo(destStream);
        }
    }
}
