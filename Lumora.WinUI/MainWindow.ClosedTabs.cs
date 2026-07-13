using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // ── Onglets récemment fermés (Ctrl+Shift+T) ──────────────────────────────

    private readonly ClosedTabHistory _closedTabs = new();

    // Appelé par CloseTab AVANT le retrait : capture l'état à restaurer.
    private void RememberClosedTab(BrowserTabState state) =>
        _closedTabs.Push(new ClosedTabRecord(state.Title, state.Address, state.IconPath, state.GroupId, state.Pinned));

    private void ReopenClosedTabAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ReopenLastClosedTab();
    }

    private void ReopenTabMenu_Click(object sender, RoutedEventArgs e) =>
        ReopenLastClosedTab();

    private void ReopenLastClosedTab()
    {
        var record = _closedTabs.Pop();
        if (record is null)
        {
            StatusText.Text = "Aucun onglet ferme recemment.";
            return;
        }

        ReopenClosedTab(record);
    }

    private void ReopenClosedTab(ClosedTabRecord record)
    {
        // No-op après Pop ; nécessaire quand la restauration vient de la palette
        // de commandes (onglet choisi au milieu de la pile).
        _closedTabs.Remove(record);

        // Le groupe d'origine a pu être supprimé entre-temps : on restaure alors hors groupe.
        var groupId = record.GroupId is { } id && _tabGroups.Any(group => group.Id == id)
            ? record.GroupId
            : null;

        AddTab(record.Title, record.Address, select: true, groupId: groupId, pinned: record.Pinned);
        StatusText.Text = $"Onglet restaure : {record.Title}";
    }
}
