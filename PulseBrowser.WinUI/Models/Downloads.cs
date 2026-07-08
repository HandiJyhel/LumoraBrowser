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

internal sealed class DownloadEntry
{
    private long _totalBytes;
    private long _receivedBytes;
    private string _state = "En cours";

    public string FileName { get; }
    public string SourceDomain { get; }
    public string LocalPath { get; }
    public string State => _state;
    public long TotalBytes => _totalBytes;
    public long ReceivedBytes => _receivedBytes;
    public bool IsCompleted => _state == "Termine";
    public bool IsFailed => _state is "Echec" or "Annule";
    public double ProgressPercent => _totalBytes > 0 ? (double)_receivedBytes / _totalBytes * 100 : 0;

    public event Action? OnChanged;

    public DownloadEntry(CoreWebView2DownloadOperation operation)
    {
        LocalPath = operation.ResultFilePath;
        FileName = Path.GetFileName(LocalPath);
        SourceDomain = Uri.TryCreate(operation.Uri, UriKind.Absolute, out var uri)
            ? uri.Host
            : operation.Uri;
        _totalBytes = operation.TotalBytesToReceive > 0 ? operation.TotalBytesToReceive : 0;
        _receivedBytes = operation.BytesReceived;

        operation.StateChanged += (s, _) =>
        {
            _state = s.State switch
            {
                CoreWebView2DownloadState.Completed => "Termine",
                CoreWebView2DownloadState.Interrupted => "Echec",
                _ => "En cours"
            };
            OnChanged?.Invoke();
        };
        operation.BytesReceivedChanged += (s, _) =>
        {
            if (s.TotalBytesToReceive > 0) _totalBytes = s.TotalBytesToReceive;
            _receivedBytes = s.BytesReceived;
            OnChanged?.Invoke();
        };
    }
}

// ── Coffre local : modèle d'identifiant du coffre vault.pulse ────────────────

