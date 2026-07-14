using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Lumora.WinUI;

// ── Suggestions de la barre d'adresse ────────────────────────────────────────
// Pendant la frappe, propose les onglets ouverts, favoris et pages de
// l'historique local qui correspondent (moteur pur : AddressSuggestionEngine).
// Calcul 100% local : la frappe n'est jamais envoyée à un service distant.
// Flèches pour parcourir, Entrée pour ouvrir, Échap pour revenir au texte tapé.
public sealed partial class MainWindow
{
    private sealed record AddressSuggestionDisplay(
        string Glyph,
        string Title,
        string UrlDisplay,
        string KindLabel,
        AddressSuggestion Suggestion);

    private readonly ObservableCollection<AddressSuggestionDisplay> _addressSuggestionItems = new();
    private bool _suppressAddressSuggestions;
    private bool _addressSuggestionsPointerInside;
    private string _addressSuggestionTypedText = string.Empty;

    private void AddressBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressAddressSuggestions) return;
        // Les mises à jour programmatiques (changement d'onglet, navigation)
        // écrivent dans la barre sans qu'elle ait le focus : pas de popup.
        if (AddressBox.FocusState == FocusState.Unfocused) return;

        UpdateAddressSuggestions();
    }

    private void AddressBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // Un clic sur une suggestion retire d'abord le focus de la barre : ne pas
        // fermer le popup avant que ItemClick ait pu s'exécuter.
        if (_addressSuggestionsPointerInside) return;
        CloseAddressSuggestions();
    }

    private void UpdateAddressSuggestions()
    {
        if (!_uiSettings.AddressBarSuggestionsEnabled)
        {
            CloseAddressSuggestions();
            return;
        }

        _addressSuggestionTypedText = AddressBox.Text ?? string.Empty;
        var suggestions = AddressSuggestionEngine.Suggest(
            _addressSuggestionTypedText,
            CollectAddressSuggestionCandidates(),
            DateTimeOffset.Now);

        if (suggestions.Count == 0)
        {
            CloseAddressSuggestions();
            return;
        }

        _addressSuggestionItems.Clear();
        foreach (var suggestion in suggestions)
        {
            _addressSuggestionItems.Add(new AddressSuggestionDisplay(
                suggestion.Kind switch
                {
                    AddressSuggestionKind.OpenTab => "",
                    AddressSuggestionKind.Bookmark => "",
                    _ => ""
                },
                suggestion.Title,
                DisplayAddressForBar(suggestion.Url),
                suggestion.Kind switch
                {
                    AddressSuggestionKind.OpenTab => "Onglet ouvert",
                    AddressSuggestionKind.Bookmark => "Favori",
                    _ => "Historique"
                },
                suggestion));
        }

        AddressSuggestionsList.SelectedIndex = -1;
        AddressSuggestionsSurface.Width = Math.Max(AddressBox.ActualWidth, 320);
        AddressSuggestionsPopup.IsOpen = true;
    }

    private IEnumerable<AddressSuggestionCandidate> CollectAddressSuggestionCandidates()
    {
        var currentTabId = CurrentTab()?.Id;
        foreach (var tab in _tabs)
        {
            if (tab.Id == currentTabId) continue;
            yield return new AddressSuggestionCandidate(
                AddressSuggestionKind.OpenTab, tab.Title, tab.Address, TabId: tab.Id);
        }

        foreach (var node in _allBookmarkNodes)
        {
            if (node.Kind != BookmarkKind.Url) continue;
            yield return new AddressSuggestionCandidate(
                AddressSuggestionKind.Bookmark, node.Title, node.Url);
        }

        var historyCandidates = AddressSuggestionEngine.AggregateHistory(
            _historyPanel.Store.AllEntries().Select(entry => (entry.Url, entry.Title, entry.VisitedAt)));
        foreach (var candidate in historyCandidates)
        {
            yield return candidate;
        }
    }

    private void CloseAddressSuggestions()
    {
        AddressSuggestionsPopup.IsOpen = false;
        _addressSuggestionsPointerInside = false;
    }

    // Retourne vrai si la touche a été consommée par le popup de suggestions.
    private bool HandleAddressSuggestionsKey(KeyRoutedEventArgs e)
    {
        if (!AddressSuggestionsPopup.IsOpen) return false;

        switch (e.Key)
        {
            case VirtualKey.Down:
                MoveAddressSuggestionSelection(+1);
                return true;

            case VirtualKey.Up:
                MoveAddressSuggestionSelection(-1);
                return true;

            case VirtualKey.Escape:
                SetAddressBoxTextSilently(_addressSuggestionTypedText);
                CloseAddressSuggestions();
                return true;

            case VirtualKey.Enter when AddressSuggestionsList.SelectedItem is AddressSuggestionDisplay selected:
                ApplyAddressSuggestion(selected);
                return true;

            case VirtualKey.Enter:
                // Aucune suggestion choisie : navigation normale, popup refermé.
                CloseAddressSuggestions();
                return false;

            default:
                return false;
        }
    }

    // La sélection suit les flèches et recopie l'adresse dans la barre (comme
    // Chrome/Firefox) ; remonter au-dessus de la liste restaure le texte tapé.
    private void MoveAddressSuggestionSelection(int delta)
    {
        var count = _addressSuggestionItems.Count;
        if (count == 0) return;

        var index = AddressSuggestionsList.SelectedIndex + delta;
        if (index >= count) index = count - 1;

        if (index < 0)
        {
            AddressSuggestionsList.SelectedIndex = -1;
            SetAddressBoxTextSilently(_addressSuggestionTypedText);
            return;
        }

        AddressSuggestionsList.SelectedIndex = index;
        AddressSuggestionsList.ScrollIntoView(AddressSuggestionsList.SelectedItem);
        SetAddressBoxTextSilently(_addressSuggestionItems[index].UrlDisplay);
    }

    private void SetAddressBoxTextSilently(string text)
    {
        _suppressAddressSuggestions = true;
        try
        {
            AddressBox.Text = text;
            AddressBox.SelectionStart = text.Length;
        }
        finally
        {
            _suppressAddressSuggestions = false;
        }
    }

    private void AddressSuggestionsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AddressSuggestionDisplay item)
        {
            ApplyAddressSuggestion(item);
        }
    }

    private void AddressSuggestions_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        _addressSuggestionsPointerInside = true;

    private void AddressSuggestions_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _addressSuggestionsPointerInside = false;
        // Si la barre a déjà perdu le focus (clic raté à côté d'une suggestion),
        // le popup ne doit pas rester ouvert.
        if (AddressBox.FocusState == FocusState.Unfocused)
        {
            CloseAddressSuggestions();
        }
    }

    private void ApplyAddressSuggestion(AddressSuggestionDisplay item)
    {
        CloseAddressSuggestions();

        var suggestion = item.Suggestion;
        if (suggestion.Kind == AddressSuggestionKind.OpenTab)
        {
            var tab = _tabs.FirstOrDefault(candidate => candidate.Id == suggestion.TabId);
            if (tab is not null)
            {
                SelectTab(tab);
                return;
            }
        }

        SetAddressBoxTextSilently(DisplayAddressForBar(suggestion.Url));
        NavigateCurrentTab(suggestion.Url, suggestion.Title);
        ShowPanel(BrowserPanel, suggestion.Title);
    }
}
