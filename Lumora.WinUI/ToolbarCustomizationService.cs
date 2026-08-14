using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

/// <summary>
/// Service de réorganisation personnalisée de la barre d'outils.
/// Gère la persistance de l'ordre, l'application aux boutons visibles et le
/// déplacement d'un bouton d'un cran (Monter/Descendre).
///
/// Le glisser-déposer (edit mode, capture de pointeur, drag live) a été
/// entièrement retiré (2026-08-14, demande explicite utilisateur) après 3
/// bugs réels trouvés et corrigés sur ce mécanisme en une seule session
/// (zone de déplacement de fenêtre qui recouvrait la barre, boutons masqués
/// pollués le calcul de cible, capture perdue même différée au relâchement -
/// voir MEMORY.md) sans jamais aboutir à un glisser fiable en conditions
/// réelles. Remplacé par un déplacement au clic ("Monter"/"Descendre",
/// <see cref="MoveButtonAdjacent"/>), intégré au menu Modules (épingler/
/// désépingler) plutôt qu'à un clic droit séparé - même raisonnement que
/// l'alternative déjà livrée pour les favoris (voir
/// Models/Bookmarks.cs, BookmarkStore.MoveNodeAdjacent).
/// </summary>
internal class ToolbarCustomizationService
{
    private List<string> _buttonOrder = new();
    private List<int> _separatorPositions = new();
    private Dictionary<string, UIElement> _buttonMap = new();
    private UiSettings _settings = new();
    private string _settingsPath = string.Empty;
    private StackPanel _toolbarPanel = new();

    /// <summary>
    /// Initialise le service avec les paramètres et la map des boutons.
    /// Charge l'ordre personnalisé depuis UiSettings et l'applique à la barre.
    /// </summary>
    public void Initialize(UiSettings settings, string settingsPath, Dictionary<string, UIElement> buttonMap, StackPanel toolbarPanel)
    {
        _settings = settings;
        _settingsPath = settingsPath;
        _buttonMap = new Dictionary<string, UIElement>(buttonMap);
        _toolbarPanel = toolbarPanel;

        LoadOrder();
        ApplyOrderToToolbar();
    }

    /// <summary>
    /// Réinitialise à la disposition par défaut et sauvegarde.
    /// </summary>
    public void ResetToDefault()
    {
        _buttonOrder = GetDefaultButtonOrder();
        _separatorPositions = GetDefaultSeparatorPositions();
        ApplyOrderToToolbar();
        SaveOrder();
    }

    /// <summary>
    /// Ordre actuel des boutons REELLEMENT VISIBLES (Visibility.Visible),
    /// dans l'ordre d'affichage. Les modules non épinglés (Visibility=
    /// Collapsed, voir UpdateModulesPinUi) sont exclus - c'est cette liste
    /// que le menu Modules affiche pour le réordonnancement (Monter/
    /// Descendre n'a de sens que parmi ce qui est reellement dans la barre).
    /// </summary>
    public IReadOnlyList<string> GetVisibleButtonOrder() =>
        _buttonOrder.Where(IsButtonVisible).ToList();

    private bool IsButtonVisible(string buttonId) =>
        _buttonMap.TryGetValue(buttonId, out var element) && element.Visibility == Visibility.Visible;

    /// <summary>
    /// Déplace <paramref name="buttonId"/> d'UN cran parmi les boutons
    /// VISIBLES (Monter = <paramref name="moveForward"/>=false, Descendre =
    /// true - même convention que BookmarkStore.MoveNodeAdjacent). Un bouton
    /// masqué (non épinglé) n'est jamais un voisin valide : le calcul se
    /// fait sur <see cref="GetVisibleButtonOrder"/>, pas sur l'ordre complet
    /// des 16 boutons - même correction que celle appliquée au glisser avant
    /// son retrait (voir MEMORY.md, "boutons masques pollues le calcul").
    /// Sauvegarde immédiatement en cas de succès (pas de mode édition à
    /// quitter, contrairement à l'ancien glisser).
    /// </summary>
    public bool MoveButtonAdjacent(string buttonId, bool moveForward)
    {
        if (!_buttonOrder.Contains(buttonId))
        {
            return false;
        }

        var visibleOrder = GetVisibleButtonOrder();
        var (canMove, beforeId) = AdjacentMoveMath.ComputeTarget(visibleOrder, buttonId, moveForward);
        if (!canMove)
        {
            return false;
        }

        _buttonOrder.Remove(buttonId);

        int insertIndex;
        if (beforeId is null)
        {
            // "En dernier" parmi les boutons VISIBLES : s'insère juste après
            // le dernier bouton visible restant dans l'ordre complet, pas à
            // la toute fin de _buttonOrder (qui contient aussi les boutons
            // masqués, positionnés après dans l'ordre par défaut).
            var lastVisibleIndex = -1;
            for (var i = 0; i < _buttonOrder.Count; i++)
            {
                if (IsButtonVisible(_buttonOrder[i])) lastVisibleIndex = i;
            }
            insertIndex = lastVisibleIndex + 1;
        }
        else
        {
            insertIndex = _buttonOrder.IndexOf(beforeId);
            if (insertIndex < 0)
            {
                // Cible disparue entre-temps (ne devrait pas arriver, mais on
                // annule proprement plutot que d'inserer a un index invalide).
                _buttonOrder.Insert(0, buttonId);
                ApplyOrderToToolbar();
                SaveOrder();
                return true;
            }
        }

        _buttonOrder.Insert(insertIndex, buttonId);
        ApplyOrderToToolbar();
        SaveOrder();
        return true;
    }

    /// <summary>
    /// Charge l'ordre sauvegardé depuis UiSettings.
    /// Si absent, utilise l'ordre par défaut.
    /// </summary>
    private void LoadOrder()
    {
        if (_settings.ToolbarButtonOrder != null && _settings.ToolbarButtonOrder.Count > 0)
        {
            _buttonOrder = new List<string>(_settings.ToolbarButtonOrder);
        }
        else
        {
            _buttonOrder = GetDefaultButtonOrder();
        }

        if (_settings.ToolbarSeparatorPositions != null && _settings.ToolbarSeparatorPositions.Count > 0)
        {
            _separatorPositions = new List<int>(_settings.ToolbarSeparatorPositions);
        }
        else
        {
            _separatorPositions = GetDefaultSeparatorPositions();
        }
    }

    /// <summary>
    /// Sauvegarde l'ordre actuel dans UiSettings et persiste sur disque.
    /// </summary>
    private void SaveOrder()
    {
        _settings.ToolbarButtonOrder = new List<string>(_buttonOrder);
        _settings.ToolbarSeparatorPositions = new List<int>(_separatorPositions);
        if (!string.IsNullOrEmpty(_settingsPath))
            _settings.Save(_settingsPath);
    }

    /// <summary>
    /// Réorganise les boutons dans le StackPanel de la barre selon _buttonOrder.
    /// Ajoute les séparateurs aux positions définies.
    /// </summary>
    private void ApplyOrderToToolbar()
    {
        if (_toolbarPanel == null) return;

        _toolbarPanel.Children.Clear();

        for (int i = 0; i < _buttonOrder.Count; i++)
        {
            string buttonId = _buttonOrder[i];
            if (_buttonMap.TryGetValue(buttonId, out var button))
            {
                _toolbarPanel.Children.Add(button);

                if (_separatorPositions.Contains(i))
                {
                    var separator = CreateSeparator();
                    _toolbarPanel.Children.Add(separator);
                }
            }
        }
    }

    /// <summary>
    /// Crée un séparateur visuel (Border vertical gris).
    /// </summary>
    private UIElement CreateSeparator()
    {
        return new Border
        {
            Width = 1,
            Height = 24,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Gray
            ),
            Margin = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    /// <summary>
    /// Retourne l'ordre par défaut des boutons (tel qu'ils sont maintenant).
    /// Les 6 premiers (2026-08-14) etaient auparavant a des Grid.Column fixes
    /// dans NavigationToolbar (colonnes 6 a 11, dans ce meme ordre visuel) -
    /// deplaces ici pour devenir reordonnables au meme titre que les
    /// modules, demande explicite utilisateur. Voir MainWindow.xaml et
    /// MEMORY.md.
    /// </summary>
    private static List<string> GetDefaultButtonOrder()
    {
        return new List<string>
        {
            "AddBookmarkButton",
            "IncognitoToolbarButton",
            "ShieldButton",
            "VaultQuickAccessButton",
            "HistoryToolbarButton",
            "DownloadsIndicatorButton",
            "ReaderModeButton",
            "NotesModuleButton",
            "ReadAloudButton",
            "DetachVideoPinnedButton",
            "VideoDownloadButton",
            "TranslatePinnedButton",
            "WebAppsPinnedButton",
            "SearchAssistButton",
            "DictationPinnedButton",
            "RssModuleButton",
            "ReadingLensButton",
            "ModulesQuickAccessButton",
            "ConnectionsQuickAccessButton",
            "ConsentIndicatorButton",
            "PopupRecoveryButton",
            "SplitViewButton"
        };
    }

    /// <summary>
    /// Retourne les positions par défaut des séparateurs (aucun, ordre linéaire).
    /// </summary>
    private static List<int> GetDefaultSeparatorPositions()
    {
        return new List<int>();
    }
}
