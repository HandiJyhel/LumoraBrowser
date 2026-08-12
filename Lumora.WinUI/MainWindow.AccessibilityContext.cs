using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private void RegisterAccessibilityContextAccelerators()
    {
        RegisterAccessibilityContextAccelerator(VirtualKey.F, AnnounceAccessibilityNavigationContext);
        RegisterAccessibilityContextAccelerator(VirtualKey.R, RestoreAccessibilityFocusAnchor);
    }

    private void RegisterAccessibilityContextAccelerator(VirtualKey key, Action onInvoked)
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

    private void UpdateAccessibilityQuickContextUi()
    {
        if (AccessibilityQuickContextText is null)
        {
            return;
        }

        AccessibilityQuickContextText.Text = DescribeAccessibilityNavigationContext(includeShortcuts: false);
    }

    private string DescribeAccessibilityNavigationContext(bool includeShortcuts = true)
    {
        var zone = ResolveAccessibilityShellZoneFromFocus();
        var zoneDefinition = Array.Find(AccessibilityShellZones, definition => definition.Zone == zone);
        var parts = new List<string>
        {
            $"Repère actuel : {zoneDefinition?.Label ?? "zone active"}.",
            DescribeAccessibilityCurrentSurface()
        };

        if (DescribeAccessibilityFocusedControl() is { Length: > 0 } controlDescription)
        {
            parts.Add(controlDescription);
        }

        if (includeShortcuts)
        {
            parts.Add("Ctrl Alt F relit ce repère. Ctrl Alt R recentre le focus.");
        }

        return string.Join(" ", parts);
    }

    private void AnnounceAccessibilityNavigationContext()
    {
        UpdateAccessibilityQuickContextUi();
        UpdateStatusText("Repère de navigation annoncé.", announce: false);
        AnnounceAccessibilityContext(DescribeAccessibilityNavigationContext(), AutomationNotificationKind.Other);
    }

    private void RestoreAccessibilityFocusAnchor()
    {
        if (!CanUseAccessibilityShellZoneNavigation())
        {
            UpdateStatusText("Recentreur de focus indisponible pendant cet overlay.");
            return;
        }

        var preferredZone = ResolveAccessibilityShellZoneFromFocus();
        var focused =
            FocusAccessibilityShellZone(preferredZone, announce: false) ||
            (preferredZone != AccessibilityShellZone.Content && FocusAccessibilityShellZone(AccessibilityShellZone.Content, announce: false)) ||
            (preferredZone != AccessibilityShellZone.Address && FocusAccessibilityShellZone(AccessibilityShellZone.Address, announce: false)) ||
            FocusAccessibilityShellZone(AccessibilityShellZone.Tabs, announce: false);

        if (!focused)
        {
            UpdateStatusText("Impossible de recentrer le focus pour le moment.");
            return;
        }

        UpdateAccessibilityQuickContextUi();
        UpdateStatusText("Focus recentré sur le repère utile.", announce: false);
        AnnounceAccessibilityContext(DescribeAccessibilityNavigationContext(includeShortcuts: false), AutomationNotificationKind.Other);
    }

    private AccessibilityShellZone ResolveAccessibilityShellZoneFromFocus()
    {
        var focused = Content?.XamlRoot is null ? null : FocusManager.GetFocusedElement(Content.XamlRoot);
        if (focused is DependencyObject dependencyObject)
        {
            if (IsDescendantOf(dependencyObject, BrowserTabs))
            {
                return AccessibilityShellZone.Tabs;
            }

            if (IsDescendantOf(dependencyObject, AddressBox))
            {
                return AccessibilityShellZone.Address;
            }

            if (IsDescendantOf(dependencyObject, ModulesButton))
            {
                return AccessibilityShellZone.Tools;
            }

            if (IsDescendantOf(dependencyObject, StatusBarRow) ||
                IsDescendantOf(dependencyObject, ModeUsageButton) ||
                IsDescendantOf(dependencyObject, CompanionButton) ||
                IsDescendantOf(dependencyObject, AccessibilityMenuButton) ||
                IsDescendantOf(dependencyObject, ProfileStatusButton))
            {
                return AccessibilityShellZone.Companion;
            }

            if (IsDescendantOf(dependencyObject, CurrentAccessibilityContentPanel()))
            {
                return AccessibilityShellZone.Content;
            }
        }

        return AccessibilityShellZones[Math.Clamp(_lastAccessibilityShellZoneIndex, 0, AccessibilityShellZones.Length - 1)].Zone;
    }

    private string DescribeAccessibilityCurrentSurface()
    {
        var panel = CurrentAccessibilityContentPanel();
        if (ReferenceEquals(panel, BrowserPanel))
        {
            var tab = CurrentTab();
            var title = tab?.Title ?? "Accueil Lumora";
            var host = DescribeAccessibilityHost(tab?.Address);
            return string.IsNullOrWhiteSpace(host)
                ? $"Surface : {title}."
                : $"Surface : {title}, sur {host}.";
        }

        if (ReferenceEquals(panel, SettingsPanel))
        {
            return $"Surface : Paramètres, section {CurrentSettingsSectionLabel()}.";
        }

        return $"Surface : {DescribeAccessibilityPanelLabel(panel)}.";
    }

    private string DescribeAccessibilityFocusedControl()
    {
        var focused = Content?.XamlRoot is null ? null : FocusManager.GetFocusedElement(Content.XamlRoot);
        if (focused is not FrameworkElement element)
        {
            return string.Empty;
        }

        var label = AutomationProperties.GetName(element);
        if (string.IsNullOrWhiteSpace(label) &&
            element is ContentControl { Content: string content } &&
            !string.IsNullOrWhiteSpace(content))
        {
            label = content;
        }

        if (string.IsNullOrWhiteSpace(label) &&
            element is ToggleSwitch toggle)
        {
            label = toggle.Header?.ToString();
        }

        if (string.IsNullOrWhiteSpace(label) &&
            element is ComboBox combo)
        {
            label = combo.Header?.ToString();
        }

        if (string.IsNullOrWhiteSpace(label) &&
            element is TextBox textBox)
        {
            label = textBox.PlaceholderText;
        }

        if (string.IsNullOrWhiteSpace(label) &&
            element is AutoSuggestBox autoSuggestBox)
        {
            label = autoSuggestBox.PlaceholderText;
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            label = element.Name;
        }

        return string.IsNullOrWhiteSpace(label)
            ? string.Empty
            : $"Contrôle cible : {label}.";
    }

    private string CurrentSettingsSectionLabel()
    {
        if (SettingsSectionAccessibility.Visibility == Visibility.Visible) return "Confort";
        if (SettingsSectionAppearance.Visibility == Visibility.Visible) return "Apparence";
        if (SettingsSectionNavigation.Visibility == Visibility.Visible) return "Navigation";
        if (SettingsSectionStartup.Visibility == Visibility.Visible) return "Démarrage";
        if (SettingsSectionVault.Visibility == Visibility.Visible) return "Coffre";
        if (SettingsSectionProfile.Visibility == Visibility.Visible) return "Profil";
        if (SettingsSectionStorage.Visibility == Visibility.Visible) return "Stockage";
        if (SettingsSectionPrivacy.Visibility == Visibility.Visible) return "Confidentialité";

        return "Vue générale";
    }

    private string DescribeAccessibilityPanelLabel(FrameworkElement panel) =>
        panel switch
        {
            _ when ReferenceEquals(panel, AboutPanel) => "À propos de Lumora",
            _ when ReferenceEquals(panel, BookmarksPanel) => "Favoris",
            _ when ReferenceEquals(panel, ImportPanel) => "Import et export des favoris",
            _ when ReferenceEquals(panel, HistoryPanel) => "Historique",
            _ when ReferenceEquals(panel, DownloadsPanel) => "Téléchargements",
            _ when ReferenceEquals(panel, SavedTabGroupsPanel) => "Groupes enregistrés",
            _ when ReferenceEquals(panel, NotesPanel) => "Notes",
            _ when ReferenceEquals(panel, VaultPanel) => "Gestionnaire de mots de passe",
            _ when ReferenceEquals(panel, PasskeysPanel) => "Clés d'accès",
            _ when ReferenceEquals(panel, ModulesPanel) => "Modules Lumora",
            _ when ReferenceEquals(panel, SiteControlPanel) => "Centre du site",
            _ when ReferenceEquals(panel, SessionsPanel) => "Sites connectés",
            _ when ReferenceEquals(panel, WalletPanel) => "Portefeuille",
            _ when ReferenceEquals(panel, WebAppsPanel) => "Applications",
            _ when ReferenceEquals(panel, ReadingLensPanel) => "Loupe de lecture",
            _ => "Panneau interne"
        };

    private static string DescribeAccessibilityHost(string? address)
    {
        if (Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
            !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        return string.Empty;
    }
}
