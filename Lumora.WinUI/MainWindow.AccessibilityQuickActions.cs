using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private void RegisterAccessibilityQuickActionAccelerators()
    {
        RegisterAccessibilityQuickAccelerator(VirtualKey.Number6, () => ApplyAccessibilityComfortProfile("balanced"));
        RegisterAccessibilityQuickAccelerator(VirtualKey.Number8, () => ApplyAccessibilityComfortProfile("vision"));
        RegisterAccessibilityQuickAccelerator(VirtualKey.Number0, AnnounceAccessibilityComfortState);
    }

    private void RegisterAccessibilityQuickAccelerator(VirtualKey key, Action onInvoked)
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

    // Renomme depuis AccessibilityQuickFlyout_Opening (2026-08-10, defusion
    // des 3 menus) : ce flyout est desormais celui du menu "Accessibilite"
    // independant - plus d'appel a UpdateModeCompanionUi(), le Compagnon a
    // son propre flyout/Opening (CompanionFlyout_Opening, MainWindow.UsageMode.cs).
    private void AccessibilityFlyout_Opening(object sender, object e)
    {
        UpdateAccessibilityQuickFlyoutUi();
        UpdateAccessibilityQuickContextUi();
    }

    private void AccessibilityQuickPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string profileKey })
        {
            return;
        }

        ApplyAccessibilityComfortProfile(profileKey);
        UpdateAccessibilityQuickFlyoutUi();
        AnnounceAccessibilityComfortState();
    }

    private void AccessibilityQuickToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string action })
        {
            return;
        }

        switch (action)
        {
            case "guide":
                ReadingGuideEnabledSwitch.IsOn = !ReadingGuideEnabledSwitch.IsOn;
                break;
            case "lens":
                ReadingLensEnabledSwitch.IsOn = !ReadingLensEnabledSwitch.IsOn;
                break;
            case "readaloud":
                ReadAloudEnabledSwitch.IsOn = !ReadAloudEnabledSwitch.IsOn;
                break;
            default:
                return;
        }

        UpdateAccessibilityQuickFlyoutUi();
        AnnounceAccessibilityComfortState();
    }

    private void AccessibilityQuickAnnounceButton_Click(object sender, RoutedEventArgs e) =>
        AnnounceAccessibilityComfortState();

    // Menu "Accessibilite" independant depuis le 2026-08-10 (defusion des 3
    // menus, demande explicite utilisateur) : porte desormais son propre
    // tooltip/nom d'accessibilite, plus besoin d'appeler UpdateUsageModeButtonUi()
    // (Mode est un bouton a part, voir MainWindow.UsageMode.cs).
    private void UpdateAccessibilityQuickButtonUi()
    {
        if (AccessibilityQuickCurrentText is null)
        {
            return;
        }

        var profileKey = ResolveAccessibilityComfortProfileFromControls();
        var preset = FindAccessibilityComfortPreset(profileKey);
        var label = profileKey == "custom" ? "Personnalisé" : preset?.Label ?? "Confort";
        AccessibilityQuickCurrentText.Text = label;

        var eclipsedByHighContrast = _uiSettings.AccessibilityHighContrast;
        var tooltip = eclipsedByHighContrast
            ? $"Confort : {label}. Couleurs remplacées tant que le contraste élevé est actif."
            : $"Confort : {label}.";
        ToolTipService.SetToolTip(AccessibilityMenuButton, tooltip);
        AutomationProperties.SetName(AccessibilityMenuButton, tooltip);
    }

    private void UpdateAccessibilityQuickFlyoutUi()
    {
        if (AccessibilityQuickProfileSummaryText is null)
        {
            return;
        }

        var profileKey = ResolveAccessibilityComfortProfileFromControls();
        var normalized = NormalizeAccessibilityComfortProfile(profileKey);
        var preset = FindAccessibilityComfortPreset(normalized);
        var label = normalized == "custom" ? "Personnalisé" : preset?.Label ?? "Confort";

        AccessibilityQuickProfileTitleText.Text = $"Profil courant : {label}";
        AccessibilityQuickProfileSummaryText.Text = normalized == "custom"
            ? "Mélange manuel détecté : vos interrupteurs actuels gardent la priorité."
            : preset?.Summary ?? "Choisissez un profil de confort.";
        AccessibilityQuickStateText.Text = DescribeAccessibilityComfortState();
        UpdateAccessibilityQuickContextUi();
        UpdateAccessibilityQuickRescueUi();

        UpdateAccessibilityQuickToggleCopy(
            AccessibilityQuickGuideButton,
            AccessibilityQuickGuideHintText,
            ReadingGuideEnabledSwitch.IsOn,
            "Le guide de lecture immersif est actif.",
            "Le guide de lecture immersif est désactivé.");
        UpdateAccessibilityQuickToggleCopy(
            AccessibilityQuickLensButton,
            AccessibilityQuickLensHintText,
            ReadingLensEnabledSwitch.IsOn,
            "La loupe de lecture est active.",
            "La loupe de lecture est désactivée.");
        UpdateAccessibilityQuickToggleCopy(
            AccessibilityQuickReadAloudButton,
            AccessibilityQuickReadAloudHintText,
            ReadAloudEnabledSwitch.IsOn,
            "La lecture à voix haute locale est active.",
            "La lecture à voix haute locale est désactivée.");

        MarkQuickPreset(AccessibilityQuickBalancedButton, normalized, "balanced");
        MarkQuickPreset(AccessibilityQuickVisionButton, normalized, "vision");
    }

    private void AccessibilityQuickOpenSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(SettingsPanel, "Paramètres");
        SettingsAccessibilityShortcutButton_Click(sender, e);
    }

    private static void UpdateAccessibilityQuickToggleCopy(
        Button button,
        TextBlock hintText,
        bool enabled,
        string enabledText,
        string disabledText)
    {
        hintText.Text = enabled ? enabledText : disabledText;
        button.Opacity = enabled ? 1 : 0.84;
        button.FontWeight = enabled ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private static void MarkQuickPreset(Button button, string currentProfile, string expectedProfile)
    {
        var active = string.Equals(currentProfile, expectedProfile, StringComparison.OrdinalIgnoreCase);
        button.Opacity = active ? 1 : 0.84;
        button.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private string DescribeAccessibilityComfortState()
    {
        var parts = new List<string>();
        var profileKey = ResolveAccessibilityComfortProfileFromControls();
        var preset = FindAccessibilityComfortPreset(profileKey);
        parts.Add($"Profil {((profileKey == "custom") ? "personnalisé" : preset?.Label ?? "confort")}.");

        if (AccessibilityLargeTextSwitch.IsOn)
        {
            parts.Add("Texte plus lisible actif.");
        }

        if (AccessibilityHighContrastSwitch.IsOn)
        {
            parts.Add("Contraste renforcé actif.");
        }

        if (AccessibilityReduceMotionSwitch.IsOn)
        {
            parts.Add("Transitions réduites.");
        }

        if (AccessibilityReduceBlueLightSwitch.IsOn)
        {
            parts.Add("Lumière bleue réduite sur les pages.");
        }

        if (ReadingGuideEnabledSwitch.IsOn)
        {
            parts.Add($"Guide de lecture actif en bande {NormalizeReadingGuideBandHeight(SelectedReadingGuideBandHeight())} pixels.");
        }

        if (ReadingLensEnabledSwitch.IsOn)
        {
            parts.Add("Loupe de lecture disponible.");
        }

        if (ReadAloudEnabledSwitch.IsOn)
        {
            parts.Add("Lecture à voix haute locale disponible.");
        }

        if (HasAccessibilityRescueSnapshot())
        {
            parts.Add("Retour à l'état d'avant disponible.");
        }

        if (parts.Count == 1)
        {
            parts.Add("Aide ponctuelle désactivée.");
        }

        return string.Join(" ", parts);
    }

    private void AnnounceAccessibilityComfortState()
    {
        UpdateAccessibilityQuickFlyoutUi();
        UpdateStatusText("État de confort annoncé.", announce: false);
        AnnounceAccessibilityContext(DescribeAccessibilityComfortState(), AutomationNotificationKind.Other);
    }
}
