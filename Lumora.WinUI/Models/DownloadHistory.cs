using System.Text.Json;

namespace Lumora.WinUI;

internal sealed record DownloadHistoryEntry(
    string Id,
    string FileName,
    string SourceUri,
    string SourceDomain,
    string LocalPath,
    long ReceivedBytes,
    long TotalBytes,
    string State,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt)
{
    public const string InProgress = "in-progress";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Canceled = "canceled";

    public bool IsCompleted => State == Completed;
    public bool IsFailed => State is Failed or Canceled;
    public bool IsActive => State == InProgress;
    public bool HasKnownSize => TotalBytes > 0;
    public double ProgressPercent => TotalBytes > 0 ? (double)ReceivedBytes / TotalBytes * 100 : 0;

    public string StateLabel => State switch
    {
        Completed => "Termine",
        Failed => "Echec",
        Canceled => "Annule",
        _ => "En cours"
    };

    public static string NewId() => Guid.NewGuid().ToString("N");

    public static string NormalizeState(string? state) => state switch
    {
        Completed => Completed,
        Failed => Failed,
        Canceled => Canceled,
        _ => InProgress
    };

    public static string SourceDomainFor(string? sourceUri)
    {
        if (Uri.TryCreate(sourceUri, UriKind.Absolute, out var uri)
            && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        return string.IsNullOrWhiteSpace(sourceUri) ? "source inconnue" : sourceUri;
    }

    public DownloadHistoryEntry WithProgress(long receivedBytes, long totalBytes) =>
        this with
        {
            ReceivedBytes = Math.Max(0, receivedBytes),
            TotalBytes = Math.Max(0, totalBytes)
        };

    public DownloadHistoryEntry WithState(string state)
    {
        var normalized = NormalizeState(state);
        return this with
        {
            State = normalized,
            CompletedAt = normalized == InProgress ? null : DateTimeOffset.Now
        };
    }
}

internal sealed class DownloadHistoryStore
{
    private const int MaxEntries = 500;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly string _path;
    private readonly List<DownloadHistoryEntry> _entries = new();
    private bool _isGuest;

    public DownloadHistoryStore(string path)
    {
        _path = path;
        Load();
    }

    public void SetGuestMode(bool isGuest)
    {
        _isGuest = isGuest;
        if (isGuest) _entries.Clear();
    }

    public IReadOnlyList<DownloadHistoryEntry> AllEntries() => _entries;

    public void Upsert(DownloadHistoryEntry entry)
    {
        var normalized = entry with { State = DownloadHistoryEntry.NormalizeState(entry.State) };
        _entries.RemoveAll(existing => existing.Id == normalized.Id);
        _entries.Insert(0, normalized);
        while (_entries.Count > MaxEntries)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }

        Save();
    }

    public void Remove(string id)
    {
        _entries.RemoveAll(entry => entry.Id == id);
        Save();
    }

    public void Clear()
    {
        _entries.Clear();
        Save();
    }

    private void Load()
    {
        try
        {
            var json = LumoraFile.TryReadAllText(_path);
            if (json is null) return;
            var loaded = JsonSerializer.Deserialize<List<DownloadHistoryEntry>>(json, JsonOptions);
            if (loaded is null) return;

            _entries.AddRange(loaded
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
                .Select(entry => entry with
                {
                    State = DownloadHistoryEntry.NormalizeState(entry.State),
                    FileName = string.IsNullOrWhiteSpace(entry.FileName)
                        ? Path.GetFileName(entry.LocalPath)
                        : entry.FileName,
                    SourceDomain = string.IsNullOrWhiteSpace(entry.SourceDomain)
                        ? DownloadHistoryEntry.SourceDomainFor(entry.SourceUri)
                        : entry.SourceDomain
                })
                .OrderByDescending(entry => entry.StartedAt)
                .Take(MaxEntries));
        }
        catch
        {
        }
    }

    private void Save()
    {
        if (_isGuest) return;
        try
        {
            LumoraFile.WriteAllText(_path, JsonSerializer.Serialize(_entries, JsonOptions));
        }
        catch
        {
        }
    }
}
