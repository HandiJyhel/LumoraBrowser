using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private sealed record AccessibilityComfortSnapshot(
        bool HighContrast,
        bool LargeText,
        bool ReduceMotion,
        bool VisibleFocus,
        bool ReduceBlueLight,
        bool ReadAloud,
        bool ReadingLens,
        bool ReadingGuide,
        int ReadingGuideBandHeight);

    private AccessibilityComfortSnapshot? _accessibilityRescueSnapshot;

    private void RegisterAccessibilityRescueAccelerators()
    {
        RegisterAccessibilityRescueAccelerator(VirtualKey.S, ActivateAccessibilityRescueMode);
        RegisterAccessibilityRescueAccelerator(VirtualKey.X, RestoreAccessibilityRescueSnapshot);
    }

    private void RegisterAccessibilityRescueAccelerator(VirtualKey key, Action onInvoked)
    {
        var accelerator = new KeyboardAccelerator
        {
            Key = key,
            Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu
        };
        accelerator.Invoked += (_, args) =>
        {
            if (IsRightAltKeyDown()) return;
            args.Handled = true;
            onInvoked();
        };
        Content.KeyboardAccelerators.Add(accelerator);
    }

    private void AccessibilityQuickRescueButton_Click(object sender, RoutedEventArgs e) =>
        ActivateAccessibilityRescueMode();

    private void AccessibilityQuickRestoreButton_Click(object sender, RoutedEventArgs e) =>
        RestoreAccessibilityRescueSnapshot();

    // Meme action que le bouton du footer, exposee aussi depuis la carte
    // "Mode secours" des Reglages > Confort : un seul chemin de logique
    // (ActivateAccessibilityRescueMode / RestoreAccessibilityRescueSnapshot),
    // deux points d'entree visuels, pas deux comportements differents.
    private void AccessibilityRescueActivateButton_Click(object sender, RoutedEventArgs e) =>
        ActivateAccessibilityRescueMode();

    private void AccessibilityRescueRestoreButton_Click(object sender, RoutedEventArgs e) =>
        RestoreAccessibilityRescueSnapshot();

    private void ActivateAccessibilityRescueMode()
    {
        CaptureAccessibilityRescueSnapshotIfNeeded();

        if (string.Equals(ResolveAccessibilityComfortProfileFromControls(), "rescue", StringComparison.OrdinalIgnoreCase))
        {
            UpdateStatusText("Mode secours déjà actif. Ctrl+Alt+X pour revenir à l'état d'avant.");
            return;
        }

        ApplyAccessibilityComfortProfile("rescue");
        UpdateAccessibilityQuickFlyoutUi();
        AnnounceAccessibilityContext(
            "Mode secours activé. Lisibilité renforcée et repères stabilisés. Contrôle Alt X permet de revenir à l'état précédent.",
            AutomationNotificationKind.Other);
    }

    private void RestoreAccessibilityRescueSnapshot()
    {
        if (_accessibilityRescueSnapshot is not { } snapshot)
        {
            UpdateStatusText("Aucun état de confort précédent à restaurer.");
            return;
        }

        ApplyAccessibilityComfortSnapshot(snapshot);
        _accessibilityRescueSnapshot = null;
        UpdateAccessibilityQuickFlyoutUi();
        UpdateStatusText("État de confort précédent restauré.");
        AnnounceAccessibilityContext(
            "État de confort précédent restauré. Le mode secours est quitté.",
            AutomationNotificationKind.Other);
    }

    private void CaptureAccessibilityRescueSnapshotIfNeeded()
    {
        if (_accessibilityRescueSnapshot is not null ||
            string.Equals(ResolveAccessibilityComfortProfileFromControls(), "rescue", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _accessibilityRescueSnapshot = new AccessibilityComfortSnapshot(
            AccessibilityHighContrastSwitch.IsOn,
            AccessibilityLargeTextSwitch.IsOn,
            AccessibilityReduceMotionSwitch.IsOn,
            AccessibilityVisibleFocusSwitch.IsOn,
            AccessibilityReduceBlueLightSwitch.IsOn,
            ReadAloudEnabledSwitch.IsOn,
            ReadingLensEnabledSwitch.IsOn,
            ReadingGuideEnabledSwitch.IsOn,
            SelectedReadingGuideBandHeight());
    }

    private bool HasAccessibilityRescueSnapshot() =>
        _accessibilityRescueSnapshot is not null;

    // Source unique du texte d'etat du mode secours, reutilisee par les deux
    // surfaces qui l'exposent (le flyout "Confort rapide" du footer et la
    // carte "Mode secours" des Reglages > Confort) pour eviter que les deux
    // divergent.
    private void UpdateAccessibilityQuickRescueUi()
    {
        var rescueActive = string.Equals(ResolveAccessibilityComfortProfileFromControls(), "rescue", StringComparison.OrdinalIgnoreCase);
        var statusText = rescueActive
            ? "Mode secours actif. Le retour à l'état d'avant reste disponible."
            : HasAccessibilityRescueSnapshot()
                ? "Un état précédent est mémorisé. Vous pouvez encore y revenir."
                : "Aucun retour mémorisé pour l'instant.";

        if (AccessibilityQuickRescueStatusText is not null)
        {
            AccessibilityQuickRescueStatusText.Text = statusText;
            AccessibilityQuickRescueButton.Opacity = rescueActive ? 0.92 : 1;
            AccessibilityQuickRescueButton.FontWeight = rescueActive ? FontWeights.SemiBold : FontWeights.Normal;
            AccessibilityQuickRestoreButton.IsEnabled = HasAccessibilityRescueSnapshot();
            AccessibilityQuickRestoreButton.Opacity = HasAccessibilityRescueSnapshot() ? 1 : 0.65;
        }

        if (AccessibilityRescueStatusText is not null)
        {
            AccessibilityRescueStatusText.Text = statusText;
            AccessibilityRescueActivateButton.Opacity = rescueActive ? 0.92 : 1;
            AccessibilityRescueActivateButton.FontWeight = rescueActive ? FontWeights.SemiBold : FontWeights.Normal;
            AccessibilityRescueRestoreButton.IsEnabled = HasAccessibilityRescueSnapshot();
            AccessibilityRescueRestoreButton.Opacity = HasAccessibilityRescueSnapshot() ? 1 : 0.65;
        }
    }

    private void ApplyAccessibilityComfortSnapshot(AccessibilityComfortSnapshot snapshot)
    {
        _suppressUiSettingsSave = true;
        try
        {
            AccessibilityHighContrastSwitch.IsOn = snapshot.HighContrast;
            AccessibilityLargeTextSwitch.IsOn = snapshot.LargeText;
            AccessibilityReduceMotionSwitch.IsOn = snapshot.ReduceMotion;
            AccessibilityVisibleFocusSwitch.IsOn = snapshot.VisibleFocus;
            AccessibilityReduceBlueLightSwitch.IsOn = snapshot.ReduceBlueLight;
            ReadAloudEnabledSwitch.IsOn = snapshot.ReadAloud;
            ReadingLensEnabledSwitch.IsOn = snapshot.ReadingLens;
            ReadingGuideEnabledSwitch.IsOn = snapshot.ReadingGuide;
            SelectComboByTag(
                ReadingGuideBandHeightCombo,
                NormalizeReadingGuideBandHeight(snapshot.ReadingGuideBandHeight).ToString(),
                "160");
        }
        finally
        {
            _suppressUiSettingsSave = false;
        }

        UpdateAccessibilityComfortProfileFromControls();
        ApplyAccessibilityComfortSideEffects();
    }
}
