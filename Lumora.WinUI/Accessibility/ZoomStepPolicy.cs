namespace Lumora.WinUI.Accessibility;

// Vision ("Ajustement n2", chantier "Reglages sur-mesure") : pas de zoom
// natif exploitable ici - verifie par la compilation, pas suppose :
// Microsoft.UI.Xaml.Controls.WebView2 (SDK WindowsAppSDK utilise par Lumora)
// n'expose ni ZoomFactor ni ZoomFactorChanged, seulement CoreWebView2 (qui
// lui-meme n'a pas ces membres - ils vivent sur CoreWebView2Controller,
// jamais expose par ce wrapper XAML). "Zoom memorise par site" reutilise
// donc le mecanisme CSS deja existant et deja fiable du Controle de site
// (SiteComfortPolicy.SetZoomPercent/ZoomPercentFor, applique via
// MainWindow.SiteComfort.cs) : cette classe fournit juste la table de
// paliers pour Ctrl+Plus/Ctrl+Moins/Ctrl+0, calquee EXACTEMENT sur les
// valeurs du menu deroulant "Zoom prefere pour ce site" pour que le clavier
// ne puisse jamais retomber sur une valeur que ce menu ne sait pas afficher.
internal static class ZoomStepPolicy
{
    // Reference directement SiteComfortPolicy plutot que de recopier les
    // memes valeurs (nettoyage 2026-09-14, doublon reel trouve en audit) :
    // le commentaire ci-dessus dit deja "calquee EXACTEMENT" sur ce menu -
    // une table recopiee a la main aurait pu diverger silencieusement si
    // SiteComfortPolicy changeait un jour, et le clavier aurait alors pu
    // retomber sur un palier que le menu deroulant ne propose pas.
    public static readonly IReadOnlyList<int> Steps = SiteComfortPolicy.SuggestedZoomPercents;
    public const int DefaultPercent = SiteComfortPolicy.DefaultZoomPercent;

    public static int StepUp(int currentPercent)
    {
        foreach (var step in Steps)
        {
            if (step > currentPercent)
            {
                return step;
            }
        }

        return Steps[^1];
    }

    public static int StepDown(int currentPercent)
    {
        for (var i = Steps.Count - 1; i >= 0; i--)
        {
            if (Steps[i] < currentPercent)
            {
                return Steps[i];
            }
        }

        return Steps[0];
    }
}
