namespace PulseBrowser.WinUI;

// Nom de fichier .lnk pour un raccourci d'application web — doit rester un nom
// de fichier Windows valide même si le titre du site contient des caractères
// interdits (ex. "Site : édition / test?"). Aucune dépendance UI.
internal static class ShortcutNaming
{
    public static string SanitizeFileName(string title, string id)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(title.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "Application Pulse";
        if (cleaned.Length > 50) cleaned = cleaned[..50];
        return $"{cleaned} - {id[..Math.Min(8, id.Length)]}";
    }
}
