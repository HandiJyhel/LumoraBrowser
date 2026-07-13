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

internal sealed class DownloadEntry
{
    private long _totalBytes;
    private long _receivedBytes;
    private string _state = DownloadHistoryEntry.InProgress;

    public string Id { get; } = DownloadHistoryEntry.NewId();
    public string FileName { get; }
    public string SourceUri { get; }
    public string SourceDomain { get; }
    public string LocalPath { get; }
    public string State => ToHistoryEntry().StateLabel;
    public long TotalBytes => _totalBytes;
    public long ReceivedBytes => _receivedBytes;
    public bool IsCompleted => _state == DownloadHistoryEntry.Completed;
    public bool IsFailed => _state is DownloadHistoryEntry.Failed or DownloadHistoryEntry.Canceled;
    public double ProgressPercent => _totalBytes > 0 ? (double)_receivedBytes / _totalBytes * 100 : 0;
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.Now;
    public DateTimeOffset? CompletedAt { get; private set; }

    public event Action? OnChanged;

    public DownloadEntry(CoreWebView2DownloadOperation operation)
    {
        LocalPath = operation.ResultFilePath;
        FileName = Path.GetFileName(LocalPath);
        SourceUri = operation.Uri;
        SourceDomain = DownloadHistoryEntry.SourceDomainFor(operation.Uri);
        _totalBytes = operation.TotalBytesToReceive > 0 ? operation.TotalBytesToReceive : 0;
        _receivedBytes = operation.BytesReceived;

        operation.StateChanged += (s, _) =>
        {
            _state = s.State switch
            {
                CoreWebView2DownloadState.Completed => DownloadHistoryEntry.Completed,
                CoreWebView2DownloadState.Interrupted => DownloadHistoryEntry.Failed,
                _ => DownloadHistoryEntry.InProgress
            };
            CompletedAt = _state == DownloadHistoryEntry.InProgress ? null : DateTimeOffset.Now;
            OnChanged?.Invoke();
        };
        operation.BytesReceivedChanged += (s, _) =>
        {
            if (s.TotalBytesToReceive > 0) _totalBytes = s.TotalBytesToReceive;
            _receivedBytes = s.BytesReceived;
            OnChanged?.Invoke();
        };
    }

    public DownloadHistoryEntry ToHistoryEntry() =>
        new(
            Id,
            FileName,
            SourceUri,
            SourceDomain,
            LocalPath,
            _receivedBytes,
            _totalBytes,
            _state,
            StartedAt,
            CompletedAt);
}

// ── Coffre local : modèle d'identifiant du coffre vault.lumora ────────────────
