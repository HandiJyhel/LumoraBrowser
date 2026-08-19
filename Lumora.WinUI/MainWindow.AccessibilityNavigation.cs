using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private enum AccessibilityShellZone
    {
        Tabs,
        Address,
        Content,
        Tools,
        Companion
    }

    private sealed record AccessibilityShellZoneDefinition(
        AccessibilityShellZone Zone,
        string Label,
        string Hint);

    private static readonly AccessibilityShellZoneDefinition[] AccessibilityShellZones =
    [
        new(AccessibilityShellZone.Tabs, "Onglets", "Parcourez vos onglets ouverts."),
        new(AccessibilityShellZone.Address, "Barre d'adresse", "Saisissez une adresse ou une recherche."),
        new(AccessibilityShellZone.Content, "Contenu actif", "Interagissez avec la page ou le panneau ouvert."),
        new(AccessibilityShellZone.Tools, "Outils et navigation", "Ouvrez le menu de navigation ou les modules Lumora."),
        new(AccessibilityShellZone.Companion, "Compagnon et statut", "Retrouvez le compagnon Lumie et les actions de pied de page.")
    ];

    private void RegisterAccessibilityZoneAccelerators()
    {
        RegisterAccessibilityZoneAccelerator(
            VirtualKey.F6,
            VirtualKeyModifiers.None,
            () => CycleAccessibilityShellZone(+1));
        RegisterAccessibilityZoneAccelerator(
            VirtualKey.F6,
            VirtualKeyModifiers.Shift,
            () => CycleAccessibilityShellZone(-1));
        RegisterAccessibilityZoneAccelerator(
            VirtualKey.Number1,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu,
            () => FocusAccessibilityShellZone(AccessibilityShellZone.Tabs));
        RegisterAccessibilityZoneAccelerator(
            VirtualKey.Number2,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu,
            () => FocusAccessibilityShellZone(AccessibilityShellZone.Address));
        RegisterAccessibilityZoneAccelerator(
            VirtualKey.Number3,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu,
            () => FocusAccessibilityShellZone(AccessibilityShellZone.Content));
        RegisterAccessibilityZoneAccelerator(
            VirtualKey.Number4,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu,
            () => FocusAccessibilityShellZone(AccessibilityShellZone.Tools));
        RegisterAccessibilityZoneAccelerator(
            VirtualKey.Number5,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu,
            () => FocusAccessibilityShellZone(AccessibilityShellZone.Companion));
    }

    private void RegisterAccessibilityZoneAccelerator(
        VirtualKey key,
        VirtualKeyModifiers modifiers,
        Action onInvoked)
    {
        var accelerator = new KeyboardAccelerator
        {
            Key = key,
            Modifiers = modifiers
        };
        accelerator.Invoked += (_, args) =>
        {
            if (IsRightAltKeyDown()) return;
            args.Handled = true;
            onInvoked();
        };
        Content.KeyboardAccelerators.Add(accelerator);
    }

    private void CycleAccessibilityShellZone(int direction)
    {
        if (!CanUseAccessibilityShellZoneNavigation())
        {
            return;
        }

        var count = AccessibilityShellZones.Length;
        for (var step = 1; step <= count; step++)
        {
            var nextIndex = ((_lastAccessibilityShellZoneIndex + (direction * step)) % count + count) % count;
            if (FocusAccessibilityShellZone(AccessibilityShellZones[nextIndex].Zone))
            {
                return;
            }
        }
    }

    private bool FocusAccessibilityShellZone(AccessibilityShellZone zone) =>
        FocusAccessibilityShellZone(zone, announce: true);

    private bool FocusAccessibilityShellZone(AccessibilityShellZone zone, bool announce)
    {
        if (!CanUseAccessibilityShellZoneNavigation())
        {
            return false;
        }

        var focused = zone switch
        {
            AccessibilityShellZone.Tabs => TryFocusTabsZone(),
            AccessibilityShellZone.Address => TryFocusAddressZone(),
            AccessibilityShellZone.Content => TryFocusContentZone(),
            AccessibilityShellZone.Tools => TryFocusToolsZone(),
            AccessibilityShellZone.Companion => TryFocusCompanionZone(),
            _ => false
        };

        if (!focused)
        {
            return false;
        }

        _lastAccessibilityShellZoneIndex = Array.FindIndex(
            AccessibilityShellZones,
            definition => definition.Zone == zone);

        if (announce &&
            Array.Find(AccessibilityShellZones, definition => definition.Zone == zone) is { } selected)
        {
            AnnounceAccessibilityContext($"Zone clavier : {selected.Label}. {selected.Hint}");
        }

        return true;
    }

    private bool TryFocusTabsZone()
    {
        // En plein ecran immersif, BrowserTabs est toujours Collapsed (et
        // AddressBox aussi, via NavigationToolbar) : TryFocusCandidate ne
        // verifie que la Visibility locale de l'element, pas celle de ses
        // ancetres, donc les deux "reussissaient" silencieusement sans que le
        // focus ne bouge reellement (audit accessibilite moteur/motricite,
        // palier 0.93.x). Rediriger vers le rail d'onglets vertical, seule
        // liste d'onglets reellement affichable dans ce mode.
        if (IsImmersiveFullScreenActive())
        {
            if (!_verticalTabsEnabled) return false;

            ShowVerticalTabsRailImmersive();
            if (FindFirstFocusableDescendant(VerticalTabsRail) is { } target)
            {
                target.Focus(FocusState.Programmatic);
                return true;
            }

            return false;
        }

        return TryFocusCandidate(BrowserTabs) || TryFocusCandidate(AddressBox);
    }

    private bool TryFocusAddressZone()
    {
        // Meme piege qu'au-dessus : AddressBox vit dans NavigationToolbar,
        // Collapsed en plein ecran immersif, et la barre compacte de ce mode
        // (FullScreenAddressText) n'est qu'un texte en lecture seule - aucune
        // saisie d'adresse n'y est possible. La palette de commande gere deja
        // une saisie d'adresse/recherche et son placement est deja adapte au
        // plein ecran (ApplyCommandPalettePlacement) : on l'ouvre a la place.
        if (IsImmersiveFullScreenActive())
        {
            ShowCommandPalette();
            return true;
        }

        return TryFocusCandidate(AddressBox);
    }

    private bool TryFocusContentZone()
    {
        var panel = CurrentAccessibilityContentPanel();
        if (ReferenceEquals(panel, BrowserPanel))
        {
            if (CurrentTab()?.View is { } view)
            {
                view.Focus(FocusState.Programmatic);
                return true;
            }

            return TryFocusCandidate(BrowserTabs);
        }

        if (FindFirstFocusableDescendant(panel) is { } target)
        {
            target.Focus(FocusState.Programmatic);
            return true;
        }

        return false;
    }

    private bool TryFocusToolsZone() =>
        TryFocusCandidate(ModulesButton);

    private bool TryFocusCompanionZone() =>
        TryFocusCandidate(BackToPageButton) ||
        TryFocusCandidate(ModeUsageButton) ||
        TryFocusCandidate(CompanionButton) ||
        TryFocusCandidate(AccessibilityMenuButton);

    private FrameworkElement CurrentAccessibilityContentPanel()
    {
        if (SettingsPanel.Visibility == Visibility.Visible) return SettingsPanel;
        if (AboutPanel.Visibility == Visibility.Visible) return AboutPanel;
        if (BookmarksPanel.Visibility == Visibility.Visible) return BookmarksPanel;
        if (ImportPanel.Visibility == Visibility.Visible) return ImportPanel;
        if (HistoryPanel.Visibility == Visibility.Visible) return HistoryPanel;
        if (DownloadsPanel.Visibility == Visibility.Visible) return DownloadsPanel;
        if (SavedTabGroupsPanel.Visibility == Visibility.Visible) return SavedTabGroupsPanel;
        if (NotesPanel.Visibility == Visibility.Visible) return NotesPanel;
        if (VaultPanel.Visibility == Visibility.Visible) return VaultPanel;
        if (ModulesPanel.Visibility == Visibility.Visible) return ModulesPanel;
        if (SiteControlPanel.Visibility == Visibility.Visible) return SiteControlPanel;
        if (SessionsPanel.Visibility == Visibility.Visible) return SessionsPanel;
        if (WalletPanel.Visibility == Visibility.Visible) return WalletPanel;
        if (WebAppsPanel.Visibility == Visibility.Visible) return WebAppsPanel;
        if (ReadingLensPanel.Visibility == Visibility.Visible) return ReadingLensPanel;

        return BrowserPanel;
    }

    private bool TryFocusCandidate(FrameworkElement? element)
    {
        if (element is null || !CanReceiveProgrammaticFocus(element))
        {
            return false;
        }

        element.Focus(FocusState.Programmatic);
        return true;
    }

    private bool CanUseAccessibilityShellZoneNavigation() =>
        LoginOverlay.Visibility != Visibility.Visible &&
        SetupWizardOverlay.Visibility != Visibility.Visible &&
        CommandPaletteOverlay.Visibility != Visibility.Visible;
}
