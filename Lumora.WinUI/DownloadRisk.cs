namespace Lumora.WinUI;

// ── Fichiers téléchargés capables d'exécuter du code ─────────────────────────
// Classe PURE, compilée aussi dans Lumora.Tests. Audit de sécurité du
// 2026-09-24 : le bouton « Ouvrir » d'un téléchargement lançait directement le
// fichier (ShellExecute). Windows affiche déjà son propre avertissement pour
// un fichier venu d'Internet, mais pas pour tous les types (scripts, raccourcis,
// fichiers d'aide compilés...) et pas de façon constante. Lumora demande donc
// une confirmation explicite pour tout type capable d'exécuter du code.
internal static class DownloadRisk
{
    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Programmes et installeurs
        ".exe", ".com", ".scr", ".pif", ".msi", ".msix", ".msixbundle", ".appx", ".appxbundle", ".msp", ".cpl",
        // Scripts
        ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".hta", ".py", ".pyw",
        // Raccourcis, liens et fichiers qui en déclenchent d'autres
        ".lnk", ".url", ".scf", ".reg", ".inf", ".chm", ".application", ".appref-ms", ".jar", ".library-ms",
        ".settingcontent-ms", ".iso", ".img", ".vhd", ".vhdx",
        // Ajouts relecture 2026-09-24 : consoles MMC (technique GrimResource),
        // installeurs d'applis, diagnostics, compléments Excel, recherches et
        // connecteurs Windows piégeables, Java Web Start, scripts VB/WSH
        // restants, pages web archivées, connexions Bureau à distance.
        ".msc", ".appinstaller", ".diagcab", ".xll", ".search-ms", ".searchconnector-ms", ".jnlp",
        ".ps1xml", ".psc1", ".psd1", ".vb", ".ws", ".mht", ".mhtml", ".rdp", ".gadget", ".msh", ".mshxml",
    };

    public static bool IsDangerous(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Points et espaces finaux ignorés par Windows ("setup.exe." s'exécute
        // comme "setup.exe").
        var trimmed = path.TrimEnd('.', ' ');
        return DangerousExtensions.Contains(Path.GetExtension(trimmed));
    }
}
