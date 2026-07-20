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
        RegisterAccessibilityQuickAccelerator(VirtualKey.Number7, () => ApplyAccessibilityComfortProfile("calm"));
        RegisterAccessibilityQuickAccelerator(VirtualKey.Number8, () => ApplyAccessibilityComfortProfile("vision"));
        RegisterAccessibilityQuickAccelerator(VirtualKey.Number9, () => ApplyAccessibilityComfortProfile("reading"));
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
            args.Handled = true;
            onInvoked();
        };
        Content.KeyboardAccelerators.Add(accelerator);
    }

    private void AccessibilityQuickFlyout_Opening(object sender, object e)
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

    private void UpdateAccessibilityQuickButtonUi()
    {
        if (AccessibilityQuickButton is null)
        {
            return;
        }

        var profileKey = ResolveAccessibilityComfortProfileFromControls();
        var preset = FindAccessibilityComfortPreset(profileKey);
        var label = profileKey == "custom" ? "Personnalise" : preset?.Label ?? "Confort";
        AccessibilityQuickCurrentText.Text = label;
        ToolTipService.SetToolTip(
            AccessibilityQuickButton,
            $"Confort rapide : {label}. Ctrl+Alt+6 a Ctrl+Alt+9 pour changer, Ctrl+Alt+0 pour relire l'etat, Ctrl+Alt+S pour le mode secours.");
        AutomationProperties.SetName(AccessibilityQuickButton, $"Confort rapide : {label}");
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
        var label = normalized == "custom" ? "Personnalise" : preset?.Label ?? "Confort";

        AccessibilityQuickProfileTitleText.Text = $"Profil courant : {label}";
        AccessibilityQuickProfileSummaryText.Text = normalized == "custom"
            ? "Melange manuel detecte : vos interrupteurs actuels gardent la priorite."
            : preset?.Summary ?? "Choisissez un profil de confort.";
        AccessibilityQuickStateText.Text = DescribeAccessibilityComfortState();
        UpdateAccessibilityQuickContextUi();
        UpdateAccessibilityQuickRescueUi();

        UpdateAccessibilityQuickToggleCopy(
            AccessibilityQuickGuideButton,
            AccessibilityQuickGuideHintText,
            ReadingGuideEnabledSwitch.IsOn,
            "Le guide de lecture immersif est actif.",
            "Le guide de lecture immersif est desactive.");
        UpdateAccessibilityQuickToggleCopy(
            AccessibilityQuickLensButton,
            AccessibilityQuickLensHintText,
            ReadingLensEnabledSwitch.IsOn,
            "La loupe de lecture est active.",
            "La loupe de lecture est desactivee.");
        UpdateAccessibilityQuickToggleCopy(
            AccessibilityQuickReadAloudButton,
            AccessibilityQuickReadAloudHintText,
            ReadAloudEnabledSwitch.IsOn,
            "La lecture a voix haute locale est active.",
            "La lecture a voix haute locale est desactivee.");

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
        parts.Add($"Profil {((profileKey == "custom") ? "personnalise" : preset?.Label ?? "confort")}.");

        if (AccessibilityLargeTextSwitch.IsOn)
        {
            parts.Add("Texte plus lisible actif.");
        }

        if (AccessibilityHighContrastSwitch.IsOn)
        {
            parts.Add("Contraste renforce actif.");
        }

        if (AccessibilityReduceMotionSwitch.IsOn)
        {
            parts.Add("Transitions reduites.");
        }

        if (AccessibilityReduceBlueLightSwitch.IsOn)
        {
            parts.Add("Lumiere bleue reduite sur les pages.");
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
            parts.Add("Lecture a voix haute locale disponible.");
        }

        if (HasAccessibilityRescueSnapshot())
        {
            parts.Add("Retour a l'etat d'avant disponible.");
        }

        if (parts.Count == 1)
        {
            parts.Add("Aide ponctuelle desactivee.");
        }

        return string.Join(" ", parts);
    }

    private void AnnounceAccessibilityComfortState()
    {
        UpdateAccessibilityQuickFlyoutUi();
        UpdateStatusText("Etat de confort annonce.", announce: false);
        AnnounceAccessibilityContext(DescribeAccessibilityComfortState(), AutomationNotificationKind.Other);
    }
}
