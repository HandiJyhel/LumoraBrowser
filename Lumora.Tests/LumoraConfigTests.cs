using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

[Collection(ProfileDirEnvironmentCollection.Name)]
public sealed class LumoraConfigTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "lumora_config_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void RepairLegacyCustomProfilePath_retrouve_un_dossier_PulseBrowser_renomme_LumoraBrowser()
    {
        var legacyPath = Path.Combine(_root, "Documents", "PulseBrowser", "H.J");
        var repairedPath = Path.Combine(_root, "Documents", "LumoraBrowser", "H.J");
        Directory.CreateDirectory(repairedPath);
        File.WriteAllText(Path.Combine(repairedPath, "profile.pulse"), "profil");

        var repaired = LumoraConfig.RepairLegacyCustomProfilePath(legacyPath);

        Assert.Equal(repairedPath, repaired);
    }

    [Fact]
    public void RepairLegacyCustomProfilePath_ne_change_pas_un_chemin_valide()
    {
        var currentPath = Path.Combine(_root, "Documents", "PulseBrowser", "H.J");
        Directory.CreateDirectory(currentPath);
        File.WriteAllText(Path.Combine(currentPath, "profile.pulse"), "profil");

        var repaired = LumoraConfig.RepairLegacyCustomProfilePath(currentPath);

        Assert.Equal(currentPath, repaired);
    }

    // 2026-08-14 : incident reel corrige (voir MEMORY.md) - un lancement de
    // verification/invite avec LUMORA_PROFILE_DIR positionne ecrivait quand
    // meme dans le vrai %LOCALAPPDATA%\Lumora\config.json partage de la
    // machine (ActiveProfileId ecrase par un profil de test), puisque
    // LumoraConfig ignorait totalement cette variable. Verifie ici que
    // Save()/Load() restent desormais confines au dossier isole.
    [Fact]
    public void Save_puis_Load_restent_confines_au_dossier_isole()
    {
        var previous = Environment.GetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable);
        var isolatedDir = Path.Combine(_root, "isolated-profile-dir");

        try
        {
            Environment.SetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable, isolatedDir);

            var config = new LumoraConfig { ActiveProfileId = "profil-de-test-isole" };
            config.Save();

            var expectedPath = Path.Combine(isolatedDir, "config.json");
            Assert.True(File.Exists(expectedPath),
                $"config.json attendu dans le dossier isole ({expectedPath}), introuvable.");

            var reloaded = LumoraConfig.Load();
            Assert.Equal("profil-de-test-isole", reloaded.ActiveProfileId);
        }
        finally
        {
            Environment.SetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable, previous);
        }
    }

    // Un dossier isole neuf (jamais utilise) ne doit jamais retomber sur un
    // vrai config.json herite (NovaBrowser/PulseBrowser) de la machine - rien
    // a migrer dans un sandbox de test, voir Load().
    [Fact]
    public void Load_sur_dossier_isole_neuf_ne_regarde_jamais_les_chemins_reels_herites()
    {
        var previous = Environment.GetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable);
        var isolatedDir = Path.Combine(_root, "isolated-fresh-dir");

        try
        {
            Environment.SetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable, isolatedDir);

            var config = LumoraConfig.Load();

            Assert.Equal("default", config.ActiveProfileId);
            Assert.Null(config.CustomProfilePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable, previous);
        }
    }
}
