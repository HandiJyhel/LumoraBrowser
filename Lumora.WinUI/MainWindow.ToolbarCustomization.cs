using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Lumora.WinUI;

/// <summary>
/// Réorganisation de la barre d'outils (MainWindow partial). Fenêtre dédiée
/// (overlay modal, <see cref="ToolbarReorganizeOverlay"/> dans MainWindow.xaml),
/// ouverte depuis le menu Modules ("Réorganiser toute la barre…"), listant
/// TOUT ce qui est réordonnable dans la barre - pas seulement les modules.
///
/// Remplace entièrement l'ancien glisser-déposer (2026-08-14, demande
/// explicite utilisateur : le clic droit dédié "ne sert à rien puisque le
/// glisser ne fonctionne pas"). 3 bugs réels trouvés et corrigés sur ce
/// mécanisme en une seule session (zone de déplacement de fenêtre, boutons
/// masqués, capture perdue même différée au relâchement) sans jamais
/// aboutir à un résultat fiable en conditions réelles - voir MEMORY.md.
/// Un déplacement au clic élimine complètement la classe de bug (aucune
/// capture de pointeur en jeu) - même principe déjà livré pour les favoris
/// (BookmarkStore.MoveNodeAdjacent).
///
/// 2e passe (2026-08-14, même jour) : la 1re version n'affichait que les 16
/// boutons de <c>ModulesQuickBar</c>, laissant de côté favoris/coffre/
/// bouclier/téléchargements/historique/incognito - retour utilisateur direct
/// ("le bouton des favoris du coffre et de tout le reste, ça compte"). Ces 6
/// boutons ont été déplacés de leurs Grid.Column fixes (NavigationToolbar)
/// vers <c>ToolbarButtonsPanel</c> (voir MainWindow.xaml) pour devenir
/// réordonnables comme les modules. Vu le nombre d'éléments résultant, la
/// petite liste intégrée au menu Modules a été remplacée par cette fenêtre
/// dédiée, groupée en 2 sections (2e maquette validée par l'utilisateur) :
/// "Toujours dans la barre" (tout ce qui est visible sans épinglage) et
/// "Modules épinglés" (le sous-ensemble optionnel, contrôlé par
/// <see cref="OptionalPinnedButtonIds"/> - même liste que UpdateModulesPinUi,
/// MainWindow.UsageMode.cs).
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// Nom d'affichage + glyphe Segoe MDL2 Assets de chaque bouton
    /// réordonnable de la barre d'outils. Repris des AutomationProperties.Name/
    /// FontIcon déjà utilisés sur chaque bouton (MainWindow.xaml) - simplifiés
    /// pour les 2 boutons dont le texte accessible change selon l'état
    /// runtime (ConsentIndicatorButton, PopupRecoveryButton), qui ne
    /// conviendrait pas tel quel comme libellé stable dans une liste.
    /// </summary>
    private static readonly (string Id, string Label, string Glyph)[] ToolbarButtonCatalog =
    {
        ("AddBookmarkButton", "Ajouter aux favoris", ""),
        ("IncognitoToolbarButton", "Incognito", ""),
        ("ShieldButton", "Confidentialité du site", ""),
        ("VaultQuickAccessButton", "Identifiants de ce site", ""),
        ("HistoryToolbarButton", "Historique", ""),
        ("DownloadsIndicatorButton", "Téléchargements", ""),
        ("ReaderModeButton", "Mode lecture", ""),
        ("NotesModuleButton", "Notes et annotations", ""),
        ("ReadAloudButton", "Lecture à voix haute", ""),
        ("DetachVideoPinnedButton", "Détacher la vidéo", ""),
        ("VideoDownloadButton", "Télécharger la vidéo", ""),
        ("TranslatePinnedButton", "Traduction locale", ""),
        ("WebAppsPinnedButton", "Applications web", ""),
        ("SearchAssistButton", "Assistant de recherche", ""),
        ("DictationPinnedButton", "Dictée vocale", ""),
        ("RssModuleButton", "Flux RSS", ""),
        ("ReadingLensButton", "Loupe de lecture", ""),
        ("ModulesQuickAccessButton", "Modules épinglés", ""),
        ("ConnectionsQuickAccessButton", "Connexions persistantes", ""),
        ("ConsentIndicatorButton", "Confidentialité (cookies)", ""),
        ("PopupRecoveryButton", "Popups en attente", ""),
        ("SplitViewButton", "Vue partagée", ""),
    };

    // Modules optionnels pilotes par l'epinglage (memes id que
    // _uiSettings.PinnedModuleIds/UpdateModulesPinUi, MainWindow.UsageMode.cs)
    // - tout le reste de ce qui est VISIBLE dans la barre est "toujours la",
    // sans epinglage a faire.
    private static readonly HashSet<string> OptionalPinnedButtonIds = new(StringComparer.Ordinal)
    {
        "ReaderModeButton", "NotesModuleButton", "ReadAloudButton", "DetachVideoPinnedButton",
        "VideoDownloadButton", "TranslatePinnedButton", "WebAppsPinnedButton", "SearchAssistButton",
        "DictationPinnedButton", "RssModuleButton", "ReadingLensButton"
    };

    private static string ToolbarButtonLabel(string buttonId) =>
        ToolbarButtonCatalog.FirstOrDefault(entry => entry.Id == buttonId).Label is { Length: > 0 } label
            ? label
            : buttonId;

    private static string ToolbarButtonGlyph(string buttonId) =>
        ToolbarButtonCatalog.FirstOrDefault(entry => entry.Id == buttonId).Glyph is { Length: > 0 } glyph
            ? glyph
            : "";

    /// <summary>
    /// Ouvre la fenêtre de réorganisation (lien "Réorganiser toute la
    /// barre…" au fond du menu Modules).
    /// </summary>
    private void ToolbarReorganizeMenuLink_Click(object sender, RoutedEventArgs e)
    {
        RefreshToolbarReorganizeSections();
        ToolbarReorganizeOverlay.Visibility = Visibility.Visible;
    }

    private void ToolbarReorganizeDoneButton_Click(object sender, RoutedEventArgs e)
    {
        ToolbarReorganizeOverlay.Visibility = Visibility.Collapsed;
    }

    private void ToolbarReorganizeResetButton_Click(object sender, RoutedEventArgs e)
    {
        _toolbarCustomization.ResetToDefault();
        DispatcherQueue.TryEnqueue(RefreshToolbarReorganizeSections);
    }

    /// <summary>
    /// (Re)construit les 2 sections de la fenêtre à partir de l'ordre
    /// visible actuel (ToolbarCustomizationService.GetVisibleButtonOrder) -
    /// un bouton masqué (module non épinglé) n'apparaît dans aucune des deux.
    /// Appelée à l'ouverture, après chaque Monter/Descendre (différé via
    /// DispatcherQueue - voir les gestionnaires de clic plus bas, même
    /// précaution que pour l'ancien mécanisme : reconstruire la liste PENDANT
    /// que le bouton cliqué traite encore son propre Click perturbe le
    /// parent) et, si la fenêtre est déjà ouverte, après un épinglage/
    /// désépinglage ailleurs (UpdateModulesPinUi, MainWindow.UsageMode.cs).
    /// </summary>
    private void RefreshToolbarReorganizeSections()
    {
        if (ToolbarReorganizeAlwaysSection is null || ToolbarReorganizeModulesSection is null) return;

        var visibleOrder = _toolbarCustomization.GetVisibleButtonOrder();
        var always = visibleOrder.Where(id => !OptionalPinnedButtonIds.Contains(id)).ToList();
        var pinned = visibleOrder.Where(id => OptionalPinnedButtonIds.Contains(id)).ToList();

        FillReorganizeSection(ToolbarReorganizeAlwaysSection, always, ToolbarReorganizeAlwaysCount);
        FillReorganizeSection(ToolbarReorganizeModulesSection, pinned, ToolbarReorganizeModulesCount);

        ToolbarReorganizeModulesEmptyHint.Visibility = pinned.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FillReorganizeSection(Panel host, IReadOnlyList<string> ids, TextBlock countText)
    {
        host.Children.Clear();
        countText.Text = ids.Count.ToString();

        for (var i = 0; i < ids.Count; i++)
        {
            host.Children.Add(CreateToolbarReorganizeRow(ids[i], isFirst: i == 0, isLast: i == ids.Count - 1));
        }
    }

    private Grid CreateToolbarReorganizeRow(string buttonId, bool isFirst, bool isLast)
    {
        var row = new Grid
        {
            ColumnSpacing = 11,
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(11)
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        var chip = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(9),
            Background = (Brush)RootShell.Resources["NovaChromeButtonBackgroundBrush"],
            BorderBrush = (Brush)RootShell.Resources["NovaChromeStrokeBrush"],
            BorderThickness = new Thickness(1)
        };
        var icon = new FontIcon
        {
            Glyph = ToolbarButtonGlyph(buttonId),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        chip.Child = icon;
        Grid.SetColumn(chip, 0);

        var label = ToolbarButtonLabel(buttonId);
        var name = new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(name, 1);

        var steppers = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Background = (Brush)RootShell.Resources["NovaChromeButtonBackgroundBrush"],
            BorderBrush = (Brush)RootShell.Resources["NovaChromeStrokeBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9)
        };
        var upButton = new Button
        {
            Content = new FontIcon { Glyph = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 11 },
            Tag = buttonId,
            IsEnabled = !isFirst,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            Padding = new Thickness(0),
            Width = 28,
            Height = 28
        };
        upButton.Click += ToolbarReorganizeUpButton_Click;
        ApplyNovaControlAccessibility(upButton, $"Monter {label} dans la barre");

        var downButton = new Button
        {
            Content = new FontIcon { Glyph = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 11 },
            Tag = buttonId,
            IsEnabled = !isLast,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(1, 0, 0, 0),
            BorderBrush = (Brush)RootShell.Resources["NovaChromeStrokeBrush"],
            CornerRadius = new CornerRadius(0),
            Padding = new Thickness(0),
            Width = 28,
            Height = 28
        };
        downButton.Click += ToolbarReorganizeDownButton_Click;
        ApplyNovaControlAccessibility(downButton, $"Descendre {label} dans la barre");

        steppers.Children.Add(upButton);
        steppers.Children.Add(downButton);
        Grid.SetColumn(steppers, 2);

        row.Children.Add(chip);
        row.Children.Add(name);
        row.Children.Add(steppers);
        return row;
    }

    // Rafraichissement DIFFERE (DispatcherQueue.TryEnqueue), pas synchrone
    // dans le gestionnaire de clic (2026-08-14, bug reel trouve en
    // verification UIA reelle sur la 1re version de cette fonction) :
    // reconstruire la section PENDANT que le bouton clique lui-meme est
    // encore en train de traiter son propre Click perturbait le conteneur
    // parent. Reordonnancement reel confirme fonctionner (verifie par la
    // position ecran reelle des boutons de la barre).
    private void ToolbarReorganizeUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string buttonId }) return;
        if (_toolbarCustomization.MoveButtonAdjacent(buttonId, moveForward: false))
        {
            DispatcherQueue.TryEnqueue(RefreshToolbarReorganizeSections);
        }
    }

    private void ToolbarReorganizeDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string buttonId }) return;
        if (_toolbarCustomization.MoveButtonAdjacent(buttonId, moveForward: true))
        {
            DispatcherQueue.TryEnqueue(RefreshToolbarReorganizeSections);
        }
    }
}
