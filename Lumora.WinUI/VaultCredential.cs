using System.Text.Json.Serialization;

namespace Lumora.WinUI;

public sealed class VaultCredential
{
    // Identité stable, indépendante de (Origin, Username), pour supporter plusieurs
    // comptes sur un même site (ex. deux comptes Google) sans ambiguïté de Renommer/Supprimer.
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
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
    // Authentification à double facteur (TOTP, RFC 6238) optionnelle, associée à
    // cet identifiant. Vide = pas de TOTP configuré. Chiffré comme le reste du
    // coffre : ne fait pas exception au stockage vault.lumora.
    [JsonPropertyName("totp_secret")] public string TotpSecret { get; init; } = string.Empty;
    [JsonPropertyName("totp_digits")] public int TotpDigits { get; init; } = 6;
    [JsonPropertyName("totp_period")] public int TotpPeriod { get; init; } = 30;
    // "SHA1" (défaut, quasi tous les services), "SHA256" ou "SHA512" — voir
    // TotpService.ParseAlgorithmName/AlgorithmName pour le round-trip. Absent
    // sur les anciennes entrées → SHA1, valeur déjà utilisée avant ce champ.
    [JsonPropertyName("totp_algorithm")] public string TotpAlgorithm { get; init; } = "SHA1";
}

