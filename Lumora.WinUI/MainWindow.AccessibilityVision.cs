using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Lumora.WinUI;

// ── Aides ciblees (accessibilite) ────────────────────────────────────────
// Contrairement au reste de "Confort" (qui ajuste la chrome native Lumora),
// ces deux reglages agissent directement sur le contenu des pages web, via
// un CSS injecte a chaque navigation - meme mecanisme que le confort par
// site (MainWindow.SiteComfort.cs) et le guide de lecture
// (MainWindow.ReadingGuide.cs), mais applique globalement plutot que par
// domaine.
public sealed partial class MainWindow
{
    private async Task ApplyAccessibilityVisionToAllTabsAsync()
    {
        foreach (var tab in _tabs)
        {
            await ApplyAccessibilityVisionAsync(tab.View);
        }
    }

    private async Task ApplyAccessibilityVisionAsync(WebView2? view)
    {
        var core = view?.CoreWebView2;
        if (view is null || core is null)
        {
            return;
        }

        try
        {
            await core.ExecuteScriptAsync(BuildAccessibilityVisionScript(
                _uiSettings.AccessibilityTextSpacing,
                _uiSettings.AccessibilityColorBoostEnabled,
                _uiSettings.AccessibilityReduceBlueLight,
                _uiSettings.AccessibilityLargeCursorEnabled));
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Accessibility vision script skipped: {error.GetType().Name}");
        }
    }

    private static string BuildAccessibilityVisionScript(string? textSpacing, bool colorBoost, bool reduceBlueLight, bool largeCursor)
    {
        var css = new List<string>();

        // Curseur agrandi et contraste (basse vision) : redefinit uniquement
        // le curseur PAR DEFAUT de la page (regle sur html, sans "*") - toute
        // regle plus specifique deja posee par le site ou par le
        // navigateur (input texte -> I-beam, bouton -> pointer...) continue
        // de s'appliquer normalement, seul le fond "fleche" change. Image
        // SVG encodee en donnees (aucune requete reseau), fleche noire
        // epaisse sur fond jaune vif pour un contraste maximal ; taille
        // native de l'image (48px) grande devant le curseur systeme (~24px).
        if (largeCursor)
        {
            css.Add($"html {{ cursor: {BuildLargeCursorCssValue()} !important; }}");
        }

        // Espacement du texte (WCAG 1.4.12) : ligne >= 1.5x, lettres >= 0.12em,
        // mots >= 0.16em, paragraphes >= 2x. "Confortable" reste en-deca du
        // maximum recommande pour un effet plus discret ; "Large" applique le
        // maximum WCAG.
        var normalizedSpacing = textSpacing?.Trim().ToLowerInvariant();
        if (normalizedSpacing is "comfortable" or "wide")
        {
            var wide = normalizedSpacing == "wide";
            var letterSpacing = wide ? "0.12em" : "0.05em";
            var wordSpacing = wide ? "0.16em" : "0.10em";
            var lineHeight = wide ? "1.6" : "1.5";
            var paragraphSpacing = wide ? "2em" : "1.5em";
            css.Add(
                $"html, html * {{ letter-spacing:{letterSpacing}!important; word-spacing:{wordSpacing}!important; line-height:{lineHeight}!important; }}");
            css.Add($"p, li {{ margin-bottom:{paragraphSpacing}!important; }}");
        }

        // Renforcement des couleurs et reduction de lumiere bleue partagent la
        // meme propriete CSS "filter" : composer une seule regle combinee,
        // sinon la seconde declaration ecraserait purement et simplement la
        // premiere (meme specificite, meme !important, seul le dernier
        // gagne en cascade).
        var filterParts = new List<string>();
        if (colorBoost)
        {
            // Sature et durcit le contraste de la page. Ce n'est pas une
            // correction daltonienne scientifique (qui demanderait un rendu
            // pixel par pixel hors de portee d'un simple filtre CSS) : une
            // aide pratique et honnete, pas un dispositif medical.
            filterParts.Add("saturate(1.45)");
            filterParts.Add("contrast(1.08)");
        }

        if (reduceBlueLight)
        {
            // Teinte chaude discrete (sepia leger + luminosite legerement
            // reduite), dans le meme esprit que Night Light / f.lux : moins
            // agressif pour des yeux fatigues, a l'oppose du contraste
            // renforce qui, lui, durcit volontairement l'affichage pour la
            // basse vision.
            filterParts.Add("sepia(0.18)");
            filterParts.Add("saturate(0.92)");
            filterParts.Add("brightness(0.98)");
        }

        if (filterParts.Count > 0)
        {
            css.Add($"html {{ filter: {string.Join(" ", filterParts)} !important; }}");
        }

        var cssText = string.Join(string.Empty, css);
        var serializedCss = JsonSerializer.Serialize(cssText);
        return $$"""
(() => {
  const id = "lumora-accessibility-vision-style";
  const css = {{serializedCss}};
  let style = document.getElementById(id);
  if (!css) {
    if (style) style.remove();
    return;
  }
  if (!style) {
    style = document.createElement("style");
    style.id = id;
    (document.head || document.documentElement).appendChild(style);
  }
  style.textContent = css;
})();
""";
    }

    // Fleche epaisse noire sur fond jaune vif (contraste maximal), pointe en
    // haut a gauche - meme silhouette generale qu'un curseur Windows
    // classique pour rester reconnaissable, agrandie a 48px (le double d'un
    // curseur systeme standard a 100%). Hotspot (6,3) : point de la fleche,
    // mis a l'echelle depuis les coordonnees du viewBox (4,2 sur 32) vers la
    // taille reellement affichee (48, facteur 1.5).
    private static string BuildLargeCursorCssValue()
    {
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='48' height='48' viewBox='0 0 32 32'>" +
            "<path d='M4 2 L4 26 L11 20 L15 29 L19 27 L15 18 L24 18 Z' fill='#FFCC33' stroke='#000000' stroke-width='2' stroke-linejoin='round'/>" +
            "</svg>";
        var encoded = Uri.EscapeDataString(svg);
        return $"url(\"data:image/svg+xml,{encoded}\") 6 3, auto";
    }
}
