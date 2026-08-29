using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // ── Nouvelle fenêtre (Ctrl+N) ────────────────────────────────────────────
    // Contrairement à Incognito (process Windows séparé, volontairement
    // isolé - voir MainWindow.Incognito.cs), une nouvelle fenêtre normale
    // reste dans CE MÊME process et réutilise donc automatiquement le même
    // environnement WebView2 : WebView2Bootstrap.ConfigureOnce est un garde
    // process-wide (déjà posé par la première fenêtre, no-op ici), donc la
    // nouvelle fenêtre pointe vers le même dossier de profil WebView2 -
    // cookies, connexions et sessions web partagés avec la fenêtre
    // d'origine, exactement comme deux fenêtres Chrome/Edge/Firefox. C'est
    // la correction du bug remonté le 2026-08-13 (redemande de connexion
    // Google/notification 2FA à chaque nouvelle fenêtre).
    //
    // Déverrouillage local (2026-08-13, suite) : la nouvelle fenêtre reprend
    // aussi le déverrouillage du coffre déjà fait par la fenêtre d'origine
    // (TryFastUnlockFrom ci-dessous, clé transmise en mémoire seulement via
    // VaultStore.UnlockWithKey/UnlockedKeySnapshot - jamais écrite sur
    // disque, jamais hors process) : plus d'écran "Choisir un profil" ni de
    // mot de passe à retaper pour une fenêtre supplémentaire. Repli
    // automatique et silencieux sur l'écran de connexion normal si ça
    // échoue (coffre en mode DPAPI sans mot de passe maître, etc.).
    //
    // Limite restante, assumée : deux fenêtres du même profil qui modifient
    // TOUTES LES DEUX les favoris/onglets/historique en même temps peuvent
    // s'écraser l'une l'autre (chaque fenêtre garde sa propre copie en
    // mémoire, dernière sauvegarde gagne - pas de synchronisation entre
    // fenêtres). Sans impact pour le cas d'usage visé ici (naviguer dans une
    // deuxième fenêtre en restant connecté) ; à traiter dans une session
    // dédiée si l'usage réel révèle un vrai besoin d'édition simultanée.
    private void NewWindowMenu_Click(object sender, RoutedEventArgs e) =>
        OpenNewWindow();

    private void NewWindowAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        OpenNewWindow();
    }

    // Suivi mono-instance (App.xaml.cs, 2026-08-13) : quand une relance de
    // l'icône Lumora est redirigée vers CE process au lieu d'en démarrer un
    // nouveau, il faut une fenêtre MainWindow vivante quelconque pour porter
    // OpenNewWindow() - la toute première fenêtre du process (App._window)
    // peut avoir été fermée entre-temps si l'utilisateur a gardé d'autres
    // fenêtres ouvertes (Ctrl+N) sans garder celle-là précisément. Liste
    // plutôt qu'un seul champ : gère ce cas sans dépendre de quelle fenêtre
    // exactement reste ouverte.
    private static readonly List<MainWindow> _liveInstances = new();

    // url non nul : lien cliqué ailleurs dans Windows pendant que Lumora
    // tourne déjà (voir App.xaml.cs/OnExistingInstanceActivated) - ouvre un
    // onglet dans la fenêtre la plus récente au lieu d'une fenêtre vierge,
    // comme Chrome/Edge/Firefox pour ce même scénario.
    internal static void OpenNewWindowFromExternalActivation(string? url = null)
    {
        var target = _liveInstances.LastOrDefault();
        if (target is null) return;

        if (!string.IsNullOrWhiteSpace(url))
        {
            target.OpenUrlInNewTab(url);
        }
        else
        {
            target.OpenNewWindow();
        }
    }

    // Même garde que OpenNewWindow : ignore tant que le profil courant n'est
    // pas déverrouillé (barre d'onglets pas encore prête).
    private void OpenUrlInNewTab(string url)
    {
        if (LoginOverlay.Visibility == Visibility.Visible ||
            SetupWizardOverlay.Visibility == Visibility.Visible)
        {
            return;
        }

        AddTab(DisplayTitle(url), url, select: true);
        BringToForeground();
    }

    // Ramène cette fenêtre au premier plan pour un lien cliqué ailleurs -
    // sans ça, l'onglet s'ouvrait silencieusement derrière les autres
    // fenêtres si Lumora était minimisé ou en arrière-plan.
    private void BringToForeground()
    {
        if (_appWindow?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter &&
            presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Minimized)
        {
            presenter.Restore();
        }
        Activate();
    }

    // Signalé une fois par fenêtre (MainWindow.Profile.cs/DismissLoginOverlay),
    // quand ses onglets sont prêts. IsBrowserReady est la version "état"
    // (déjà vrai au moment où l'appelant regarde, ex. déverrouillage rapide
    // synchrone terminé pendant le constructeur) ; BrowserReady reste
    // l'événement pour un abonnement AVANT que ce soit encore le cas (flux
    // de connexion normal, asynchrone). Utilisés par MainWindow.TabDetach.cs.
    internal event Action? BrowserReady;
    internal bool IsBrowserReady { get; private set; }

    private void OpenNewWindow()
    {
        // Même garde que OpenIncognitoWindow (MainWindow.Incognito.cs) : pas
        // de nouvelle fenêtre tant que le profil courant n'est pas
        // déverrouillé (réglages/coffre pas encore prêts pour l'alimenter).
        if (LoginOverlay.Visibility == Visibility.Visible ||
            SetupWizardOverlay.Visibility == Visibility.Visible)
        {
            return;
        }

        // Propage l'etat invite de CETTE fenetre à la nouvelle (sinon "Nouvelle
        // fenêtre" depuis une session invite ouvrait une fenetre NORMALE) et
        // lui passe cette fenêtre comme source de déverrouillage rapide - voir
        // TryFastUnlockFrom.
        var window = new MainWindow(_pendingGuestLaunch, unlockSource: this);
        window.Activate();
        window.InitializeBrowserSurface();
    }

    // Reprend le déverrouillage déjà fait par `source` : copie sa clé de
    // coffre déjà dérivée (jamais recalculée - Argon2id est volontairement
    // coûteux) et son profil déjà chargé, puis saute directement à la fin du
    // flux de connexion. Retourne false (sans rien modifier) si la fenêtre
    // source n'est en fait pas déverrouillée, ou si le coffre refuse la clé
    // (ex. coffre DPAPI sans mot de passe maître) - l'appelant retombe alors
    // sur le flux de connexion normal.
    private bool TryFastUnlockFrom(MainWindow source)
    {
        try
        {
            if (source._userProfile is null) return false;
            var key = source._vault.UnlockedKeySnapshot;
            if (key is null) return false;
            if (!_vault.UnlockWithKey(key)) return false;

            _userProfile = source._userProfile;
            // _pendingStartupPageApply reste par défaut à true tant qu'on ne
            // l'a pas explicitement mis à false : le mettre à false ICI, AVANT
            // DismissLoginOverlay, est ce qui lui fait sauter la restauration
            // de session (ApplyStartupPage) - une nouvelle fenêtre ne doit pas
            // dupliquer tous les onglets de la fenêtre d'origine, juste ouvrir
            // un onglet vierge (géré ensuite par InitializeBrowserSurface,
            // appelé par l'appelant juste après la construction).
            _pendingStartupPageApply = false;
            DismissLoginOverlay();
            return true;
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"Fast unlock from sibling window failed: {ex.GetType().Name}");
            return false;
        }
    }
}
