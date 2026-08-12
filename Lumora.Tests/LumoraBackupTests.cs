using System.Security.Cryptography;
using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Vérifie le round-trip de la sauvegarde chiffrée (.lumorabackup v2 : Argon2id + AES-GCM)
// et le rejet d'un mauvais mot de passe.
public sealed class LumoraBackupTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "nova_bak_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Export_puis_Import_restitue_les_donnees_de_navigation()
    {
        var sourceDir = Path.Combine(_root, "source");
        var source = LumoraProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);
        LumoraFile.WriteAllText(source.BookmarksFile, "signet\tperso\thttps://exemple.fr");

        var backupFile = Path.Combine(_root, "sauvegarde.lumorabackup");
        LumoraBackup.Export(backupFile, "phrase-secrete", source);
        Assert.True(File.Exists(backupFile));

        var targetDir = Path.Combine(_root, "cible");
        var target = LumoraProfilePaths.FromDirectory(targetDir);
        LumoraBackup.Import(backupFile, "phrase-secrete", target);

        Assert.Equal(
            LumoraFile.ReadAllText(source.BookmarksFile),
            LumoraFile.ReadAllText(target.BookmarksFile));
    }

    [Fact]
    public void Import_avec_mauvais_mot_de_passe_leve_une_exception()
    {
        var sourceDir = Path.Combine(_root, "source2");
        var source = LumoraProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);
        LumoraFile.WriteAllText(source.BookmarksFile, "signet\tperso\thttps://exemple.fr");

        var backupFile = Path.Combine(_root, "sauvegarde2.lumorabackup");
        LumoraBackup.Export(backupFile, "bon-mot-de-passe", source);

        var target = LumoraProfilePaths.FromDirectory(Path.Combine(_root, "cible2"));
        Assert.Throws<CryptographicException>(
            () => LumoraBackup.Import(backupFile, "mauvais", target));
    }

    // Ce qui manquait avant l'extension du 2026-08-12 : groupes d'onglets, web
    // apps, RSS, passkeys, downloads, index sémantique, favicons/icônes et avatar
    // faisaient partie du profil sur disque mais pas du .lumorabackup.
    [Fact]
    public void Export_puis_Import_restitue_les_fichiers_profil_ajoutes()
    {
        var sourceDir = Path.Combine(_root, "source3");
        var source = LumoraProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);
        LumoraFile.WriteAllText(source.SavedTabGroupsFile, "[{\"name\":\"travail\"}]");
        LumoraFile.WriteAllText(source.SiteRelocationsFile, "{}");
        LumoraFile.WriteAllText(source.SemanticIndexFile, "[]");
        LumoraFile.WriteAllText(source.DownloadsFile, "[]");
        LumoraFile.WriteAllText(source.PasskeysFile, "exemple.fr");
        LumoraFile.WriteAllText(source.WebAppsFile, "[]");
        LumoraFile.WriteAllText(source.RssFeedsFile, "[]");

        Directory.CreateDirectory(source.FaviconsDir);
        File.WriteAllBytes(Path.Combine(source.FaviconsDir, "abc.png"), [1, 2, 3]);
        Directory.CreateDirectory(source.WebAppIconsDir);
        File.WriteAllBytes(Path.Combine(source.WebAppIconsDir, "app.ico"), [4, 5, 6]);
        File.WriteAllBytes(Path.Combine(source.ProfileDir, "avatar.png"), [7, 8, 9]);

        var backupFile = Path.Combine(_root, "sauvegarde3.lumorabackup");
        LumoraBackup.Export(backupFile, "phrase-secrete", source);

        var targetDir = Path.Combine(_root, "cible3");
        var target = LumoraProfilePaths.FromDirectory(targetDir);
        LumoraBackup.Import(backupFile, "phrase-secrete", target);

        Assert.Equal("[{\"name\":\"travail\"}]", LumoraFile.ReadAllText(target.SavedTabGroupsFile));
        Assert.Equal("exemple.fr", LumoraFile.ReadAllText(target.PasskeysFile));
        Assert.Equal("[]", LumoraFile.ReadAllText(target.RssFeedsFile));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(target.FaviconsDir, "abc.png")));
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(Path.Combine(target.WebAppIconsDir, "app.ico")));
        Assert.Equal(new byte[] { 7, 8, 9 }, File.ReadAllBytes(Path.Combine(target.ProfileDir, "avatar.png")));
    }

    [Fact]
    public void Export_inclut_le_coffre_portable_et_le_restitue_a_l_import()
    {
        var sourceDir = Path.Combine(_root, "source4");
        var source = LumoraProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);

        var vault = new VaultStore(source.VaultFile);
        vault.SetMasterPassword("mot-de-passe-coffre");
        vault.Upsert("https://exemple.fr", "alice", "s3cret");
        Assert.True(VaultStore.IsPortable(source.VaultFile));

        var backupFile = Path.Combine(_root, "sauvegarde4.lumorabackup");
        var result = LumoraBackup.Export(backupFile, "phrase-secrete", source);
        Assert.True(result.VaultIncluded);
        Assert.False(result.VaultSkippedNotPortable);

        var target = LumoraProfilePaths.FromDirectory(Path.Combine(_root, "cible4"));
        LumoraBackup.Import(backupFile, "phrase-secrete", target);

        var restored = new VaultStore(target.VaultFile);
        Assert.True(restored.Unlock("mot-de-passe-coffre"));
        Assert.Single(restored.ListCredentials());
        Assert.Equal("alice", restored.ListCredentials()[0].Username);
    }

    [Fact]
    public void Export_ignore_le_coffre_non_portable_mode_dpapi()
    {
        var sourceDir = Path.Combine(_root, "source5");
        var source = LumoraProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);

        var vault = new VaultStore(source.VaultFile);
        vault.Upsert("https://exemple.fr", "alice", "s3cret"); // pas de mot de passe maître = mode DPAPI
        Assert.False(VaultStore.IsPortable(source.VaultFile));

        var backupFile = Path.Combine(_root, "sauvegarde5.lumorabackup");
        var result = LumoraBackup.Export(backupFile, "phrase-secrete", source);
        Assert.False(result.VaultIncluded);
        Assert.True(result.VaultSkippedNotPortable);
    }
}
