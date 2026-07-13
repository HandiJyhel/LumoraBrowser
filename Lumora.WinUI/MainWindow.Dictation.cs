using Microsoft.UI.Xaml;

namespace Lumora.WinUI;

// ── Dictée vocale (accessibilité) ────────────────────────────────────────
// Lumora n'embarque plus de moteur de reconnaissance vocale. L'implémentation
// SAPI (System.Speech + capture NAudio, 0.65.3 → 0.65.7) retranscrivait mal
// malgré le gain automatique et le filtre de confiance : le moteur SAPI date
// d'une autre époque et sa reconnaissance du français reste médiocre. La
// dictée moderne de Windows (Win+H) fonctionne très bien dans les champs du
// navigateur ; le bouton micro sert désormais d'aide-mémoire vers ce
// raccourci. Il n'existe pas d'API publique pour déclencher cette dictée par
// programme (seule une simulation clavier Win+H serait possible, fragile),
// d'où le choix assumé d'une simple astuce.
public sealed partial class MainWindow
{
    private const string DictationShortcutTip =
        "Dictee vocale : cliquez dans un champ de texte puis appuyez sur Win+H pour utiliser la dictee Windows.";

    private void UpdateDictationButtonVisibility()
    {
        if (MicDictationButton is null) return;
        MicDictationButton.Visibility = _uiSettings.AccessibilityVoiceDictationEnabled
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void MicDictationButton_Click(object sender, RoutedEventArgs e) =>
        StatusText.Text = DictationShortcutTip;
}
