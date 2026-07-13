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

public sealed record PasskeyEntry(string Origin, DateTimeOffset CreatedAt, DateTimeOffset LastUsedAt)
{
    public string Serialize() =>
        $"{Uri.EscapeDataString(Origin)}\t{CreatedAt.ToUnixTimeSeconds()}\t{LastUsedAt.ToUnixTimeSeconds()}";

    public static PasskeyEntry? TryParse(string line)
    {
        var parts = line.Split('\t');
        if (parts.Length < 3) return null;
        if (!long.TryParse(parts[1], out var created) || !long.TryParse(parts[2], out var used)) return null;
        return new PasskeyEntry(Uri.UnescapeDataString(parts[0]),
            DateTimeOffset.FromUnixTimeSeconds(created),
            DateTimeOffset.FromUnixTimeSeconds(used));
    }
}

