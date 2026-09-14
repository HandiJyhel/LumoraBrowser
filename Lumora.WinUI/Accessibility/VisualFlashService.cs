using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI.Accessibility;

// "Remplacer les sons par un flash visuel" (accessibilite, chantier
// "Ajustement n2" 2026-09-11) : fournit un flash visuel discret (bref eclair
// sur une bordure plein ecran, IsHitTestVisible=False pour ne jamais
// intercepter de clic) a appeler aux moments qui auraient logiquement merite
// un son - aujourd'hui : rappel de pause et demande de notification d'un
// site. Extrait dans son propre fichier pour ne pas alourdir davantage
// MainWindow.Settings.cs/MainWindow.xaml.cs.
//
// Ne touche PLUS ElementSoundPlayer directement (2026-09-11, correction) :
// "Sons & ambiance" (Sound/SoundThemeService.cs) veut, LUI AUSSI, couper les
// sons systeme quand son propre pack est actif - les deux reglages
// controlant le meme etat global WinUI sans se coordonner, l'un pouvait
// ecraser le choix de l'autre. Coupe desormais de facon centralisee par
// MainWindow (UpdateElementSoundMuteState, MainWindow.SoundTheme.cs) d'apres
// l'OU logique des deux reglages.
internal sealed class VisualFlashService
{
    private const double FlashOpacity = 0.30;
    private static readonly TimeSpan FlashDuration = TimeSpan.FromMilliseconds(220);

    private readonly Border _overlay;
    private readonly DispatcherTimer _resetTimer = new() { Interval = FlashDuration };

    public bool Enabled { get; private set; }

    public VisualFlashService(Border overlay)
    {
        _overlay = overlay;
        _resetTimer.Tick += (_, _) =>
        {
            _resetTimer.Stop();
            _overlay.Opacity = 0;
        };
    }

    public void SetEnabled(bool enabled) => Enabled = enabled;

    public void FlashIfEnabled()
    {
        if (!Enabled)
        {
            return;
        }

        _overlay.Opacity = FlashOpacity;
        _resetTimer.Stop();
        _resetTimer.Start();
    }
}
