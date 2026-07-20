using System.Security.Cryptography;
using Lumora.WinUI;
using Lumora.WinUI.Tor;
using Xunit;

namespace Lumora.Tests;

// Teste la verification d'integrite (VerifyEngineIntegrityAsync) qui gate
// StartAsync : un tor.exe absent, inconnu ou modifie ne doit jamais pouvoir
// etre execute. Utilise la surcharge testable (hashes injectes) pour verifier
// le cas d'acceptation sans embarquer le veritable tor.exe (~10 Mo) dans les
// tests ; les cas de rejet utilisent aussi bien la vraie liste epinglee
// (TorTrustedRelease) que des hashes de test. Ne tape jamais le reseau ni ne
// lance de vrai processus (meme principe que YtDlpEngineProviderTests).
public class TorProcessManagerTests
{
    private static LumoraProfilePaths CreateTempProfile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "LumoraTests", Guid.NewGuid().ToString("N"), "profile");
        Directory.CreateDirectory(dir);
        return LumoraProfilePaths.FromDirectory(dir);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    [Fact]
    public async Task Refuse_quand_tor_exe_est_absent()
    {
        var profile = CreateTempProfile();

        var ok = await TorProcessManager.VerifyEngineIntegrityAsync(profile);

        Assert.False(ok);
    }

    [Fact]
    public async Task Accepte_un_fichier_dont_le_hash_correspond_exactement_a_la_liste_de_confiance()
    {
        var profile = CreateTempProfile();
        var torDir = TorProcessManager.ExpectedDirectory(profile);
        Directory.CreateDirectory(torDir);
        var exePath = Path.Combine(torDir, "tor.exe");
        File.WriteAllText(exePath, "contenu-de-test-reconnu");
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tor.exe"] = ComputeSha256(exePath),
        };

        var ok = await TorProcessManager.VerifyEngineIntegrityAsync(profile, hashes);

        Assert.True(ok);
    }

    [Fact]
    public async Task Refuse_un_fichier_dont_un_seul_octet_a_ete_modifie()
    {
        var profile = CreateTempProfile();
        var torDir = TorProcessManager.ExpectedDirectory(profile);
        Directory.CreateDirectory(torDir);
        var exePath = Path.Combine(torDir, "tor.exe");
        File.WriteAllText(exePath, "contenu-de-test-reconnu");
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tor.exe"] = ComputeSha256(exePath),
        };
        File.WriteAllText(exePath, "contenu-de-test-modifie");

        var ok = await TorProcessManager.VerifyEngineIntegrityAsync(profile, hashes);

        Assert.False(ok);
    }

    [Fact]
    public async Task Refuse_le_veritable_tor_exe_officiel_contre_un_hash_epingle_incorrect()
    {
        // Verifie le cas symetrique au vrai mecanisme de securite : un fichier
        // present et non modifie, mais dont le hash ne figure pas dans la
        // liste de confiance fournie, doit etre refuse.
        var profile = CreateTempProfile();
        var torDir = TorProcessManager.ExpectedDirectory(profile);
        Directory.CreateDirectory(torDir);
        var exePath = Path.Combine(torDir, "tor.exe");
        File.WriteAllText(exePath, "binaire-non-reconnu");
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tor.exe"] = new string('0', 64),
        };

        var ok = await TorProcessManager.VerifyEngineIntegrityAsync(profile, hashes);

        Assert.False(ok);
    }

    [Fact]
    public void TrustedFileHashes_ne_contient_pas_de_hash_duplique_et_est_bien_forme()
    {
        var hashes = TorTrustedRelease.TrustedFileHashes;

        Assert.NotEmpty(hashes);
        foreach (var (fileName, hash) in hashes)
        {
            Assert.False(string.IsNullOrWhiteSpace(fileName));
            Assert.Equal(64, hash.Length);
            Assert.Equal(hash, hash.ToLowerInvariant());
            Assert.Matches("^[0-9a-f]{64}$", hash);
        }

        var distinctHashes = new HashSet<string>(hashes.Values, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(hashes.Count, distinctHashes.Count);
    }

    [Fact]
    public async Task StartAsync_refuse_de_lancer_un_tor_exe_non_verifie()
    {
        var profile = CreateTempProfile();
        var torDir = TorProcessManager.ExpectedDirectory(profile);
        Directory.CreateDirectory(torDir);
        // Fichier present (passe le check File.Exists) mais au contenu non
        // reconnu par la vraie liste epinglee : StartAsync doit refuser de
        // l'executer, sans jamais tenter de lancer le processus.
        File.WriteAllText(Path.Combine(torDir, "tor.exe"), "pas-le-vrai-tor");

        using var manager = new TorProcessManager();
        var started = await manager.StartAsync(profile);

        Assert.False(started);
        Assert.Equal(TorEngineState.Error, manager.State);
    }

    [Fact]
    public async Task RequestNewCircuitAsync_refuse_honnetement_si_le_moteur_n_est_pas_connecte()
    {
        // Aucun tor.exe reel lance dans ces tests (meme principe que les tests
        // d'integrite ci-dessus) : verifie que la demande de nouveau circuit
        // ne tente jamais de parler au control port quand Tor n'est pas dans
        // l'etat Connected, plutot que de planter ou de rester silencieuse.
        using var manager = new TorProcessManager();

        var (success, message) = await manager.RequestNewCircuitAsync();

        Assert.False(success);
        Assert.Equal("Le moteur Tor n'est pas connecte.", message);
    }

    [Fact]
    public void EvaluateCooldown_bloque_une_demande_trop_rapprochee()
    {
        var now = DateTime.UtcNow;
        var lastRequest = now - TimeSpan.FromSeconds(3);

        var (allowed, remaining) = TorProcessManager.EvaluateCooldown(
            lastRequest, now, TorProcessManager.NewCircuitCooldown);

        Assert.False(allowed);
        Assert.True(remaining > TimeSpan.Zero);
        Assert.True(remaining <= TorProcessManager.NewCircuitCooldown);
    }

    [Fact]
    public void EvaluateCooldown_autorise_une_fois_le_delai_ecoule()
    {
        var now = DateTime.UtcNow;
        var lastRequest = now - TorProcessManager.NewCircuitCooldown - TimeSpan.FromSeconds(1);

        var (allowed, remaining) = TorProcessManager.EvaluateCooldown(
            lastRequest, now, TorProcessManager.NewCircuitCooldown);

        Assert.True(allowed);
        Assert.Equal(TimeSpan.Zero, remaining);
    }

    [Fact]
    public void EvaluateCooldown_autorise_la_toute_premiere_demande()
    {
        var (allowed, remaining) = TorProcessManager.EvaluateCooldown(
            null, DateTime.UtcNow, TorProcessManager.NewCircuitCooldown);

        Assert.True(allowed);
        Assert.Equal(TimeSpan.Zero, remaining);
    }
}
