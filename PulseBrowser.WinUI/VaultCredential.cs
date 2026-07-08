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

public sealed class VaultCredential
{
    [JsonPropertyName("origin")] public string Origin { get; init; } = string.Empty;
    [JsonPropertyName("username")] public string Username { get; init; } = string.Empty;
    [JsonPropertyName("password")] public string Password { get; init; } = string.Empty;
    // Nom personnalisé optionnel (édité par l'utilisateur), affiché en titre à la place de l'URL.
    [JsonPropertyName("label")] public string Label { get; init; } = string.Empty;
    // URL de la page où le mot de passe a été saisi, pour y retourner depuis le coffre
    // (comme les grands gestionnaires). Vide sur les anciennes entrées → repli sur Origin.
    [JsonPropertyName("login_url")] public string LoginUrl { get; init; } = string.Empty;
    [JsonPropertyName("created_at")] public long CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public long UpdatedAt { get; init; }
}

