using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

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
}
