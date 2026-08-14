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

    // 2026-08-14 : incident reel corrige (voir MEMORY.md) - ForProfileId
    // ignorait totalement LUMORA_PROFILE_DIR (contrairement a Default()),
    // un profil cree via le chemin par defaut de l'assistant atterrissait
    // reellement dans le vrai %LOCALAPPDATA%\Lumora\profiles de la machine.
    [Fact]
    public void ForProfileId_sous_isolation_ignore_l_id_et_retourne_le_dossier_isole_a_plat()
    {
        var previous = Environment.GetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable);
        var dir = Path.Combine(Path.GetTempPath(), "LumoraTests", Guid.NewGuid().ToString("N"), "profile");

        try
        {
            Environment.SetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable, dir);

            var forNamedId = LumoraProfilePaths.ForProfileId("un-nom-quelconque");
            var forDefault = LumoraProfilePaths.Default();

            // Meme convention que Default() : le dossier isole EST le
            // profil, a plat - peu importe l'id demande, jamais un
            // sous-dossier dans le vrai ProfilesRoot() de la machine.
            Assert.Equal(Path.GetFullPath(dir), forNamedId.ProfileDir);
            Assert.Equal(forDefault.ProfileDir, forNamedId.ProfileDir);
            Assert.True(LumoraProfilePaths.HasIsolatedProfileDirOverride());
        }
        finally
        {
            Environment.SetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable, previous);
        }
    }

    [Fact]
    public void HasIsolatedProfileDirOverride_faux_par_defaut()
    {
        var previous = Environment.GetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable, null);
            Assert.False(LumoraProfilePaths.HasIsolatedProfileDirOverride());
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
