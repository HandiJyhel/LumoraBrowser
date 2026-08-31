using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

// Mise en veille des onglets inactifs (2026-08-31, retour utilisateur :
// "optimiser un maximum la gestion memoire et processeur"). Reutilise
// entierement le mecanisme de creation paresseuse deja existant
// (EnsureTabViewReadyAsync, MainWindow.Navigation.cs) : "mettre en veille" =
// exactement l'inverse, decharger le moteur WebView2 (CloseTabView, la MEME
// methode que la fermeture normale d'un onglet - meme nettoyage : Credentials,
// scripts privacy, dictionnaires par-coeur) en gardant Address/Title/IconPath.
// La reactivation recree tout seul via le meme chemin qu'un onglet jamais
// encore ouvert (ActivateTab -> EnsureTabView si tab.View est null) : aucun
// code de "reveil" a ecrire. WebView2 n'expose aucune API de gel partiel -
// seul un dechargement complet libere reellement memoire/CPU, comme les
// onglets "en sommeil" d'Edge/Chrome (le contenu recharge a zero, defilement/
// saisie non enregistree perdus - meme compromis assume par ces navigateurs).
public sealed partial class MainWindow
{
    private DispatcherTimer? _tabSuspensionTimer;

    private void InitTabSuspensionTimer()
    {
        _tabSuspensionTimer?.Stop();
        _tabSuspensionTimer = null;
        if (_isGuestMode) return; // session deja ephemere, rien a economiser
        if (!_uiSettings.TabSuspensionEnabled || _uiSettings.TabSuspensionThresholdMinutes <= 0)
        {
            return;
        }

        // Tick toutes les minutes : suffisant face a un seuil exprime en
        // dizaines de minutes, pas besoin d'une precision plus fine.
        _tabSuspensionTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _tabSuspensionTimer.Tick += (_, _) => SuspendEligibleTabs(manual: false);
        _tabSuspensionTimer.Start();
    }

    // Decharge tous les onglets inactifs depuis au moins le seuil regle (ou
    // tout de suite si manual=true, ignore alors le seuil). Exclusions :
    // - l'onglet actif (seule exclusion demandee explicitement) ;
    // - un onglet visible en vue partagee (IsTabInSplitView) : pas une
    //   politique, une necessite technique - un onglet affiche a l'ecran ne
    //   peut pas perdre son moteur sans casser l'affichage, meme s'il n'est
    //   pas "l'onglet actif" au sens strict.
    // manual=true (bouton/raccourci "Verrouiller maintenant" ou
    // LockSessionNow) fonctionne meme si TabSuspensionEnabled est desactive :
    // le minuteur automatique est optionnel, ce declenchement immediat ne
    // l'est pas.
    private void SuspendEligibleTabs(bool manual)
    {
        var activeId = CurrentTab()?.Id;
        var threshold = TimeSpan.FromMinutes(Math.Max(1, _uiSettings.TabSuspensionThresholdMinutes));
        var now = DateTimeOffset.Now;
        var suspendedAny = false;

        foreach (var tab in _tabs)
        {
            if (tab.Id == activeId) continue;
            if (tab.View is null) continue; // deja en veille, ou jamais encore ouvert
            if (IsTabInSplitView(tab.Id)) continue;
            if (!manual && now - tab.LastActiveAt < threshold) continue;

            CloseTabView(tab);
            tab.IsDormant = true;
            suspendedAny = true;
        }

        if (suspendedAny)
        {
            RenderVerticalTabs();
            RefreshHorizontalTabHeaders();
        }
    }

    private void TabSuspensionEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.TabSuspensionEnabled = TabSuspensionEnabledSwitch.IsOn;
        TabSuspensionThresholdCombo.IsEnabled = TabSuspensionEnabledSwitch.IsOn;
        _uiSettings.Save(_profile.UiSettingsFile);
        InitTabSuspensionTimer();
    }

    private void TabSuspensionThresholdCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        if (TabSuspensionThresholdCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out var minutes))
        {
            _uiSettings.TabSuspensionThresholdMinutes = minutes;
            _uiSettings.Save(_profile.UiSettingsFile);
            InitTabSuspensionTimer();
        }
    }

    // ── Verrouillage manuel ("Verrouiller maintenant", Ctrl+Maj+L) ──────────
    // Bouton toolbar + raccourci clavier demandes explicitement par
    // l'utilisateur (2026-08-31) : verrouille Lumora tout de suite (comme
    // Win+L) ET met en pause les onglets inactifs au meme moment, sans
    // attendre leur minuteur individuel - un seul geste pour les deux effets.
    // LockSessionNow (MainWindow.Profile.cs) appelle SuspendEligibleTabs lui-
    // meme des qu'un verrouillage a reellement lieu : le verrouillage
    // automatique par inactivite (SessionTimer_Tick) en profite donc aussi,
    // gratuitement, sans code duplique ici.
    private void LockNowMenu_Click(object sender, RoutedEventArgs e) =>
        LockSessionNow("Verrouillé manuellement.");

    private void LockNowAccelerator() =>
        LockSessionNow("Verrouillé manuellement.");
}
