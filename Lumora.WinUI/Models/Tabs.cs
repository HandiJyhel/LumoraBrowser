using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;

namespace Lumora.WinUI;

internal sealed record BrowserTabState(int Id, string Title, string Address)
{
    public string Title { get; set; } = Title;
    public string Address { get; set; } = Address;
    public string IconPath { get; set; } = string.Empty;
    public string IconUri => string.IsNullOrWhiteSpace(IconPath) ? string.Empty : new Uri(IconPath).AbsoluteUri;
    public int? GroupId { get; set; }
    public bool Pinned { get; set; }

    // Un WebView2 par onglet : chaque onglet garde son moteur (créé paresseusement
    // à la première activation) et l'adresse à charger dès que le moteur est prêt.
    public Microsoft.UI.Xaml.Controls.WebView2? View { get; set; }
    public string? PendingAddress { get; set; }
}


internal sealed record SavedTab(string Title, string Address, string IconPath, int? GroupId = null, bool Pinned = false);

internal sealed record TabGroup(int Id, string Name, int ColorIndex)
{
    public string Name { get; set; } = Name;
}

internal sealed class TabSession
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public List<SavedTab> Tabs { get; set; } = new();
    public List<TabGroup> Groups { get; set; } = new();
    public int ActiveIndex { get; set; }

    public static TabSession Load(string path, string? legacyPath = null)
    {
        try
        {
            if (!File.Exists(path))
            {
                // Migration depuis tabs.json non chiffré
                if (legacyPath is not null && File.Exists(legacyPath))
                {
                    var migrated = JsonSerializer.Deserialize<TabSession>(File.ReadAllText(legacyPath), JsonOpts) ?? new TabSession();
                    migrated.Save(path);
                    try { File.Delete(legacyPath); } catch { }
                    return migrated;
                }

                return new TabSession();
            }

            var json = LumoraFile.TryReadAllText(path);
            if (json is null) return new TabSession();
            return JsonSerializer.Deserialize<TabSession>(json, JsonOpts) ?? new TabSession();
        }
        catch
        {
            return new TabSession();
        }
    }

    public void Save(string path)
    {
        try
        {
            LumoraFile.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { }
    }
}

