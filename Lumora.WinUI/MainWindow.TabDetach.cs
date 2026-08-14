using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;

namespace Lumora.WinUI;

// ── Détacher un onglet en nouvelle fenêtre ─────────────────────────────────
// Deux points d'entrée pour le même geste (demande explicite utilisateur,
// 2026-08-13) :
// - glisser-déposer un onglet du rail vertical hors de la fenêtre (le geste
//   "à la Chrome/Windows Terminal") ;
// - "Déplacer vers une nouvelle fenêtre" dans le clic droit de l'onglet,
//   alternative accessible pour qui ne peut pas faire ce geste à la souris
//   (même raisonnement que "Monter"/"Descendre" à côté du glisser-déposer de
//   réordonnancement, voir MainWindow.TabGroups.cs).
//
// Un WebView2 ne peut pas être déplacé physiquement d'une Window à l'autre
// (contrôle lié à la fenêtre qui l'a créé) : la nouvelle fenêtre recharge la
// même adresse plutôt que de migrer le moteur existant. La session
// (cookies/connexions) reste intacte grâce au profil partagé entre fenêtres
// du même process (voir MainWindow.NewWindow.cs) - seul l'état EN PAGE
// (formulaire en cours, position de défilement...) ne survit pas, même
// limite déjà acceptée pour "Dupliquer l'onglet".
public sealed partial class MainWindow
{
    // BrowserReady/IsBrowserReady : déclarés dans MainWindow.NewWindow.cs
    // (partagés avec le déverrouillage rapide "Nouvelle fenêtre").

    private void DetachTabToNewWindow(BrowserTabState tab)
    {
        // Rien à détacher si c'est le seul onglet restant : la fenêtre
        // d'origine se retrouverait sans onglet du tout.
        if (_tabs.Count <= 1) return;

        var title = tab.Title;
        var address = tab.Address;
        var pinned = tab.Pinned;

        var window = new MainWindow(_pendingGuestLaunch, unlockSource: this);
        window.Activate();
        window.InitializeBrowserSurface();

        // IsBrowserReady peut déjà être vrai ICI : le déverrouillage rapide
        // (TryFastUnlockFrom) est synchrone, terminé pendant le constructeur,
        // avant même que InitializeBrowserSurface (ci-dessus) ait ajouté
        // l'onglet vierge par défaut - dans ce cas, adopter l'onglet détaché
        // tout de suite plutôt que d'attendre un événement qui ne se
        // redéclenchera plus. Sinon (déverrouillage normal, asynchrone,
        // encore en attente d'une vraie saisie utilisateur), s'abonner à
        // l'événement comme avant.
        if (window.IsBrowserReady)
        {
            AdoptDetachedTab(window, title, address, pinned);
        }
        else
        {
            window.BrowserReady += () => AdoptDetachedTab(window, title, address, pinned);
        }

        CloseTab(tab);
    }

    // La nouvelle fenêtre a déjà créé/restauré son propre onglet par défaut
    // (accueil vierge) - le remplacer par l'onglet détaché plutôt que de
    // garder les deux, sauf si une vraie session (plusieurs onglets, ou un
    // onglet non-accueil) a été restaurée : dans ce cas l'onglet détaché
    // vient s'ajouter.
    private static void AdoptDetachedTab(MainWindow window, string title, string address, bool pinned)
    {
        var defaultTab = window._tabs.Count == 1 &&
                          window._tabs[0].Address == "lumora://accueil" &&
                          !window._tabs[0].Pinned
            ? window._tabs[0]
            : null;
        window.AddTab(title, address, select: true, pinned: pinned);
        if (defaultTab is not null)
        {
            window.CloseTab(defaultTab);
        }
    }

    private void DetachTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            DetachTabToNewWindow(tab);
        }
    }

    // ── Glisser-déposer hors de la fenêtre (rail vertical) ────────────────
    // VerticalTabButton_DragStarting/DragOver/Drop (MainWindow.TabGroups.cs)
    // gèrent déjà le réordonnancement DANS le rail. DropCompleted, lui, se
    // déclenche à la fin de TOUT glisser-déposer, accepté ou non : un
    // DropResult "None" ET un curseur relâché hors des limites de la
    // fenêtre signent un vrai geste de détachement (pas juste un dépose
    // refusé à l'intérieur, ex. mélange épinglé/normal - voir le commentaire
    // au-dessus de ReorderTab). Le seuil "hors fenêtre" évite tout
    // déclenchement accidentel pendant un réordonnancement normal, qui reste
    // bien à l'intérieur des limites de la fenêtre.
    private void VerticalTabButton_DropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        if (args.DropResult != Windows.ApplicationModel.DataTransfer.DataPackageOperation.None) return;
        if (sender is not Button { Tag: int id }) return;
        var tab = _tabs.FirstOrDefault(t => t.Id == id);
        if (tab is null || _appWindow is null) return;
        if (!GetCursorPos(out var cursor)) return;

        var position = _appWindow.Position;
        var size = _appWindow.Size;
        var outsideWindow =
            cursor.X < position.X || cursor.X > position.X + size.Width ||
            cursor.Y < position.Y || cursor.Y > position.Y + size.Height;
        if (!outsideWindow) return;

        DetachTabToNewWindow(tab);
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
