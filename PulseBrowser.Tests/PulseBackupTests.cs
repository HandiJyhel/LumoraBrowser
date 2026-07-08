using System.Security.Cryptography;
using PulseBrowser.WinUI;
using Xunit;

namespace PulseBrowser.Tests;

// Vérifie le round-trip de la sauvegarde chiffrée (.pulsebackup v2 : Argon2id + AES-GCM)
// et le rejet d'un mauvais mot de passe.
public sealed class PulseBackupTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "pulse_bak_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Export_puis_Import_restitue_les_donnees_de_navigation()
    {
        var sourceDir = Path.Combine(_root, "source");
        var source = PulseProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);
        PulseFile.WriteAllText(source.BookmarksFile, "signet\tperso\thttps://exemple.fr");

        var backupFile = Path.Combine(_root, "sauvegarde.pulsebackup");
        PulseBackup.Export(backupFile, "phrase-secrete", source);
        Assert.True(File.Exists(backupFile));

        var targetDir = Path.Combine(_root, "cible");
        var target = PulseProfilePaths.FromDirectory(targetDir);
        PulseBackup.Import(backupFile, "phrase-secrete", target);

        Assert.Equal(
            PulseFile.ReadAllText(source.BookmarksFile),
            PulseFile.ReadAllText(target.BookmarksFile));
    }

    [Fact]
    public void Import_avec_mauvais_mot_de_passe_leve_une_exception()
    {
        var sourceDir = Path.Combine(_root, "source2");
        var source = PulseProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);
        PulseFile.WriteAllText(source.BookmarksFile, "signet\tperso\thttps://exemple.fr");

        var backupFile = Path.Combine(_root, "sauvegarde2.pulsebackup");
        PulseBackup.Export(backupFile, "bon-mot-de-passe", source);

        var target = PulseProfilePaths.FromDirectory(Path.Combine(_root, "cible2"));
        Assert.Throws<CryptographicException>(
            () => PulseBackup.Import(backupFile, "mauvais", target));
    }
}
