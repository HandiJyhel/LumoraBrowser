using Microsoft.UI.Xaml;

namespace Lumora.WinUI;

// Regroupe ce qui doit être garanti à chaque fois que l'écran de connexion
// (LoginOverlay) s'affiche - premier lancement (InitializeLoginOverlayAsync),
// changement/création de profil en cours de session (ShowProfileOverlay) et
// verrouillage (LockSessionNow). Trouvé en usage reel le 2026-07-29 : ces
// trois points d'entree dupliquaient le meme bloc de 3 lignes sans jamais
// arreter les navigations en cours, donc un chargement deja lance (ou un
// onglet en arriere-plan) continuait a charger - et le bloqueur de pub a se
// declencher - meme ecran verrouille affiche par-dessus.
public sealed partial class MainWindow
{
    private void ShowLoginOverlayChrome()
    {
        StopAllTabNavigations();
        BrowserHost.IsHitTestVisible = false;
        LoginOverlay.Visibility = Visibility.Visible;
        LoginOverlay.Focus(FocusState.Programmatic);
    }

    private void StopAllTabNavigations()
    {
        foreach (var tab in _tabs)
        {
            tab.View?.CoreWebView2?.Stop();
        }
    }
}
