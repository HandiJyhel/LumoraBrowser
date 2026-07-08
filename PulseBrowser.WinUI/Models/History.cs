using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;

namespace PulseBrowser.WinUI;

public sealed record HistoryEntry(string Url, string Title, DateTimeOffset VisitedAt);

public sealed record HistoryListItem(
    string IconGlyph,
    string IconImageUri,
    Visibility IconImageVisibility,
    Visibility IconGlyphVisibility,
    string Title,
    string Domain,
    string TimeDisplay,
    HistoryEntry Entry);

internal sealed class HistoryStore
{
    private const int MaxEntries = 2000;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
    private readonly string _historyFile;
    private readonly string? _legacyHistoryFile;
    private readonly List<HistoryEntry> _entries = new();
    private bool _isGuest;

    public void SetGuestMode(bool isGuest)
    {
        _isGuest = isGuest;
        if (isGuest) _entries.Clear(); // vider sans toucher le disque
    }

    public HistoryStore(string historyFile, string? legacyHistoryFile = null)
    {
        _historyFile       = historyFile;
        _legacyHistoryFile = legacyHistoryFile;
        Load();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_historyFile))
            {
                // Migration depuis history.json non chiffré
                if (_legacyHistoryFile is not null && File.Exists(_legacyHistoryFile))
                {
                    var legacyJson = File.ReadAllText(_legacyHistoryFile, Encoding.UTF8);
                    var legacyData = JsonSerializer.Deserialize<List<HistoryEntry>>(legacyJson, JsonOpts);
                    if (legacyData is not null) _entries.AddRange(legacyData);
                    Save(); // réécrit dans le nouveau format chiffré
                    try { File.Delete(_legacyHistoryFile); } catch { }
                }
                return;
            }

            var json = PulseFile.TryReadAllText(_historyFile);
            if (json is null) return;
            var loaded = JsonSerializer.Deserialize<List<HistoryEntry>>(json, JsonOpts);
            if (loaded is not null) _entries.AddRange(loaded);
        }
        catch { }
    }

    public void Add(string url, string title)
    {
        if (!BookmarkStore.IsWebUrl(url))
        {
            return;
        }

        _entries.Insert(0, new HistoryEntry(
            url,
            string.IsNullOrWhiteSpace(title) ? url : title,
            DateTimeOffset.Now));
        while (_entries.Count > MaxEntries)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }

        Save();
    }

    public void Remove(HistoryEntry entry)
    {
        _entries.Remove(entry);
        Save();
    }

    public IReadOnlyList<HistoryEntry> AllEntries() => _entries;

    public IEnumerable<HistoryEntry> Search(string query)
    {
        var q = query.Trim().ToLowerInvariant();
        return _entries.Where(e =>
            e.Title.ToLowerInvariant().Contains(q) ||
            e.Url.ToLowerInvariant().Contains(q));
    }

    public void Clear()
    {
        _entries.Clear();
        Save();
    }

    private void Save()
    {
        if (_isGuest) return;
        try
        {
            PulseFile.WriteAllText(_historyFile, JsonSerializer.Serialize(_entries, JsonOpts));
        }
        catch { }
    }
}

