using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Lumora.WinUI;

// ── Groupes d'onglets enregistrés ────────────────────────────────────────────
// Permet de « ranger » un groupe d'onglets dans une bibliothèque locale et de le
// rouvrir plus tard, même après avoir fermé ses onglets. Répond au vrai manque
// des navigateurs actuels : un groupe patiemment constitué disparaît des qu'on
// le ferme. Ici, il reste retrouvable. 100% local, fichier chiffré du profil.
public sealed partial class MainWindow
{
    // Snapshot en attente pour le garde-fou : capturé au moment ou un groupe non
    // enregistré va disparaître (les onglets peuvent deja etre fermes ensuite).
    private (string Name, int ColorIndex, List<SavedTabGroupTab> Tabs)? _pendingKeepGroup;

    // ── Enregistrement explicite depuis l'en-tête du groupe ───────────────────

    private void SaveGroupToLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: TabGroup group }) return;
        if (SaveLiveGroup(group))
        {
            StatusText.Text = $"Groupe « {group.Name} » enregistre. Retrouvez-le dans Naviguer > Groupes enregistres.";
        }
        else
        {
            StatusText.Text = "Ce groupe n'a aucune page enregistrable (seulement l'accueil).";
        }
    }

    // Enregistre l'etat actuel d'un groupe vivant. Retourne false si rien
    // d'enregistrable (que des pages internes).
    private bool SaveLiveGroup(TabGroup group)
    {
        var tabs = _tabs
            .Where(tab => tab.GroupId == group.Id)
            .Select(tab => new SavedTabGroupTab(tab.Title, tab.Address))
            .ToList();

        var saved = _savedTabGroups.Save(group.Name, group.ColorIndex, tabs, DateTimeOffset.Now);
        if (saved is null) return false;

        _savedGroupIds.Add(group.Id);
        return true;
    }

    // ── Garde-fou : proposer de garder un groupe avant qu'il disparaisse ──────

    // Appelé quand un groupe non enregistré est sur le point de disparaître
    // (dissolution, ou fermeture de son dernier onglet). Ne redemande pas pour un
    // groupe déjà range.
    private void OfferKeepGroupIfUnsaved(TabGroup group)
    {
        if (_isGuestMode || _savedGroupIds.Contains(group.Id)) return;

        var tabs = _tabs
            .Where(tab => tab.GroupId == group.Id)
            .Select(tab => new SavedTabGroupTab(tab.Title, tab.Address))
            .Where(tab => SavedTabGroupStore.IsSavableUrl(tab.Url))
            .ToList();
        if (tabs.Count == 0) return;

        _pendingKeepGroup = (group.Name, group.ColorIndex, tabs);
        SaveGroupText.Text = $"Garder le groupe « {group.Name} » ({tabs.Count} onglet(s)) pour le retrouver plus tard ?";
        SaveGroupBar.Visibility = Visibility.Visible;
    }

    private void SaveGroupAccept_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingKeepGroup is { } pending)
        {
            var saved = _savedTabGroups.Save(pending.Name, pending.ColorIndex, pending.Tabs, DateTimeOffset.Now);
            StatusText.Text = saved is not null
                ? $"Groupe « {pending.Name} » garde. Naviguer > Groupes enregistres."
                : "Rien a enregistrer dans ce groupe.";
        }

        DismissSaveGroupBar();
    }

    private void SaveGroupDismiss_Click(object sender, RoutedEventArgs e) => DismissSaveGroupBar();

    private void DismissSaveGroupBar()
    {
        _pendingKeepGroup = null;
        SaveGroupBar.Visibility = Visibility.Collapsed;
    }

    // ── Bibliothèque : panneau « Groupes enregistrés » ────────────────────────

    private void SavedTabGroupsMenu_Click(object sender, RoutedEventArgs e)
    {
        RenderSavedTabGroups();
        ShowPanel(SavedTabGroupsPanel, "Groupes enregistres");
    }

    private void RenderSavedTabGroups()
    {
        SavedTabGroupsPanelItems.Children.Clear();
        var groups = _savedTabGroups.Groups;
        if (groups.Count == 0)
        {
            SavedTabGroupsPanelItems.Children.Add(new TextBlock
            {
                Text = "Aucun groupe enregistre pour l'instant. Depuis un groupe d'onglets, choisissez « Enregistrer le groupe » pour le retrouver ici.",
                Opacity = 0.65,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        foreach (var group in groups)
        {
            SavedTabGroupsPanelItems.Children.Add(BuildSavedGroupCard(group));
        }
    }

    private UIElement BuildSavedGroupCard(SavedTabGroup group)
    {
        var card = new Border
        {
            Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(8),
            Background = (Brush)RootShell.Resources["NovaChromeSurfaceRaisedBrush"],
            BorderBrush = (Brush)RootShell.Resources["NovaChromeStrokeBrush"],
            BorderThickness = new Thickness(1)
        };

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Pastille de couleur du groupe (même palette que les groupes vivants).
        var colorDot = new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(
                TabGroupPalette[((group.ColorIndex % TabGroupPalette.Length) + TabGroupPalette.Length) % TabGroupPalette.Length])
        };
        Grid.SetColumn(colorDot, 0);
        root.Children.Add(colorDot);

        var infoPanel = new StackPanel { Spacing = 4 };
        infoPanel.Children.Add(new TextBlock
        {
            Text = group.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = AccessibilityBodyFontSize(),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        infoPanel.Children.Add(new TextBlock
        {
            Text = $"{group.TabCount} onglet(s) · enregistre {SavedGroupWhen(group.SavedAt)}",
            Opacity = 0.6,
            FontSize = AccessibilitySecondaryFontSize()
        });
        // Apercu des premieres pages du groupe.
        var preview = string.Join("  ·  ", group.Tabs.Take(4).Select(t => t.Title));
        if (group.Tabs.Count > 4) preview += "  ·  …";
        infoPanel.Children.Add(new TextBlock
        {
            Text = preview,
            Opacity = 0.5,
            FontSize = AccessibilitySecondaryFontSize(),
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetColumn(infoPanel, 1);
        root.Children.Add(infoPanel);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12, 0, 0, 0)
        };
        var openButton = new Button { Content = "Ouvrir", Tag = group.Id, FontSize = AccessibilitySecondaryFontSize() };
        ApplyNovaControlAccessibility(openButton, $"Ouvrir le groupe enregistre {group.Name}");
        openButton.Click += OpenSavedGroup_Click;
        actions.Children.Add(openButton);
        var deleteButton = new Button
        {
            Content = "Supprimer",
            Tag = group.Id,
            FontSize = AccessibilitySecondaryFontSize(),
            Style = (Style)RootShell.Resources["NovaCompactButtonStyle"]
        };
        ApplyNovaControlAccessibility(deleteButton, $"Supprimer le groupe enregistre {group.Name}");
        deleteButton.Click += DeleteSavedGroup_Click;
        actions.Children.Add(deleteButton);
        Grid.SetColumn(actions, 2);
        root.Children.Add(actions);

        card.Child = root;
        return card;
    }

    private static string SavedGroupWhen(DateTimeOffset savedAt)
    {
        var age = DateTimeOffset.Now - savedAt;
        if (age < TimeSpan.FromMinutes(1)) return "a l'instant";
        if (age < TimeSpan.FromHours(1)) return $"il y a {(int)age.TotalMinutes} min";
        if (age < TimeSpan.FromDays(1)) return $"il y a {(int)age.TotalHours} h";
        if (age < TimeSpan.FromDays(30)) return $"il y a {(int)age.TotalDays} j";
        return savedAt.ToLocalTime().ToString("d MMM yyyy");
    }

    private void OpenSavedGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        var group = _savedTabGroups.Find(id);
        if (group is null) return;

        OpenSavedGroup(group);
        ShowPanel(BrowserPanel, CurrentTab()?.Title ?? "Accueil Lumora");
    }

    // Reconstruit un groupe vivant a partir d'une entree de la bibliotheque : un
    // nouveau groupe (meme nom, meme couleur) et un onglet par page enregistree.
    private void OpenSavedGroup(SavedTabGroup group)
    {
        var liveGroup = new TabGroup(_nextGroupId++, group.Name, group.ColorIndex);
        _tabGroups.Add(liveGroup);
        // Le groupe rouvert est deja « connu » de la bibliotheque : ne pas
        // reproposer de le garder si l'utilisateur le referme.
        _savedGroupIds.Add(liveGroup.Id);

        BrowserTabState? first = null;
        foreach (var tab in group.Tabs)
        {
            var state = AddTab(tab.Title, tab.Url, select: false, groupId: liveGroup.Id);
            first ??= state;
        }

        if (first is not null) SelectTab(first);
        StatusText.Text = $"Groupe « {group.Name} » rouvert ({group.TabCount} onglet(s)).";
    }

    private void DeleteSavedGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        if (_savedTabGroups.Remove(id))
        {
            RenderSavedTabGroups();
            StatusText.Text = "Groupe enregistre supprime.";
        }
    }
}
