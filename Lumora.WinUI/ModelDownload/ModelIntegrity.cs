using System.Security.Cryptography;

namespace Lumora.WinUI.ModelDownload;

// Un fichier de modele attendu, avec son empreinte SHA256 de reference -
// partage entre les trois catalogues de telechargement (SearchAssist,
// Traduction, Recherche semantique) pour eviter de redefinir la meme forme
// trois fois.
internal sealed record ModelFile(string RelativePath, string Sha256);

// Verification d'integrite des modeles telecharges (SearchAssist, Traduction,
// Recherche semantique) : aucun des trois telechargements ne verifiait quoi
// que ce soit au-dela du HTTPS + code 200 avant le 2026-07-20, alors que
// AGENTS.md pose explicitement le principe "un binaire execute localement,
// son integrite est verifiee (empreinte cryptographique) avant toute
// execution." Empreintes SHA256 obtenues en telechargeant reellement chaque
// fichier depuis Hugging Face au moment de l'integration (pas une source
// tierce) : ce n'est pas une preuve que le depot Hugging Face lui-meme est
// de confiance, mais ca detecte toute alteration ULTERIEURE (CDN compromis,
// interception, corruption reseau) entre ce moment de reference et un
// telechargement futur - exactement le risque que ce garde-fou vise.
internal static class ModelIntegrity
{
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // A appeler sur le fichier temporaire ("*.part"), AVANT de le deplacer
    // vers sa destination finale : un fichier dont l'empreinte ne correspond
    // pas n'a jamais l'occasion d'exister sous son nom final. Supprime le
    // fichier invalide plutot que de le laisser trainer a moitie verifie.
    public static async Task VerifyOrDeleteAsync(string filePath, string expectedSha256Hex, CancellationToken cancellationToken = default)
    {
        var actual = await ComputeSha256Async(filePath, cancellationToken);
        if (!string.Equals(actual, expectedSha256Hex, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(filePath); } catch { }
            throw new InvalidDataException(
                $"Integrite invalide pour {Path.GetFileName(filePath)} : empreinte attendue {expectedSha256Hex}, obtenue {actual}. " +
                "Fichier supprime par precaution - le telechargement sera retente au prochain usage.");
        }
    }
}
