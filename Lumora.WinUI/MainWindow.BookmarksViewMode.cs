using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

// Bascule Icônes/Liste/Détails du contenu du gestionnaire de favoris
// (2026-08-31, session "gestion des favoris" - jusqu'ici seule une grille de
// vignettes existait, demande explicite utilisateur : "parfois ça peut être
// plus simple à gérer"). ItemsPanel/ItemTemplate de BookmarksList sont
// permutés en code plutôt que via un DataTemplateSelector : un seul type
// d'objet (BookmarkListItem) est toujours affiché, jamais un mélange de
// types - un simple aiguillage sur _bookmarkViewMode suffit.
public sealed partial class MainWindow
{
    private void BookmarkViewModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        // Meme garde que les autres bascules de reglage (BookmarksBarSwitch_Toggled
        // etc.) : evite une sauvegarde/reapplication prematuree pendant
        // qu'ApplyUiSettings positionne encore les controles au demarrage.
        if (_suppressUiSettingsSave) return;
        if (sender is not RadioButton { Tag: string tag }) return;

        _bookmarkViewMode = NormalizeBookmarkViewMode(tag);
        ApplyBookmarkViewMode();
        SaveWorkspaceUiSettings();
    }

    // Applique _bookmarkViewMode aux controles (ItemsPanel/ItemTemplate de
    // BookmarksList, visibilite de l'en-tete "Détails", etat des 3
    // RadioButton) - appele au chargement des reglages ET a chaque bascule.
    private void ApplyBookmarkViewMode()
    {
        var (panelKey, templateKey) = _bookmarkViewMode switch
        {
            "list" => ("BookmarkRowsItemsPanelTemplate", "BookmarkListItemTemplate"),
            "details" => ("BookmarkRowsItemsPanelTemplate", "BookmarkDetailsItemTemplate"),
            _ => ("BookmarkIconsItemsPanelTemplate", "BookmarkIconsItemTemplate")
        };

        BookmarksList.ItemsPanel = (ItemsPanelTemplate)RootShell.Resources[panelKey];
        BookmarksList.ItemTemplate = (DataTemplate)RootShell.Resources[templateKey];
        BookmarkDetailsHeaderRow.Visibility = _bookmarkViewMode == "details" ? Visibility.Visible : Visibility.Collapsed;

        var radio = _bookmarkViewMode switch
        {
            "list" => BookmarkViewModeListRadio,
            "details" => BookmarkViewModeDetailsRadio,
            _ => BookmarkViewModeIconsRadio
        };
        // Ne force IsChecked que si necessaire : le mettre a "true" alors
        // qu'il l'est deja redeclencherait quand meme l'evenement Checked
        // sur certaines versions de WinUI, pour rien.
        if (!radio.IsChecked.GetValueOrDefault())
        {
            radio.IsChecked = true;
        }
    }
}
