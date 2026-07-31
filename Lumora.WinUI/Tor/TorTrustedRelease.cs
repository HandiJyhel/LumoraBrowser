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
// Source verifiee : tor-expert-bundle-windows-x86_64-15.0.19.tar.gz
// (https://dist.torproject.org/torbrowser/15.0.19/), SHA256 archive
// 6ac067402c7b4a3dc37887ed3754b3914b67fdc220c966190683e9ccf91abf0f.
// Re-verifie le 2026-07-22 (signature GPG "Good signature" confirmee contre
// la cle ci-dessus, gpg --verify local) suite au retrait de la version
// 15.0.18 du miroir officiel (404 constate en usage reel) - dist.torproject.org
// n'archive qu'un nombre limite de versions a la fois.
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
    public const string VersionLabel = "Tor Expert Bundle 15.0.19 (Windows x86_64)";

    public const string Version = "15.0.19";
    public const string ArchiveFileName = "tor-expert-bundle-windows-x86_64-15.0.19.tar.gz";
    public const string ArchiveUrl =
        $"https://dist.torproject.org/torbrowser/{Version}/{ArchiveFileName}";
    public const string ArchiveSha256 =
        "6ac067402c7b4a3dc37887ed3754b3914b67fdc220c966190683e9ccf91abf0f";

    public static readonly IReadOnlyDictionary<string, string> TrustedFileHashes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tor.exe"] = "ec7708e0b43e0e00b1533d11ed3ca244e6f11cb2a7b62d319ad73a7b13123033",
        };
}
