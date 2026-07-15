using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

// ── Panneau Notes : bloc-notes local ─────────────────────────────────────────
// Liste à gauche, éditeur à droite. Sauvegarde automatique différée pendant la
// frappe (aucun bouton « Enregistrer » à oublier), vidée immédiatement au
// changement de note ou de panneau. Les données vivent dans NoteStore
// (notes.lumora, chiffré DPAPI) ; en mode invité, en mémoire de session.
public sealed partial class MainWindow
{
    private List<Note> _visibleNotes = new();
    private string? _selectedNoteId;
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
        RefreshNotesPanel();
        NoteTitleBox.Focus(FocusState.Programmatic);
        StatusText.Text = "Nouvelle note creee.";
    }

    private void NoteFromPageButton_Click(object sender, RoutedEventArgs e)
    {
        FlushPendingNoteSave();
        var tab = CurrentTab();
        var address = tab?.Address ?? string.Empty;
        if (!BookmarkStore.IsWebUrl(address))
        {
            StatusText.Text = "Ouvrez une page web pour prendre une note dessus.";
            return;
        }

        var created = _notes.Add(tab?.Title ?? DisplayTitle(address), string.Empty, address);
        _selectedNoteId = created.Id;
        RefreshNotesPanel();
        NoteContentBox.Focus(FocusState.Programmatic);
        StatusText.Text = $"Note rattachee a : {DisplayTitle(address)}";
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
        _selectedNoteId = (NotesList.SelectedItem as ListViewItem)?.Tag as string;
        LoadSelectedNoteIntoEditor();
    }

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
        // frappe, la note ne saute pas sous le curseur. Le tri par date de
        // modification s'applique à la prochaine ouverture ou recherche.
        var index = _visibleNotes.FindIndex(note => note.Id == updated.Id);
        if (index >= 0)
        {
            _visibleNotes[index] = updated;
            if (NotesList.Items[index] is ListViewItem item)
            {
                item.Content = NoteListItemContent(updated);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, NoteStore.DisplayTitle(updated));
            }
        }

        NoteMetaText.Text = NoteMetaLine(updated);
        StatusText.Text = "Note enregistree.";
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
            Content = $"Supprimer definitivement « {(note is null ? "cette note" : NoteStore.DisplayTitle(note))} » ?",
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
        StatusText.Text = "Note supprimee.";
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
            StatusText.Text = $"Note rattachee a : {DisplayTitle(address)}";
        }
    }

    // Reconstruit la liste depuis le store (tri par derniere modification),
    // applique le filtre de recherche et resynchronise l'éditeur.
    private void RefreshNotesPanel()
    {
        _suppressNoteEvents = true;
        try
        {
            var query = NotesSearchBox.Text;
            _visibleNotes = _notes.AllNotes().Where(note => NoteStore.Matches(note, query)).ToList();

            NotesList.Items.Clear();
            foreach (var note in _visibleNotes)
            {
                var item = new ListViewItem { Content = NoteListItemContent(note), Tag = note.Id };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, NoteStore.DisplayTitle(note));
                NotesList.Items.Add(item);
            }

            if (_selectedNoteId is not null && _visibleNotes.All(note => note.Id != _selectedNoteId))
            {
                _selectedNoteId = null;
            }

            if (_selectedNoteId is not null)
            {
                NotesList.SelectedIndex = _visibleNotes.FindIndex(note => note.Id == _selectedNoteId);
            }

            NotesEmptyText.Visibility = _visibleNotes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _suppressNoteEvents = false;
        }

        LoadSelectedNoteIntoEditor();
    }

    private void LoadSelectedNoteIntoEditor()
    {
        var note = _visibleNotes.FirstOrDefault(candidate => candidate.Id == _selectedNoteId);
        if (note is null)
        {
            NoteEditorHost.Visibility = Visibility.Collapsed;
            NotesEmptyText.Visibility = _visibleNotes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
        $"Creee le {note.CreatedAt.LocalDateTime:g} - modifiee le {note.UpdatedAt.LocalDateTime:g}";
}
