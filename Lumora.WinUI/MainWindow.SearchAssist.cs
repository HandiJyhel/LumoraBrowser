using Microsoft.UI.Xaml;
using Lumora.WinUI.SearchAssist;

namespace Lumora.WinUI;

// ── Assistant IA de recherche (local) ────────────────────────────────────
// Reformule la requete tapee dans la barre d'adresse via un petit modele de
// langage local (SearchAssistService). Jamais automatique : uniquement sur
// clic, et la suggestion doit etre validee explicitement avant de remplacer
// le texte de la barre d'adresse.
public sealed partial class MainWindow
{
    private readonly SearchAssistService _searchAssistService = new();
    private bool _searchAssistInProgress;

    private void UpdateSearchAssistButtonVisibility()
    {
        if (SearchAssistButton is null) return;
        SearchAssistButton.Visibility = _uiSettings.SearchAssistEnabled
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void SearchAssistButton_Click(object sender, RoutedEventArgs e)
    {
        if (_searchAssistInProgress || !_uiSettings.SearchAssistEnabled) return;

        var query = AddressBox.Text?.Trim();
        if (string.IsNullOrEmpty(query)) return;

        _searchAssistInProgress = true;
        SearchAssistButton.IsEnabled = false;
        var progress = new Progress<string>(msg => StatusText.Text = msg);
        try
        {
            StatusText.Text = "Reflexion en cours (modele local)...";
            var suggestion = await _searchAssistService.RewriteQueryAsync(query, progress);
            if (string.IsNullOrWhiteSpace(suggestion))
            {
                StatusText.Text = "Aucune suggestion generee.";
                return;
            }

            SearchAssistSuggestionText.Text = suggestion;
            SearchAssistFlyout.ShowAt(SearchAssistButton);
            StatusText.Text = "Suggestion prete.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Assistant IA indisponible : {ex.Message}";
        }
        finally
        {
            _searchAssistInProgress = false;
            SearchAssistButton.IsEnabled = true;
        }
    }

    private void SearchAssistUseButton_Click(object sender, RoutedEventArgs e)
    {
        AddressBox.Text = SearchAssistSuggestionText.Text;
        AddressBox.SelectionStart = AddressBox.Text.Length;
        SearchAssistFlyout.Hide();
    }

    private void SearchAssistIgnoreButton_Click(object sender, RoutedEventArgs e) =>
        SearchAssistFlyout.Hide();
}
