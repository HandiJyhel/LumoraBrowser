using Microsoft.UI.Xaml;

namespace Lumora.WinUI;

// ── Dictée vocale (accessibilité) ────────────────────────────────────────
// Lumora n'embarque plus de moteur de reconnaissance vocale. L'implémentation
// SAPI (System.Speech + capture NAudio, 0.65.3 → 0.65.7) retranscrivait mal
// malgré le gain automatique et le filtre de confiance : le moteur SAPI date
// d'une autre époque et sa reconnaissance du français reste médiocre. La
// dictée moderne de Windows (Win+H) fonctionne très bien dans les champs du
// navigateur ; le bouton micro sert d'aide-mémoire vers ce raccourci. Il
// n'existe pas d'API publique pour déclencher cette dictée par programme
// (seule une simulation clavier Win+H serait possible, fragile), d'où le
// choix assumé d'une simple astuce. Le réglage "Confort" dédié qui
// grisait/activait ce bouton a été retiré le 2026-07-20 (aucune fonction
// réelle derrière ce toggle, juste une opacité réduite) : le bouton reste
// disponible en permanence via le système de modules épinglables.
public sealed partial class MainWindow
{
    private const string DictationShortcutTip =
        "Dictee vocale : cliquez dans un champ de texte puis appuyez sur Win+H pour utiliser la dictee Windows.";

    private void UpdateDictationButtonVisibility()
    {
        if (DictationPinnedButton is null) return;
        DictationPinnedButton.Opacity = 1;
        UpdateModulesPinUi();
    }

    private void MicDictationButton_Click(object sender, RoutedEventArgs e) =>
        StatusText.Text = DictationShortcutTip;
}
