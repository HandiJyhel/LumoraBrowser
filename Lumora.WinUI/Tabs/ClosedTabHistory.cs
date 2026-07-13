namespace Lumora.WinUI;

// Instantané d'un onglet fermé, suffisant pour le restaurer (Ctrl+Shift+T).
// Volontairement découplé de SavedTab/BrowserTabState : classe pure, testable
// sans dépendance UI.
internal sealed record ClosedTabRecord(string Title, string Address, string IconPath, int? GroupId, bool Pinned);

// Pile bornée (plus récent en tête) des onglets fermés de la session en cours.
// En mémoire uniquement : au redémarrage, la restauration de session couvre déjà
// les onglets encore ouverts ; garder les fermés sur disque serait une trace de
// navigation de plus, contraire à l'esprit du projet.
internal sealed class ClosedTabHistory
{
    public const int Capacity = 20;

    private readonly List<ClosedTabRecord> _items = new();

    public IReadOnlyList<ClosedTabRecord> Items => _items;

    // Un onglet resté sur l'accueil n'a rien à restaurer : le rouvrir donnerait
    // la même chose que « Nouvel onglet ».
    public static bool IsWorthRestoring(string address) =>
        !string.IsNullOrWhiteSpace(address) &&
        !address.Equals("lumora://accueil", StringComparison.OrdinalIgnoreCase);

    public void Push(ClosedTabRecord record)
    {
        if (!IsWorthRestoring(record.Address)) return;

        _items.Insert(0, record);
        if (_items.Count > Capacity)
        {
            _items.RemoveAt(_items.Count - 1);
        }
    }

    public ClosedTabRecord? Pop()
    {
        if (_items.Count == 0) return null;

        var record = _items[0];
        _items.RemoveAt(0);
        return record;
    }

    // Retrait ciblé pour la restauration depuis la palette de commandes
    // (l'utilisateur peut rouvrir un onglet qui n'est pas le dernier fermé).
    public bool Remove(ClosedTabRecord record) => _items.Remove(record);

    public void Clear() => _items.Clear();
}
