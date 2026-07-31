namespace Lumora.WinUI;

// Logique pure : construit les identifiants Windows (AppUserModelID) utilisés
// à la fois sur le raccourci .lnk (ShellShortcut) et au démarrage du process
// (SetCurrentProcessExplicitAppUserModelID dans App.xaml.cs). Les deux DOIVENT
// rester identiques pour qu'un épinglage à la barre des tâches lance bien la
// bonne fenêtre avec la bonne icône.
internal static class WebAppIdentity
{
    public const string BrowserAppUserModelId = "Lumora.Browser";

    public static string AppUserModelId(string appId) => $"Lumora.WebApp.{appId}";
}
