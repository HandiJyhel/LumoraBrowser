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
    public static readonly int[] Steps = [90, 100, 110, 125, 140, 160];
    public const int DefaultPercent = 100;

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
        for (var i = Steps.Length - 1; i >= 0; i--)
        {
            if (Steps[i] < currentPercent)
            {
                return Steps[i];
            }
        }

        return Steps[0];
    }
}
