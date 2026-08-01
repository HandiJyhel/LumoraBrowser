using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

// ── Panneau Notes : pages annotées + bloc-notes libre ────────────────────────
// La liste de gauche mélange deux types d'entrées : les pages annotées en mode
// lecture (reprise d'activité — un clic rouvre la page avec ses surlignages)
// et les notes libres. Le détail de droite s'adapte à la sélection : lecteur
// d'annotations ou éditeur de note (sauvegarde automatique différée pendant la
// frappe, vidée immédiatement au changement de sélection). Les données vivent
// dans AnnotationStore (annotations.lumora) et NoteStore (notes.lumora),
// chiffrés DPAPI ; en mode invité, en mémoire de session.
public sealed partial class MainWindow
{
    private List<Note> _visibleNotes = new();
    private List<AnnotatedPage> _visibleAnnotatedPages = new();
    private string? _selectedNoteId;
    private string? _selectedAnnotatedPageUrl;
    private bool _suppressNoteEvents;
    private DispatcherQueueTimer? _noteSaveTimer;

    private void NotesMenu_Click(object sender, RoutedEventArgs e)
    {
        RefreshNotesPanel();
        ShowPanel(NotesPanel, "Notes");
    }

    private void NotesAddButton_Click(object sender, RoutedEventArgs e)
    {
        FlushPendingNoteSave();
        var created = _notes.Add(string.Empty, string.Empty);
        _selectedNoteId = created.Id;
        _selectedAnnotatedPageUrl = null;
        RefreshNotesPanel();
        NoteTitleBox.Focus(FocusState.Programmatic);
        StatusText.Text = "Nouvelle note créée.";
    }

    private void NotesSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FlushPendingNoteSave();
        RefreshNotesPanel();
    }

    private void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressNoteEvents)
        {
            return;
        }

        // Les modifications de la note quittée partent AVANT de charger l'autre.
        FlushPendingNoteSave();
        var tag = (NotesList.SelectedItem as ListViewItem)?.Tag as string;
        _selectedNoteId = TagNoteId(tag);
        _selectedAnnotatedPageUrl = TagPageUrl(tag);
        LoadSelectionIntoDetail();
    }

    // Tags de la liste mixte : "note|<id>" ou "page|<url>".
    private static string? TagNoteId(string? tag) =>
        tag?.StartsWith("note|", StringComparison.Ordinal) == true ? tag["note|".Length..] : null;

    private static string? TagPageUrl(string? tag) =>
        tag?.StartsWith("page|", StringComparison.Ordinal) == true ? tag["page|".Length..] : null;

    private void NoteEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressNoteEvents || _selectedNoteId is null)
        {
            return;
        }

        // Sauvegarde différée : une écriture par pause de frappe, pas par touche.
        _noteSaveTimer ??= CreateNoteSaveTimer();
        _noteSaveTimer.Stop();
        _noteSaveTimer.Start();
    }

    private DispatcherQueueTimer CreateNoteSaveTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(800);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => SaveNoteEditor();
        return timer;
    }

    private void FlushPendingNoteSave()
    {
        if (_noteSaveTimer?.IsRunning == true)
        {
            _noteSaveTimer.Stop();
            SaveNoteEditor();
        }
    }

    private void SaveNoteEditor()
    {
        if (_selectedNoteId is null)
        {
            return;
        }

        // TextChanged arrive en différé sous WinUI : le chargement d'une note
        // dans l'éditeur en déclenche un hors suppression. Sans changement
        // réel, ne rien écrire et ne pas annoncer de sauvegarde fantôme.
        var current = _visibleNotes.FirstOrDefault(note => note.Id == _selectedNoteId);
        if (current is not null && current.Title == NoteTitleBox.Text && current.Content == NoteContentBox.Text)
        {
            return;
        }

        var updated = _notes.Update(_selectedNoteId, NoteTitleBox.Text, NoteContentBox.Text);
        if (updated is null)
        {
            return;
        }

        // Mise à jour du titre dans la liste EN PLACE : pas de re-tri pendant la
        // frappe, la note ne saute pas sous le curseur. Les notes suivent les
        // pages annotées dans la liste, d'où le décalage d'index. Le tri par
        // date s'applique à la prochaine ouverture ou recherche.
        var index = _visibleNotes.FindIndex(note => note.Id == updated.Id);
        if (index >= 0)
        {
            _visibleNotes[index] = updated;
            var listIndex = _visibleAnnotatedPages.Count + index;
            if (listIndex < NotesList.Items.Count && NotesList.Items[listIndex] is ListViewItem item)
            {
                item.Content = NoteListItemContent(updated);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, NoteStore.DisplayTitle(updated));
            }
        }

        NoteMetaText.Text = NoteMetaLine(updated);
        StatusText.Text = "Note enregistrée.";
    }

    private async void NoteDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNoteId is null)
        {
            return;
        }

        _noteSaveTimer?.Stop();
        var note = _visibleNotes.FirstOrDefault(candidate => candidate.Id == _selectedNoteId);
        var dialog = new ContentDialog
        {
            Title = "Supprimer la note",
            Content = $"Supprimer définitivement « {(note is null ? "cette note" : NoteStore.DisplayTitle(note))} » ?",
            PrimaryButtonText = "Supprimer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _notes.Remove(_selectedNoteId);
        _selectedNoteId = null;
        RefreshNotesPanel();
        StatusText.Text = "Note supprimée.";
    }

    private void NoteLinkButton_Click(object sender, RoutedEventArgs e)
    {
        FlushPendingNoteSave();
        var url = _visibleNotes.FirstOrDefault(note => note.Id == _selectedNoteId)?.Url;
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        // Navigation demandée par l'utilisateur : jamais re-vérifiée par le
        // bouclier anti-redirection.
        _navHealth.RegisterExplicitNavigation(url);
        NavigateCurrentTab(url, DisplayTitle(url));
        ShowPanel(BrowserPanel, DisplayTitle(url));
    }

    private void NoteAttachPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNoteId is null)
        {
            return;
        }

        var address = CurrentTab()?.Address ?? string.Empty;
        if (!BookmarkStore.IsWebUrl(address))
        {
            StatusText.Text = "Ouvrez une page web pour y rattacher la note.";
            return;
        }

        FlushPendingNoteSave();
        var updated = _notes.Update(_selectedNoteId, NoteTitleBox.Text, NoteContentBox.Text, address);
        if (updated is not null)
        {
            var index = _visibleNotes.FindIndex(note => note.Id == updated.Id);
            if (index >= 0)
            {
                _visibleNotes[index] = updated;
            }

            ApplyNoteLinkUi(updated);
            StatusText.Text = $"Note rattachée à : {DisplayTitle(address)}";
        }
    }

    // ── Pages annotées : reprise et gestion ──────────────────────────────────

    private async void AnnotationOpenPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAnnotatedPageUrl is null)
        {
            return;
        }

        await OpenPageInReaderAsync(_selectedAnnotatedPageUrl);
    }

    private async void AnnotationForgetPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAnnotatedPageUrl is null)
        {
            return;
        }

        var page = _visibleAnnotatedPages.FirstOrDefault(candidate => candidate.Url == _selectedAnnotatedPageUrl);
        var dialog = new ContentDialog
        {
            Title = "Oublier cette page",
            Content = $"Supprimer définitivement les {page?.Count ?? 0} annotation(s) de « {AnnotatedPageDisplayTitle(page)} » ?",
            PrimaryButtonText = "Supprimer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _annotations.RemoveForPage(_selectedAnnotatedPageUrl);
        _selectedAnnotatedPageUrl = null;
        RefreshNotesPanel();
        UpdateReaderModeUi();
        StatusText.Text = "Annotations de la page supprimées.";
    }

    private void AnnotationDeleteOne_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string annotationId)
        {
            return;
        }

        if (!_annotations.Remove(annotationId))
        {
            return;
        }

        StatusText.Text = "Annotation supprimée.";
        UpdateReaderModeUi();
        // La page peut avoir perdu sa dernière annotation : reconstruire la
        // liste entière (l'entrée disparaît alors proprement).
        if (_selectedAnnotatedPageUrl is not null && _annotations.CountForPage(_selectedAnnotatedPageUrl) == 0)
        {
            _selectedAnnotatedPageUrl = null;
        }

        RefreshNotesPanel();
    }

    // ── Construction de la liste et du détail ────────────────────────────────

    // Reconstruit la liste depuis les stores (pages annotées d'abord, notes
    // libres ensuite, chacune triée par dernière activité), applique le filtre
    // de recherche et resynchronise le détail.
    private void RefreshNotesPanel()
    {
        _suppressNoteEvents = true;
        try
        {
            var query = NotesSearchBox.Text;
            _visibleAnnotatedPages = _annotations.AnnotatedPages()
                .Where(page => AnnotatedPageMatches(page, query))
                .ToList();
            _visibleNotes = _notes.AllNotes().Where(note => NoteStore.Matches(note, query)).ToList();

            NotesList.Items.Clear();
            foreach (var page in _visibleAnnotatedPages)
            {
                var item = new ListViewItem { Content = AnnotatedPageListItemContent(page), Tag = $"page|{page.Url}" };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item,
                    $"Page annotée : {AnnotatedPageDisplayTitle(page)}");
                NotesList.Items.Add(item);
            }

            foreach (var note in _visibleNotes)
            {
                var item = new ListViewItem { Content = NoteListItemContent(note), Tag = $"note|{note.Id}" };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, NoteStore.DisplayTitle(note));
                NotesList.Items.Add(item);
            }

            if (_selectedNoteId is not null && _visibleNotes.All(note => note.Id != _selectedNoteId))
            {
                _selectedNoteId = null;
            }

            if (_selectedAnnotatedPageUrl is not null &&
                _visibleAnnotatedPages.All(page => page.Url != _selectedAnnotatedPageUrl))
            {
                _selectedAnnotatedPageUrl = null;
            }

            if (_selectedAnnotatedPageUrl is not null)
            {
                NotesList.SelectedIndex = _visibleAnnotatedPages.FindIndex(page => page.Url == _selectedAnnotatedPageUrl);
            }
            else if (_selectedNoteId is not null)
            {
                var noteIndex = _visibleNotes.FindIndex(note => note.Id == _selectedNoteId);
                NotesList.SelectedIndex = noteIndex < 0 ? -1 : _visibleAnnotatedPages.Count + noteIndex;
            }

            NotesEmptyText.Visibility = NotesList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _suppressNoteEvents = false;
        }

        LoadSelectionIntoDetail();
    }

    // La recherche couvre le titre et l'URL de la page, mais aussi le texte de
    // ses surlignages et commentaires.
    private bool AnnotatedPageMatches(AnnotatedPage page, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var trimmed = query.Trim();
        return page.PageTitle.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
               page.Url.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
               _annotations.ForPage(page.Url).Any(annotation => AnnotationStore.Matches(annotation, trimmed));
    }

    private void LoadSelectionIntoDetail()
    {
        var page = _visibleAnnotatedPages.FirstOrDefault(candidate => candidate.Url == _selectedAnnotatedPageUrl);
        if (page is not null)
        {
            NoteEditorHost.Visibility = Visibility.Collapsed;
            NotesEmptyText.Visibility = Visibility.Collapsed;
            LoadAnnotatedPageIntoViewer(page);
            return;
        }

        AnnotationViewerHost.Visibility = Visibility.Collapsed;
        var note = _visibleNotes.FirstOrDefault(candidate => candidate.Id == _selectedNoteId);
        if (note is null)
        {
            NoteEditorHost.Visibility = Visibility.Collapsed;
            NotesEmptyText.Visibility = NotesList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        _suppressNoteEvents = true;
        try
        {
            NoteEditorHost.Visibility = Visibility.Visible;
            NotesEmptyText.Visibility = Visibility.Collapsed;
            NoteTitleBox.Text = note.Title;
            NoteContentBox.Text = note.Content;
            NoteMetaText.Text = NoteMetaLine(note);
            ApplyNoteLinkUi(note);
        }
        finally
        {
            _suppressNoteEvents = false;
        }
    }

    private void LoadAnnotatedPageIntoViewer(AnnotatedPage page)
    {
        AnnotationViewerHost.Visibility = Visibility.Visible;
        AnnotationPageTitleText.Text = AnnotatedPageDisplayTitle(page);
        AnnotationPageUrlText.Text = page.Url;
        ToolTipService.SetToolTip(AnnotationPageTitleText, AnnotatedPageDisplayTitle(page));
        ToolTipService.SetToolTip(AnnotationPageUrlText, page.Url);
        ToolTipService.SetToolTip(AnnotationPageUrlText, page.Url);
        AnnotationMetaText.Text =
            $"{page.Count} annotation(s) - dernière le {page.UpdatedAt.LocalDateTime:g}";

        AnnotationsList.Items.Clear();
        foreach (var annotation in _annotations.ForPage(page.Url))
        {
            AnnotationsList.Items.Add(AnnotationListItemContent(annotation));
        }
    }

    private static string AnnotatedPageDisplayTitle(AnnotatedPage? page)
    {
        if (page is null) return "cette page";
        if (!string.IsNullOrWhiteSpace(page.PageTitle)) return page.PageTitle.Trim();
        return Uri.TryCreate(page.Url, UriKind.Absolute, out var parsed) ? parsed.Host : page.Url;
    }

    private static StackPanel AnnotatedPageListItemContent(AnnotatedPage page)
    {
        var panel = new StackPanel { Spacing = 2, Padding = new Thickness(0, 6, 0, 6) };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        header.Children.Add(new FontIcon
        {
            Glyph = "",
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(new TextBlock
        {
            Text = AnnotatedPageDisplayTitle(page),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        panel.Children.Add(header);

        var meta = $"{page.Count} passage(s) surligné(s) - {HistoryTimeFormatter.Format(page.UpdatedAt)}";
        if (Uri.TryCreate(page.Url, UriKind.Absolute, out var parsed))
        {
            meta = $"{meta} - {parsed.Host}";
        }

        panel.Children.Add(new TextBlock { Text = meta, Opacity = 0.6, FontSize = 12 });
        return panel;
    }

    // Fiche d'une annotation dans le détail : extrait surligné, commentaire
    // éventuel, date, suppression unitaire.
    private Grid AnnotationListItemContent(PageAnnotation annotation)
    {
        var grid = new Grid { ColumnSpacing = 8, Padding = new Thickness(0, 8, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(new TextBlock
        {
            Text = $"« {AnnotationStore.DisplayQuote(annotation)} »",
            TextWrapping = TextWrapping.Wrap,
            FontStyle = Windows.UI.Text.FontStyle.Italic
        });
        if (!string.IsNullOrWhiteSpace(annotation.Comment))
        {
            body.Children.Add(new TextBlock
            {
                Text = annotation.Comment,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.78
            });
        }

        body.Children.Add(new TextBlock
        {
            Text = HistoryTimeFormatter.Format(annotation.UpdatedAt),
            Opacity = 0.6,
            FontSize = 12
        });
        grid.Children.Add(body);

        var delete = new Button
        {
            Content = new SymbolIcon(Symbol.Delete),
            Tag = annotation.Id,
            VerticalAlignment = VerticalAlignment.Top
        };
        ToolTipService.SetToolTip(delete, "Supprimer cette annotation");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(delete, "Supprimer cette annotation");
        delete.Click += AnnotationDeleteOne_Click;
        Grid.SetColumn(delete, 1);
        grid.Children.Add(delete);

        return grid;
    }

    private void ApplyNoteLinkUi(Note note)
    {
        var hasLink = !string.IsNullOrWhiteSpace(note.Url);
        NoteLinkButton.Visibility = hasLink ? Visibility.Visible : Visibility.Collapsed;
        if (hasLink)
        {
            NoteLinkButton.Content = DisplayTitle(note.Url);
            ToolTipService.SetToolTip(NoteLinkButton, note.Url);
        }
    }

    private static StackPanel NoteListItemContent(Note note)
    {
        var panel = new StackPanel { Spacing = 2, Padding = new Thickness(0, 6, 0, 6) };
        panel.Children.Add(new TextBlock
        {
            Text = NoteStore.DisplayTitle(note),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var meta = HistoryTimeFormatter.Format(note.UpdatedAt);
        if (!string.IsNullOrWhiteSpace(note.Url) &&
            Uri.TryCreate(note.Url, UriKind.Absolute, out var parsed))
        {
            meta = $"{meta} - {parsed.Host}";
        }

        panel.Children.Add(new TextBlock { Text = meta, Opacity = 0.6, FontSize = 12 });
        return panel;
    }

    private static string NoteMetaLine(Note note) =>
        $"Créée le {note.CreatedAt.LocalDateTime:g} - modifiée le {note.UpdatedAt.LocalDateTime:g}";
}
