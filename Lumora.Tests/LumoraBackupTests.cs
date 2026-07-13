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
}
