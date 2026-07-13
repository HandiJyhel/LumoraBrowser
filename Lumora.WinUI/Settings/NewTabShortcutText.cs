namespace Lumora.WinUI.Settings;

// Sérialisation texte ("Titre | URL" par ligne) des raccourcis nouvel onglet,
// affichée/éditée dans le panneau Paramètres. Travaille sur des tuples plutôt
// que sur le modèle UiSettings.NewTabShortcut, qui dépend de WinUI et ne peut
// pas être compilé dans le projet de test. Aucune dépendance UI ici.
internal static class NewTabShortcutText
{
    private const int MaxShortcuts = 12;

    public static string ToText(IEnumerable<(string Title, string Url)> shortcuts) =>
        string.Join(Environment.NewLine, shortcuts
            .Where(shortcut => !string.IsNullOrWhiteSpace(shortcut.Title) && !string.IsNullOrWhiteSpace(shortcut.Url))
            .Select(shortcut => $"{shortcut.Title.Trim()} | {shortcut.Url.Trim()}"));

    public static List<(string Title, string Url)> Parse(string text)
    {
        var shortcuts = new List<(string, string)>();
        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var parts = line.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                continue;

            shortcuts.Add((parts[0], parts[1]));
            if (shortcuts.Count >= MaxShortcuts)
                break;
        }

        return shortcuts;
    }
}
