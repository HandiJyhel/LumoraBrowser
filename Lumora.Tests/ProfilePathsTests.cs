using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public class ProfilePathsTests
{
    [Fact]
    public void Default_utilise_le_profil_isole_depuis_l_environnement()
    {
        var previous = Environment.GetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable);
        var dir = Path.Combine(Path.GetTempPath(), "LumoraTests", Guid.NewGuid().ToString("N"), "profile");

        try
        {
            Environment.SetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable, dir);

            var paths = LumoraProfilePaths.Default();

            Assert.Equal(Path.GetFullPath(dir), paths.ProfileDir);
            Assert.StartsWith(paths.ProfileDir, paths.BrowserDataDir);
        }
        finally
        {
            Environment.SetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable, previous);
        }
    }

    [Fact]
    public void FromDirectory_reutilise_les_fichiers_pulse_quand_lumora_absent()
    {
        var root = Path.Combine(Path.GetTempPath(), "LumoraTests", Guid.NewGuid().ToString("N"));
        try
        {
            var profileDir = Path.Combine(root, "profile");
            var navigationDir = Path.Combine(profileDir, "navigation");
            Directory.CreateDirectory(navigationDir);
            File.WriteAllText(Path.Combine(profileDir, "profile.pulse"), "profil");
            File.WriteAllText(Path.Combine(profileDir, "vault.pulse"), "coffre");
            File.WriteAllText(Path.Combine(navigationDir, "bookmarks.pulse"), "favoris");
            File.WriteAllText(Path.Combine(navigationDir, "bookmarks.nova"), "favoris-vide");

            var paths = LumoraProfilePaths.FromDirectory(profileDir);

            Assert.EndsWith("profile.pulse", paths.ProfileFile);
            Assert.EndsWith("vault.pulse", paths.VaultFile);
            Assert.EndsWith("bookmarks.pulse", paths.BookmarksFile);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FromDirectory_garde_lumora_prioritaire_si_present()
    {
        var root = Path.Combine(Path.GetTempPath(), "LumoraTests", Guid.NewGuid().ToString("N"));
        try
        {
            var profileDir = Path.Combine(root, "profile");
            Directory.CreateDirectory(profileDir);
            File.WriteAllText(Path.Combine(profileDir, "profile.lumora"), "profil-lumora");
            File.WriteAllText(Path.Combine(profileDir, "profile.pulse"), "profil-pulse");

            var paths = LumoraProfilePaths.FromDirectory(profileDir);

            Assert.EndsWith("profile.lumora", paths.ProfileFile);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
