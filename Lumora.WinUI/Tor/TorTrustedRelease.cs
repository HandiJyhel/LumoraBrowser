namespace Lumora.WinUI.Tor;

// Liste des fichiers autorises a etre executes depuis <profil>/tor/, avec leur
// empreinte SHA256 epinglee en dur. Aucune valeur ici n'est inventee : chaque
// hash a ete calcule localement sur l'archive officielle telechargee depuis
// dist.torproject.org, apres verification de la signature GPG detachee du
// fichier de sommes de controle (sha256sums-signed-build.txt.asc) par la cle
// "Tor Browser Developers (signing key)" (empreinte principale
// EF6E 286D DA85 EA2A 4BA7 DE68 4E2C 6E87 9329 8290, sous-cle de signature
// CAAE 408A EBE2 288E 96FC 5D5E 1574 32CF 78A6 5729) et comparaison du SHA256
// de l'archive avec l'entree correspondante de ce fichier signe.
//
// Source verifiee : tor-expert-bundle-windows-x86_64-15.0.20.tar.gz
// (https://dist.torproject.org/torbrowser/15.0.20/), SHA256 archive
// d59bff934e3ad876e1623e24ae60c19aeea56f50178093b9f86fba230639f949.
// Re-verifie le 2026-08-21 (signature GPG "Good signature" confirmee contre
// la cle ci-dessus, gpg --verify local) suite au retrait de la version
// 15.0.19 du miroir officiel (404 constate en usage reel) - dist.torproject.org
// n'archive qu'un nombre limite de versions a la fois (15.0.18 avait deja ete
// retiree de la meme facon le 2026-07-22).
//
// Ce bundle Windows ne contient aucune DLL separee (tor.exe est le seul
// executable utilise par TorProcessManager ; tor-gencert.exe et les
// transports enfichables ne sont pas references par le code actuel et ne
// sont donc pas epingles ici).
//
// ArchiveUrl/ArchiveSha256 sont utilises par TorEngineProvider pour
// telecharger et verifier l'archive avant extraction. Cette liste correspond
// a une version precise et ne se met pas a jour seule : une nouvelle version
// de Tor demande de refaire cette verification (signature GPG + SHA256) a la
// main et de mettre a jour les valeurs ci-dessous - et le retrait d'une
// ancienne version du miroir (comme pour 15.0.18) demande la meme chose.
internal static class TorTrustedRelease
{
    public const string VersionLabel = "Tor Expert Bundle 15.0.20 (Windows x86_64)";

    public const string Version = "15.0.20";
    public const string ArchiveFileName = "tor-expert-bundle-windows-x86_64-15.0.20.tar.gz";
    public const string ArchiveUrl =
        $"https://dist.torproject.org/torbrowser/{Version}/{ArchiveFileName}";
    public const string ArchiveSha256 =
        "d59bff934e3ad876e1623e24ae60c19aeea56f50178093b9f86fba230639f949";

    public static readonly IReadOnlyDictionary<string, string> TrustedFileHashes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tor.exe"] = "ea61ba0ed5b89d0622d2894b2a86f5ff34ce9b48e6e40d64341e7c0c7ee03e08",
        };
}
