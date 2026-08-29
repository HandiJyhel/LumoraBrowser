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
        AddressSuggestion Suggestion)
    {
        // Nom accessible unique : sans ca, un lecteur d'ecran lit Title,
        // UrlDisplay et KindLabel comme trois TextBlock separes.
        public string AccessibleName => $"{KindLabel} : {Title}, {UrlDisplay}";
    }

    private readonly ObservableCollection<AddressSuggestionDisplay> _addressSuggestionItems = new();
    private bool _suppressAddressSuggestions;
    private bool _addressSuggestionsPointerInside;
    private string _addressSuggestionTypedText = string.Empty;
    // Delai de grace avant fermeture du popup de suggestions : sans ca, la
    // perte de focus de la barre (qui precede le survol du popup pendant un
    // trajet de souris normal) fermait le popup avant que le pointeur ait pu
    // l'atteindre - signale par l'utilisateur le 2026-07-29 ("on peut pas se
    // deplacer avec la souris"). 280ms : assez court pour ne pas laisser un
    // popup fantome trainer, assez long pour un trajet de souris normal vers
    // la premiere ligne de suggestion.
    private static readonly TimeSpan AddressSuggestionsCloseGraceDelay = TimeSpan.FromMilliseconds(280);
    private DispatcherTimer? _addressSuggestionsCloseGraceTimer;

    private void AddressBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateAddressIdentityChrome(AddressBox.Text);

        if (_suppressAddressSuggestions) return;
        // Les mises à jour programmatiques (changement d'onglet, navigation)
        // écrivent dans la barre sans qu'elle ait le focus : pas de popup.
        if (AddressBox.FocusState == FocusState.Unfocused) return;

        // Instrumentation (2026-08-24, diagnostic "curseur disparait, impossible
        // d'ecrire" signale apres le correctif du vol de focus) : mesure le temps
        // reellement passe dans UpdateAddressSuggestions, avec les VRAIES donnees
        // (onglets/favoris/historique) - a servi a ecarter un blocage du thread UI
        // (cause reelle = AddressBox_LostFocus, voir plus bas). Gardee (comme le
        // "Diagnostic focus Win32" plus ancien, MainWindow.WindowChrome.cs) : cout
        // nul hors LUMORA_TRACE_STARTUP=1, utile si ce type de symptome revient.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        UpdateAddressSuggestions();
        sw.Stop();
        WinUiRuntimeTrace.Write($"AddressBox_TextChanged: longueur={AddressBox.Text?.Length ?? 0} UpdateAddressSuggestions={sw.ElapsedMilliseconds}ms popupOuvert={AddressSuggestionsPopup.IsOpen}");
    }

    // Bascule vers l'URL complète pour l'édition dès que la barre reçoit le focus
    // (clic, Tab, Ctrl+L...) - l'affichage au repos (DisplayAddressForBar,
    // MainWindow.xaml.cs) est simplifié depuis le 2026-08-22 (domaine + chemin, sans
    // requête). SelectAll() en plus : même convention que Chrome/Firefox/Edge.
    //
    // Repris en profondeur le 2026-08-24 (3 correctifs au coup par coup dans la même
    // session - survol, alt-tab, fermeture du popup de suggestions - sans garantie
    // qu'une 4e cause n'existe pas) : ce handler n'écrase plus JAMAIS une édition en
    // cours. La seule question posée est structurelle, pas "pourquoi le focus est-il
    // revenu ?" : la barre affiche-t-elle encore exactement son état AU REPOS (le
    // texte simplifié de la page réelle) ? Si oui, c'est un focus neuf → on bascule
    // sur l'URL complète pour éditer. Si non, une édition était déjà en cours et a
    // simplement été interrompue (alt-tab, effet de bord d'un Popup qui se ferme, ou
    // toute autre cause pas encore rencontrée) → on la laisse intacte, sans avoir
    // besoin de connaître la cause précise de l'interruption.
    private void AddressBox_GotFocus(object sender, RoutedEventArgs e)
    {
        var rawAddress = CurrentTab()?.Address;
        if (string.IsNullOrWhiteSpace(rawAddress) || rawAddress.Equals("lumora://accueil", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (AddressBox.Text != DisplayAddressForBar(rawAddress))
        {
            return;
        }

        _suppressAddressSuggestions = true;
        AddressBox.Text = rawAddress;
        _suppressAddressSuggestions = false;
        AddressBox.SelectAll();
    }

    // Ne touche plus au texte tapé (voir la note sur AddressBox_GotFocus) : une perte
    // de focus, quelle qu'en soit la cause, ne signifie plus "l'utilisateur abandonne
    // sa saisie". Seul un signal sans ambiguïté le fait desormais : un vrai clic
    // ailleurs dans l'app (RootPointerPressed, MainWindow.xaml.cs) ou Échap
    // (AddressBox_KeyDown, MainWindow.Navigation.cs) - les deux reecrivent le texte
    // AVANT de faire perdre le focus, donc ce handler peut s'en servir comme signal :
    // si le texte affiche represente encore une edition en cours au moment ou ce
    // LostFocus se declenche, rien ne l'a explicitement abandonnee - la perte de
    // focus est un pur accident technique (survol - deja bloque a la source pendant
    // l'edition -, alt-tab, fermeture d'un popup de suggestions, ou une cause pas
    // encore rencontree) : on reprend le focus pour que la frappe continue sans que
    // l'utilisateur ait besoin de recliquer, jamais le contenu (deja protege par la
    // regle structurelle de AddressBox_GotFocus). `DispatcherQueue.TryEnqueue` car un
    // `Focus()` synchrone a l'interieur du handler LostFocus lui-meme peut etre
    // ignore par WinUI (reentrance) - verifie en direct, la 1ere version de ce
    // correctif tentait la reprise trop tot (avant la perte reelle, qui est
    // asynchrone) et ne se declenchait jamais.
    private void AddressBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // Un clic sur une suggestion retire d'abord le focus de la barre : ne pas
        // fermer le popup avant que ItemClick ait pu s'exécuter, ni revenir à
        // l'affichage simplifié avant que la navigation choisie ait eu lieu.
        if (_addressSuggestionsPointerInside) return;
        ScheduleAddressSuggestionsClose();

        var rawAddress = CurrentTab()?.Address;
        var stillEditing = !string.IsNullOrWhiteSpace(rawAddress) &&
            AddressBox.Text != DisplayAddressForBar(rawAddress);

        if (stillEditing)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (AddressBox.FocusState == FocusState.Unfocused)
                {
                    AddressBox.Focus(FocusState.Programmatic);
                }
            });
        }
    }

    // Barre au repos (hors édition) : revient à l'affichage simplifié de la page
    // réellement chargée - même convention que Chrome/Firefox/Edge (annuler une
    // saisie non validée). N'est plus appelée que sur un signal de sortie sans
    // ambiguïté (RootPointerPressed, Échap) - jamais depuis AddressBox_LostFocus.
    private void RevertAddressBarToSimplifiedDisplay()
    {
        var address = CurrentTab()?.Address;
        if (string.IsNullOrWhiteSpace(address)) return;

        var displayAddress = DisplayAddressForBar(address);
        if (AddressBox.Text != displayAddress) AddressBox.Text = displayAddress;
    }

    // Seul declencheur restant d'un abandon volontaire de la saisie en cours dans la
    // barre d'adresse (voir AddressBox_GotFocus/LostFocus) : un vrai clic (bouton de
    // pointeur enfonce), n'importe ou ailleurs dans l'app, PENDANT que la barre est en
    // cours d'edition. Abonne via `AddHandler(..., handledEventsToo: true)`
    // (MainWindow.xaml.cs) pour voir tous les clics, meme ceux deja marques geres par
    // un bouton/TextBox enfant. Chrome/Firefox/Edge se comportent pareil : cliquer
    // ailleurs DANS le navigateur annule une saisie non validee, mais rien d'autre
    // (survol, alt-tab, effets de bord internes) n'y touche plus.
    private void RootPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (AddressBox.FocusState == FocusState.Unfocused && !AddressSuggestionsPopup.IsOpen) return;

        if (e.OriginalSource is DependencyObject source &&
            (IsDescendantOf(source, AddressBox) || IsDescendantOf(source, AddressSuggestionsSurface)))
        {
            return;
        }

        CloseAddressSuggestions();
        RevertAddressBarToSimplifiedDisplay();
    }

    // Ferme le popup apres un court delai plutot qu'immediatement, pour laisser
    // le temps a un trajet de souris normal (barre -> popup) d'y arriver avant
    // qu'il disparaisse. Annule tout seul si le pointeur entre dans le popup ou
    // si la barre reprend le focus avant l'echeance (re-verifie au Tick).
    private void ScheduleAddressSuggestionsClose()
    {
        if (!AddressSuggestionsPopup.IsOpen) return;

        _addressSuggestionsCloseGraceTimer ??= new DispatcherTimer { Interval = AddressSuggestionsCloseGraceDelay };
        _addressSuggestionsCloseGraceTimer.Tick -= AddressSuggestionsCloseGraceTimer_Tick;
        _addressSuggestionsCloseGraceTimer.Tick += AddressSuggestionsCloseGraceTimer_Tick;
        _addressSuggestionsCloseGraceTimer.Stop();
        _addressSuggestionsCloseGraceTimer.Start();
    }

    private void AddressSuggestionsCloseGraceTimer_Tick(object? sender, object e)
    {
        _addressSuggestionsCloseGraceTimer?.Stop();
        if (_addressSuggestionsPointerInside) return;
        if (AddressBox.FocusState != FocusState.Unfocused) return;
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
        _addressSuggestionsCloseGraceTimer?.Stop();
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
        // le popup ne doit pas rester ouvert - mais laisse le même délai de grâce
        // qu'ailleurs plutôt qu'une fermeture instantanée (ex. sortie transitoire
        // du popup en visant une ligne proche du bord).
        if (AddressBox.FocusState == FocusState.Unfocused)
        {
            ScheduleAddressSuggestionsClose();
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
