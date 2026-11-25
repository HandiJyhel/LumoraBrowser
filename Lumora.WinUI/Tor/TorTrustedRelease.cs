namespace Lumora.WinUI.Tor;

// Parametres de confiance pour le moteur Tor (depuis 1.0.1 : plus AUCUNE
// version de Tor n'est epinglee en dur).
//
// Historique : jusqu'a la 1.0.0, ce fichier epinglait une version precise de
// Tor (15.0.18, puis 15.0.19, puis 15.0.20) avec ses empreintes SHA256.
// dist.torproject.org n'archive qu'un nombre limite de versions : chaque
// retrait d'une version epinglee rendait l'installation de Tor impossible
// (404) jusqu'a une nouvelle version de Lumora - arrive trois fois (2026-07-22,
// 2026-08-21, puis 15.0.20 retiree au profit de la branche 16).
//
// Nouveau modele : le point de confiance n'est plus une version mais la CLE
// DE SIGNATURE des developpeurs de Tor. TorEngineProvider lit la derniere
// version stable sur le miroir officiel, telecharge l'archive et sa signature
// OpenPGP detachee (.asc), puis n'installe que si la signature est valide
// pour une sous-cle liee a la cle principale epinglee ci-dessous. Le SHA256 de
// l'archive et de tor.exe calcules a l'installation sont ensuite consignes
// dans un recu local (engine-receipt.json) que TorProcessManager reverifie a
// chaque lancement.
internal static class TorTrustedRelease
{
    // Repertoire des releases Tor Browser (dont le "Tor Expert Bundle", seul
    // artefact utilise : tor.exe sans le navigateur).
    public const string MirrorBaseUrl = "https://dist.torproject.org/torbrowser/";

    // Cle principale "Tor Browser Developers (signing key)" - SEUL point de
    // confiance du dispositif. Verifiee a la main le 2026-11-25 : la signature
    // OpenPGP de l'archive tor-expert-bundle-windows-x86_64-16.0.3.tar.gz est
    // "Good signature" (gpg --verify) pour la sous-cle 022D A248 432D 2A0E
    // 0F54 E65E 316C 1FAC D62D 07D9, liee a cette cle principale.
    public const string PinnedSigningKeyFingerprint = "EF6E286DDA85EA2A4BA7DE684E2C6E8793298290";

    // Source d'un bloc de cle a jour quand une release est signee par une
    // sous-cle absente du bloc embarque (rotation periodique de la sous-cle
    // de signature). Le contenu recupere n'est jamais cru sur parole : il
    // repasse par les memes verifications que le bloc embarque.
    public const string SigningKeyRefreshUrl =
        "https://keys.openpgp.org/vks/v1/by-fingerprint/" + PinnedSigningKeyFingerprint;

    // Plancher anti-retour-arriere : meme signee correctement, une version
    // plus ancienne que celle-ci n'est jamais installee (un miroir compromis
    // pourrait rejouer une ancienne archive legitime dont une faille est
    // connue). Remonter cette valeur quand une version est verifiee a la main.
    public static readonly Version MinimumVersion = new(16, 0, 3);

    // Compatibilite avec les installations de la 1.0.0 : leur tor.exe (15.0.20)
    // n'a pas de recu local. Son empreinte, verifiee a la main (signature GPG
    // + SHA256, 2026-08-21), reste acceptee pour que Tor continue de demarrer
    // jusqu'a la premiere mise a jour automatique, qui installera un recu.
    public const string LegacyVersion = "15.0.20";

    public static readonly IReadOnlyDictionary<string, string> LegacyTrustedFileHashes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tor.exe"] = "ea61ba0ed5b89d0622d2894b2a86f5ff34ce9b48e6e40d64341e7c0c7ee03e08",
        };

    public static string ArchiveFileName(string version) =>
        $"tor-expert-bundle-windows-x86_64-{version}.tar.gz";

    public static string ArchiveUrl(string version) =>
        $"{MirrorBaseUrl}{version}/{ArchiveFileName(version)}";

    public static string SignatureUrl(string version) => ArchiveUrl(version) + ".asc";
}
