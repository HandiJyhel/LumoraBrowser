namespace Lumora.WinUI;

// Une application web Lumora : un site épinglé, ouvert dans une fenêtre dédiée
// sans onglets ni barre d'adresse, partageant le même profil (cookies/sessions).
internal sealed class LumoraWebApp
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string RootDomain { get; set; } = string.Empty;
    // Nom de fichier .ico dans WebAppIconsDir, ou null (fallback icône Lumora).
    public string? IconFile { get; set; }
    public bool AlwaysOnTop { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    // Nom de fichier .lnk (sans dossier) dans le Menu Démarrer, pour pouvoir
    // supprimer/recréer le bon raccourci lors d'un renommage ou d'une suppression.
    public string? ShortcutFileName { get; set; }
    public bool HasDesktopShortcut { get; set; }
}
