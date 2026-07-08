using System.Text.Json;

namespace PulseBrowser.WinUI;

internal sealed class PulseConfig
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Ce fichier est TOUJOURS dans %LOCALAPPDATA%\PulseBrowser\ — jamais dans le profil custom,
    // c'est lui qui indique où chercher le profil.
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PulseBrowser", "config.json");

    public string? CustomProfilePath { get; set; }
    public string ActiveProfileId { get; set; } = "default";

    public static PulseConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<PulseConfig>(json) ?? new PulseConfig();
            }
        }
        catch { }
        return new PulseConfig();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(ConfigPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
