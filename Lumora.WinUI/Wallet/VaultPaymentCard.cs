using System.Text.Json.Serialization;

namespace Lumora.WinUI;

// Moyen de paiement du portefeuille local. Stocké UNIQUEMENT dans vault.lumora
// (même chiffrement que les identifiants), jamais dans Chromium/WebView2.
// Le cryptogramme (CVV) n'est JAMAIS stocké, par choix de sécurité : l'utilisateur
// le saisit lui-même au moment du paiement.
public sealed class VaultPaymentCard
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    // Nom donné par l'utilisateur (ex. "Carte perso", "CB pro").
    [JsonPropertyName("label")] public string Label { get; init; } = string.Empty;
    [JsonPropertyName("holder")] public string Holder { get; init; } = string.Empty;
    // Numéro complet, chiffres uniquement (normalisé à l'enregistrement).
    [JsonPropertyName("number")] public string Number { get; init; } = string.Empty;
    [JsonPropertyName("exp_month")] public int ExpMonth { get; init; }
    [JsonPropertyName("exp_year")] public int ExpYear { get; init; }
    [JsonPropertyName("note")] public string Note { get; init; } = string.Empty;
    [JsonPropertyName("created_at")] public long CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public long UpdatedAt { get; init; }
}
