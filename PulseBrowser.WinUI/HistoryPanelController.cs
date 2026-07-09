using System.Collections.ObjectModel;

namespace PulseBrowser.WinUI;

// Regroupe l'état auparavant éparpillé sur MainWindow (historique, favoris de
// panneau, téléchargements de session, terme de recherche) sous un seul champ.
// Le rendu (éléments XAML, cache de favicons) reste sur MainWindow.History.cs :
// les extraire ici demanderait d'injecter la fenêtre entière, ce qui ne
// réduirait pas le couplage réel.
internal sealed class HistoryPanelController
{
    public HistoryStore Store { get; }
    public ObservableCollection<HistoryListItem> Items { get; } = new();
    public List<DownloadEntry> Downloads { get; } = new();
    public string SearchTerm { get; set; } = string.Empty;

    public HistoryPanelController(HistoryStore store)
    {
        Store = store;
    }
}
