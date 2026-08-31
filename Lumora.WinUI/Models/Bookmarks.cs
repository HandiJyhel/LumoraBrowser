using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Lumora.WinUI.Credentials;

namespace Lumora.WinUI;

public sealed record BookmarkListItem(
    string IconGlyph,
    string IconImageUri,
    Visibility IconImageVisibility,
    Visibility IconGlyphVisibility,
    string Title,
    string Detail,
    BookmarkNode Node);

public enum BookmarkKind
{
    Folder,
    Url
}

public sealed record BookmarkNode(
    string Id,
    string ParentId,
    BookmarkKind Kind,
    uint Position,
    string Title,
    string Url,
    string IconPath = "")
{
    public bool IsRoot => Id is BookmarkStore.ToolbarRootId or BookmarkStore.OtherRootId;
    public string IconUri => string.IsNullOrWhiteSpace(IconPath) ? string.Empty : new Uri(IconPath).AbsoluteUri;
}

public static class BookmarkGlyphs
{
    public const string Folder = "";
    public const string Link = "";

    public static string For(BookmarkNode node) => node.Kind == BookmarkKind.Folder ? Folder : Link;
}


public sealed class BookmarkStore
{
    public const string ToolbarRootId = "root-toolbar";
    public const string OtherRootId = "root-other";
    public const string InvisibleTitle = "\u200B";

    private readonly string _bookmarksFile;
    private readonly string? _legacyBookmarksFile;
    private readonly string? _legacyFavoritesFile;
    private bool _isGuest;

    public BookmarkStore(string bookmarksFile, string? legacyBookmarksFile, string? legacyFavoritesFile)
    {
        _bookmarksFile = bookmarksFile;
        _legacyBookmarksFile = legacyBookmarksFile;
        _legacyFavoritesFile = legacyFavoritesFile;
    }

    // Favoris du mode invité : vivants pendant la session, jamais écrits sur le
    // disque. Sans cette couche, « Ajouter aux favoris » annonçait un succès en
    // mode invité sans rien ajouter nulle part (0.78.2).
    private List<BookmarkNode>? _guestNodes;

    public void SetGuestMode(bool isGuest)
    {
        _isGuest = isGuest;
        if (!isGuest) _guestNodes = null;
    }

    public List<BookmarkNode> AllNodes()
    {
        if (_isGuest) return Sort(new List<BookmarkNode>(_guestNodes ??= RootNodes()));

        EnsureFile();
        var content = LumoraFile.TryReadAllText(_bookmarksFile) ?? string.Empty;
        var nodes = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseLine)
            .Where(node => node is not null)
            .Cast<BookmarkNode>()
            .ToList();
        EnsureRoots(nodes);
        return Sort(nodes);
    }

    public void AddUrl(string parentId, string url, string title, string iconPath = "")
    {
        if (!IsWebUrl(url))
        {
            return;
        }

        var nodes = AllNodes();
        nodes.Add(new BookmarkNode(NextNodeId(nodes, "bookmark"), parentId, BookmarkKind.Url, NextPosition(nodes, parentId), CleanTitle(title, url), url, CleanLocalPath(iconPath)));
        WriteNodes(nodes);
    }

    public static bool IsIconOnlyTitle(string title) =>
        title.Equals(InvisibleTitle, StringComparison.Ordinal);

    public BookmarkNode? AddOrUpdateUrl(string? id, string parentId, string url, string title, string iconPath = "")
    {
        if (!IsWebUrl(url))
        {
            return null;
        }

        var nodes = AllNodes();
        if (nodes.All(node => node.Id != parentId || node.Kind != BookmarkKind.Folder))
        {
            parentId = ToolbarRootId;
        }

        var index = string.IsNullOrWhiteSpace(id)
            ? -1
            : nodes.FindIndex(node => node.Id == id && node.Kind == BookmarkKind.Url);

        if (index >= 0)
        {
            var node = nodes[index];
            var moved = !node.ParentId.Equals(parentId, StringComparison.Ordinal);
            var updated = node with
            {
                ParentId = parentId,
                Position = moved ? NextPosition(nodes, parentId) : node.Position,
                Title = CleanTitle(title, url),
                Url = url,
                IconPath = CleanLocalPath(iconPath)
            };
            nodes[index] = updated;
            WriteNodes(nodes);
            return updated;
        }

        var created = new BookmarkNode(
            NextNodeId(nodes, "bookmark"),
            parentId,
            BookmarkKind.Url,
            NextPosition(nodes, parentId),
            CleanTitle(title, url),
            url,
            CleanLocalPath(iconPath));
        nodes.Add(created);
        WriteNodes(nodes);
        return created;
    }

    public bool SetIconForUrl(string url, string iconPath)
    {
        if (!IsWebUrl(url) || string.IsNullOrWhiteSpace(iconPath))
        {
            return false;
        }

        var nodes = AllNodes();
        var changed = false;
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (node.Kind == BookmarkKind.Url && node.Url.Equals(url, StringComparison.OrdinalIgnoreCase) && node.IconPath != iconPath)
            {
                nodes[index] = node with { IconPath = CleanLocalPath(iconPath) };
                changed = true;
            }
        }

        if (changed)
        {
            WriteNodes(nodes);
        }

        return changed;
    }

    public bool SetIconForOrigin(string url, string iconPath)
    {
        if (!IsWebUrl(url) || string.IsNullOrWhiteSpace(iconPath))
        {
            return false;
        }

        // Comparaison par hôte (sans scheme) : un favori importé resté en http://
        // doit récupérer l'icône capturée lors d'une visite https:// du même site.
        var host = PublicSuffixService.HostOf(url);
        var nodes = AllNodes();
        var changed = false;
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (node.Kind == BookmarkKind.Url && node.IconPath != iconPath &&
                PublicSuffixService.HostOf(node.Url).Equals(host, StringComparison.OrdinalIgnoreCase))
            {
                nodes[index] = node with { IconPath = CleanLocalPath(iconPath) };
                changed = true;
            }
        }

        if (changed)
        {
            WriteNodes(nodes);
        }

        return changed;
    }

    public string AddFolder(string parentId, string title)
    {
        var nodes = AllNodes();
        if (nodes.All(node => node.Id != parentId || node.Kind != BookmarkKind.Folder))
        {
            parentId = ToolbarRootId;
        }

        var id = NextNodeId(nodes, "folder");
        nodes.Add(new BookmarkNode(id, parentId, BookmarkKind.Folder, NextPosition(nodes, parentId), CleanTitle(title, string.Empty), string.Empty));
        WriteNodes(nodes);
        return id;
    }

    public void RenameNode(string id, string title)
    {
        if (id is ToolbarRootId or OtherRootId)
        {
            return;
        }

        var nodes = AllNodes();
        var index = nodes.FindIndex(node => node.Id == id);
        if (index < 0)
        {
            return;
        }

        var node = nodes[index];
        nodes[index] = node with { Title = CleanTitle(title, node.Url) };
        WriteNodes(nodes);
    }

    // Réécrit l'URL de signets existants (id -> nouvelle URL), en un seul passage
    // disque. Utilisé quand un site a déménagé de domaine : titres, position et
    // icônes sont conservés. Retourne le nombre de signets réellement modifiés.
    public int UpdateUrls(IReadOnlyDictionary<string, string> newUrlsByNodeId)
    {
        if (newUrlsByNodeId.Count == 0)
        {
            return 0;
        }

        var nodes = AllNodes();
        var updated = 0;
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node.Kind != BookmarkKind.Url ||
                !newUrlsByNodeId.TryGetValue(node.Id, out var url) ||
                !IsWebUrl(url) ||
                node.Url.Equals(url, StringComparison.Ordinal))
            {
                continue;
            }

            nodes[i] = node with { Url = url };
            updated++;
        }

        if (updated > 0)
        {
            WriteNodes(nodes);
        }

        return updated;
    }

    // Deplacement direct dans la barre de favoris (2026-08-13) : meme
    // comportement que Chrome/Edge, aucun mode dedie a activer - on glisse une
    // icone ou un dossier et on le depose au bon endroit. Portee volontairement
    // limitee a un reordonnancement ENTRE FRERES DU MEME PARENT pour cette
    // passe (deposer DANS un dossier, avec ouverture automatique au survol,
    // est laisse pour plus tard - voir MEMORY.md). movedId se place juste
    // avant beforeId ; si beforeId est vide/absent, movedId part en dernier.
    // Reassigne des Position sequentielles a TOUS les freres (pas seulement
    // aux deux nodes concernes) pour eliminer d'eventuels trous/doublons
    // hérités d'anciens imports plutot que de les faire perdurer.
    public bool ReorderNode(string movedId, string? beforeId)
    {
        var nodes = AllNodes();
        var moved = nodes.FirstOrDefault(node => node.Id == movedId);
        if (moved is null || moved.IsRoot)
        {
            return false;
        }

        BookmarkNode? before = null;
        if (!string.IsNullOrEmpty(beforeId))
        {
            before = nodes.FirstOrDefault(node => node.Id == beforeId);
            if (before is null || before.Id == moved.Id || !before.ParentId.Equals(moved.ParentId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        var siblings = nodes
            .Where(node => node.ParentId.Equals(moved.ParentId, StringComparison.Ordinal) && node.Id != moved.Id)
            .OrderBy(node => node.Position)
            .ToList();
        var insertIndex = before is null ? siblings.Count : siblings.FindIndex(node => node.Id == before.Id);
        siblings.Insert(insertIndex, moved);

        var newPositions = new Dictionary<string, uint>(StringComparer.Ordinal);
        for (var i = 0; i < siblings.Count; i++)
        {
            newPositions[siblings[i].Id] = (uint)i;
        }

        var changed = false;
        for (var i = 0; i < nodes.Count; i++)
        {
            if (newPositions.TryGetValue(nodes[i].Id, out var position) && nodes[i].Position != position)
            {
                nodes[i] = nodes[i] with { Position = position };
                changed = true;
            }
        }

        if (changed)
        {
            WriteNodes(nodes);
        }

        return true;
    }

    // Deplacement DANS un dossier different (2026-08-31, "gestion des favoris
    // a chier" - jusqu'ici ReorderNode ne savait que reordonner ENTRE FRERES
    // DU MEME PARENT ; aucun moyen de ranger un favori DANS un dossier
    // n'existait, ni par glisser ni par menu). Toujours ajoute en DERNIERE
    // position parmi les enfants du nouveau parent - un ReorderNode
    // (deja teste) peut affiner la position ensuite si besoin.
    // Refuse silencieusement (retourne false, comme ReorderNode/RemoveNode) :
    // - movedId absent ou racine (une racine n'a pas de parent a changer) ;
    // - newParentId absent ou n'est pas un dossier ;
    // - newParentId == movedId (un noeud ne peut pas devenir son propre
    //   parent) ;
    // - newParentId est un DESCENDANT de movedId (deplacer "Voyages" dans
    //   "Voyages/Vols" creerait un cycle - IsDescendantOfMoved remonte la
    //   chaine ParentId depuis newParentId jusqu'a une racine, en cherchant
    //   movedId sur le trajet).
    public bool MoveNode(string movedId, string newParentId)
    {
        var nodes = AllNodes();
        var moved = nodes.FirstOrDefault(node => node.Id == movedId);
        if (moved is null || moved.IsRoot)
        {
            return false;
        }

        var newParent = nodes.FirstOrDefault(node => node.Id == newParentId);
        if (newParent is null || newParent.Kind != BookmarkKind.Folder || newParent.Id == movedId)
        {
            return false;
        }

        if (IsDescendantOfMoved(nodes, newParentId, movedId))
        {
            return false;
        }

        if (moved.ParentId.Equals(newParentId, StringComparison.Ordinal))
        {
            // Deja dans ce dossier : rien a faire (evite un aller-retour
            // disque inutile et une position "en dernier" surprenante pour
            // un favori qui n'a en realite pas bouge).
            return true;
        }

        var index = nodes.FindIndex(node => node.Id == movedId);
        nodes[index] = moved with
        {
            ParentId = newParentId,
            Position = NextPosition(nodes, newParentId)
        };
        WriteNodes(nodes);
        return true;
    }

    // Vrai si candidateId EST movedId ou l'un de ses descendants (remonte la
    // chaine ParentId depuis candidateId). Utilise par MoveNode pour refuser
    // tout deplacement qui creerait un cycle (dossier depose dans son propre
    // sous-dossier).
    private static bool IsDescendantOfMoved(IReadOnlyList<BookmarkNode> nodes, string candidateId, string movedId)
    {
        var currentId = candidateId;
        while (!string.IsNullOrEmpty(currentId))
        {
            if (currentId == movedId)
            {
                return true;
            }

            var current = nodes.FirstOrDefault(node => node.Id == currentId);
            if (current is null)
            {
                return false;
            }

            currentId = current.ParentId;
        }

        return false;
    }

    // Alternative au glisser (2026-08-14, demande explicite utilisateur apres
    // plusieurs echecs reels du glisser-deposer malgre 3 correctifs bases sur
    // de la documentation officielle puis une trace reelle - voir MEMORY.md) :
    // deplace un favori/dossier d'UN cran parmi ses freres via un simple clic
    // (menu contextuel), sans dependre d'un geste souris. Le calcul de la
    // cible est isole dans AdjacentMoveMath (pur, teste directement -
    // voir ce fichier) ; ReorderNode (deja teste) fait le travail reel.
    public bool MoveNodeAdjacent(string movedId, bool moveForward)
    {
        var nodes = AllNodes();
        var moved = nodes.FirstOrDefault(node => node.Id == movedId);
        if (moved is null || moved.IsRoot)
        {
            return false;
        }

        var orderedIds = nodes
            .Where(node => node.ParentId.Equals(moved.ParentId, StringComparison.Ordinal))
            .OrderBy(node => node.Position)
            .Select(node => node.Id)
            .ToList();

        var (canMove, beforeId) = AdjacentMoveMath.ComputeTarget(orderedIds, movedId, moveForward);
        return canMove && ReorderNode(movedId, beforeId);
    }

    public void RemoveNode(string id)
    {
        if (id is ToolbarRootId or OtherRootId)
        {
            return;
        }

        RemoveNodes(new[] { id }, backupLabel: string.Empty);
    }

    public int RemoveNodes(IEnumerable<string> ids, string backupLabel = "before-bulk-delete")
    {
        var nodes = AllNodes();
        var removeIds = ids
            .Where(id => id is not ToolbarRootId and not OtherRootId)
            .ToHashSet(StringComparer.Ordinal);
        if (removeIds.Count == 0)
        {
            return 0;
        }

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var node in nodes)
            {
                if (removeIds.Contains(node.ParentId) && removeIds.Add(node.Id))
                {
                    changed = true;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(backupLabel))
        {
            Backup(backupLabel);
        }

        WriteNodes(nodes.Where(node => !removeIds.Contains(node.Id)).ToList());
        return removeIds.Count;
    }

    public int ClearUserBookmarks()
    {
        var nodes = AllNodes();
        var count = nodes.Count(node => !node.IsRoot);
        if (count == 0)
        {
            return 0;
        }

        Backup("before-clear-all");
        WriteNodes(RootNodes());
        return count;
    }

    public int RepairImportedFolderDuplicates()
    {
        EnsureFile();
        var nodes = AllNodes();
        var mergedFolders = MergeSiblingImportFolders(nodes);
        var removedDuplicateUrls = RemoveDuplicateUrls(nodes);
        var changed = mergedFolders + removedDuplicateUrls;
        if (changed == 0)
        {
            return 0;
        }

        Backup("before-bookmark-duplicate-repair");
        WriteNodes(nodes);
        return changed;
    }

    public int MergeImport(BookmarkImportTree tree)
    {
        EnsureFile();
        Backup("before-winui-import");
        var nodes = AllNodes();
        var state = ImportState.From(nodes);
        var imported = 0;
        imported += AddImportItems(nodes, ToolbarRootId, tree.Toolbar, state);
        imported += AddImportItems(nodes, OtherRootId, tree.Other, state);
        MergeSiblingImportFolders(nodes);
        WriteNodes(nodes);
        return imported;
    }

    public int ReplaceWithImport(BookmarkImportTree tree)
    {
        EnsureFile();
        Backup("before-winui-replace");
        var nodes = RootNodes();
        var state = ImportState.From(nodes);
        var imported = 0;
        imported += AddImportItems(nodes, ToolbarRootId, tree.Toolbar, state);
        imported += AddImportItems(nodes, OtherRootId, tree.Other, state);
        MergeSiblingImportFolders(nodes);
        WriteNodes(nodes);
        return imported;
    }

    // Accumulateurs tenus a jour au fil de l'import (au lieu de rescanner toute
    // la liste de noeuds a chaque favori ajoute) : un import de plusieurs
    // milliers de favoris passait en O(n^2) sur le thread d'interface et
    // rendait l'application totalement figee ("impossible de fermer") - signale
    // par l'utilisateur, 2026-08-22.
    private sealed class ImportState
    {
        public required HashSet<string> ExistingIds { get; init; }
        public required HashSet<string> ExistingUrls { get; init; }
        public required Dictionary<string, uint> NextPositionByParent { get; init; }
        public long IdCounter;

        public static ImportState From(List<BookmarkNode> nodes) => new()
        {
            ExistingIds = nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal),
            ExistingUrls = nodes.Where(node => node.Kind == BookmarkKind.Url)
                .Select(node => node.Url)
                .ToHashSet(StringComparer.Ordinal),
            NextPositionByParent = nodes.GroupBy(node => node.ParentId)
                .ToDictionary(group => group.Key, group => group.Max(node => node.Position) + 1),
            IdCounter = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        public string NextId(string prefix)
        {
            string id;
            do
            {
                id = $"{prefix}-{IdCounter++}";
            } while (!ExistingIds.Add(id));

            return id;
        }

        public uint NextPosition(string parentId)
        {
            var position = NextPositionByParent.TryGetValue(parentId, out var next) ? next : 0;
            NextPositionByParent[parentId] = position + 1;
            return position;
        }
    }

    private void EnsureFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_bookmarksFile)!);
        if (File.Exists(_bookmarksFile)) return;

        var nodes = RootNodes();

        // Migration depuis bookmarks.tsv (format précédent non chiffré)
        if (_legacyBookmarksFile is not null && File.Exists(_legacyBookmarksFile))
        {
            var legacyContent = File.ReadAllText(_legacyBookmarksFile, Encoding.UTF8);
            LumoraFile.WriteAllText(_bookmarksFile, legacyContent);
            try { File.Delete(_legacyBookmarksFile); } catch { }
            return;
        }

        // Migration depuis favorites.tsv (très ancien format plat)
        if (_legacyFavoritesFile is not null && File.Exists(_legacyFavoritesFile))
        {
            var folderId = "legacy-flat-favorites";
            nodes.Add(new BookmarkNode(folderId, OtherRootId, BookmarkKind.Folder, 0, "Anciens favoris importes", string.Empty));
            var index = 0u;
            foreach (var line in File.ReadLines(_legacyFavoritesFile))
            {
                var parts = line.Split('\t');
                if (parts.Length < 3) continue;
                var url = DecodeField(parts[1]);
                if (!IsWebUrl(url)) continue;
                nodes.Add(new BookmarkNode($"legacy-{index + 1}", folderId, BookmarkKind.Url, index++, CleanTitle(DecodeField(parts[2]), url), url));
            }
        }

        WriteNodes(nodes);
    }

    private int AddImportItems(List<BookmarkNode> nodes, string parentId, IReadOnlyList<BookmarkImportItem> items, ImportState state)
    {
        var imported = 0;
        foreach (var item in items)
        {
            if (item.Url is not null)
            {
                if (!IsWebUrl(item.Url) || !state.ExistingUrls.Add(item.Url))
                {
                    continue;
                }

                nodes.Add(new BookmarkNode(state.NextId("bookmark"), parentId, BookmarkKind.Url, state.NextPosition(parentId), CleanTitle(item.Title, item.Url), item.Url, CleanLocalPath(item.IconPath)));
                imported++;
                continue;
            }

            var folderTitle = CleanTitle(item.Title, string.Empty);
            var existingFolder = FindExistingImportFolder(nodes, parentId, folderTitle);
            var folderId = existingFolder?.Id ?? state.NextId("folder");
            var createdFolder = existingFolder is null;
            if (createdFolder)
            {
                nodes.Add(new BookmarkNode(folderId, parentId, BookmarkKind.Folder, state.NextPosition(parentId), folderTitle, string.Empty));
            }

            var childImported = AddImportItems(nodes, folderId, item.Children, state);
            imported += childImported;

            if (createdFolder && childImported == 0 && nodes.All(node => node.ParentId != folderId))
            {
                nodes.RemoveAll(node => node.Id == folderId);
            }
        }

        return imported;
    }

    private static BookmarkNode? FindExistingImportFolder(IReadOnlyList<BookmarkNode> nodes, string parentId, string title) =>
        nodes
            .Where(node => node.ParentId == parentId && node.Kind == BookmarkKind.Folder)
            .OrderBy(node => node.Position)
            .FirstOrDefault(node => SameImportFolderTitle(node.Title, title));

    private static int MergeSiblingImportFolders(List<BookmarkNode> nodes)
    {
        var removed = 0;
        var changed = true;
        while (changed)
        {
            changed = false;
            var duplicateGroup = nodes
                .Where(node => !node.IsRoot && node.Kind == BookmarkKind.Folder)
                .GroupBy(node => node.ParentId)
                .SelectMany(parentGroup => parentGroup
                    .GroupBy(node => ImportFolderKey(node.Title), StringComparer.OrdinalIgnoreCase)
                    .Where(titleGroup => titleGroup.Count() > 1))
                .FirstOrDefault();

            if (duplicateGroup is null)
            {
                continue;
            }

            var keep = duplicateGroup.OrderBy(node => node.Position).ThenBy(node => node.Id, StringComparer.Ordinal).First();
            foreach (var duplicate in duplicateGroup.Where(node => node.Id != keep.Id).ToList())
            {
                for (var index = 0; index < nodes.Count; index++)
                {
                    var child = nodes[index];
                    if (child.ParentId == duplicate.Id)
                    {
                        nodes[index] = child with
                        {
                            ParentId = keep.Id,
                            Position = NextPosition(nodes, keep.Id)
                        };
                    }
                }

                nodes.RemoveAll(node => node.Id == duplicate.Id);
                removed++;
                changed = true;
            }
        }

        return removed;
    }

    private static int RemoveDuplicateUrls(List<BookmarkNode> nodes)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var removeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes
                     .Where(node => node.Kind == BookmarkKind.Url)
                     .OrderBy(node => node.ParentId)
                     .ThenBy(node => node.Position))
        {
            if (!seen.Add(node.Url))
            {
                removeIds.Add(node.Id);
            }
        }

        if (removeIds.Count == 0)
        {
            return 0;
        }

        nodes.RemoveAll(node => removeIds.Contains(node.Id));
        return removeIds.Count;
    }

    private static bool SameImportFolderTitle(string left, string right) =>
        ImportFolderKey(left).Equals(ImportFolderKey(right), StringComparison.OrdinalIgnoreCase);

    private static string ImportFolderKey(string title) => title.Trim();

    private void WriteNodes(IReadOnlyList<BookmarkNode> nodes)
    {
        if (_isGuest)
        {
            _guestNodes = nodes.ToList();
            return;
        }

        var builder = new StringBuilder();
        foreach (var node in Sort(nodes))
        {
            builder.Append(EncodeField(node.Id)).Append('\t')
                .Append(EncodeField(node.ParentId)).Append('\t')
                .Append(node.Kind == BookmarkKind.Folder ? "folder" : "url").Append('\t')
                .Append(node.Position).Append('\t')
                .Append(EncodeField(node.Title)).Append('\t')
                .Append(EncodeField(node.Url)).Append('\t')
                .Append(EncodeField(node.IconPath)).Append('\n');
        }

        LumoraFile.WriteAllText(_bookmarksFile, builder.ToString());
    }

    private void Backup(string label)
    {
        if (!File.Exists(_bookmarksFile))
        {
            return;
        }

        var backupPath = Path.Combine(Path.GetDirectoryName(_bookmarksFile)!, $"bookmarks.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.{label}.bak.tsv");
        File.Copy(_bookmarksFile, backupPath, overwrite: true);
    }

    private static List<BookmarkNode> RootNodes() =>
        new()
        {
            new BookmarkNode(ToolbarRootId, string.Empty, BookmarkKind.Folder, 0, "Barre des favoris", string.Empty),
            new BookmarkNode(OtherRootId, string.Empty, BookmarkKind.Folder, 1, "Autres favoris", string.Empty)
        };

    private static void EnsureRoots(List<BookmarkNode> nodes)
    {
        foreach (var root in RootNodes())
        {
            if (nodes.All(node => node.Id != root.Id))
            {
                nodes.Add(root);
            }
        }
    }

    private static BookmarkNode? ParseLine(string line)
    {
        var parts = line.Split('\t');
        if (parts.Length < 6 || !uint.TryParse(parts[3], out var position))
        {
            return null;
        }

        var kind = parts[2] switch
        {
            "folder" => BookmarkKind.Folder,
            "url" => BookmarkKind.Url,
            _ => (BookmarkKind?)null
        };

        var iconPath = parts.Length >= 7 ? CleanLocalPath(DecodeField(parts[6])) : string.Empty;
        return kind is null
            ? null
            : new BookmarkNode(DecodeField(parts[0]), DecodeField(parts[1]), kind.Value, position, DecodeField(parts[4]), DecodeField(parts[5]), iconPath);
    }

    private static List<BookmarkNode> Sort(IEnumerable<BookmarkNode> nodes) =>
        nodes.OrderBy(node => node.ParentId).ThenBy(node => node.Position).ThenBy(node => node.Title).ToList();

    private static uint NextPosition(IEnumerable<BookmarkNode> nodes, string parentId)
    {
        var positions = nodes.Where(node => node.ParentId == parentId).Select(node => node.Position).ToList();
        return positions.Count == 0 ? 0 : positions.Max() + 1;
    }

    private static string NextNodeId(IEnumerable<BookmarkNode> nodes, string prefix)
    {
        var existing = nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var index = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        while (true)
        {
            var id = $"{prefix}-{index++}";
            if (!existing.Contains(id))
            {
                return id;
            }
        }
    }

    private static string CleanTitle(string title, string url)
    {
        title = NormalizeBookmarkTitle(title, url);
        if (!string.IsNullOrEmpty(title))
        {
            return title.Length > 160 ? title[..160] : title;
        }

        return IsWebUrl(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "Dossier sans nom";
    }

    private static string NormalizeBookmarkTitle(string title, string url)
    {
        if (title.Contains(InvisibleTitle, StringComparison.Ordinal))
        {
            return title;
        }

        var trimmed = title.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            return trimmed;
        }

        return IsWebUrl(url) ? InvisibleTitle : string.Empty;
    }

    private static string CleanLocalPath(string path)
    {
        path = path.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static bool IsWebUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static string EncodeField(string value)
    {
        var builder = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            if ((b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z') || (b >= '0' && b <= '9') || b is (byte)'-' or (byte)'_' or (byte)'.' or (byte)'~')
            {
                builder.Append((char)b);
            }
            else
            {
                builder.Append('%').Append(b.ToString("X2"));
            }
        }

        return builder.ToString();
    }

    private static string DecodeField(string value)
    {
        var bytes = new List<byte>();
        for (var index = 0; index < value.Length;)
        {
            if (value[index] == '%' && index + 2 < value.Length && byte.TryParse(value.Substring(index + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var decoded))
            {
                bytes.Add(decoded);
                index += 3;
            }
            else
            {
                bytes.Add((byte)value[index]);
                index++;
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}

public sealed record BookmarkImportItem(string Title, string? Url, List<BookmarkImportItem> Children, string IconPath = "")
{
    public static BookmarkImportItem Folder(string title, IEnumerable<BookmarkImportItem> children) =>
        new(title, null, children.ToList());

    public static BookmarkImportItem UrlItem(string title, string url, string iconPath = "") =>
        new(title, url, new List<BookmarkImportItem>(), iconPath);
}

public sealed record BookmarkImportTree(List<BookmarkImportItem> Toolbar, List<BookmarkImportItem> Other)
{
    private static readonly Regex AnchorRegex = new(
        "<A\\s+[^>]*HREF\\s*=\\s*[\"'](?<url>[^\"']+)[\"'][^>]*>(?<title>.*?)</A>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    public static BookmarkImportTree FromHtml(string html, string sourceName)
    {
        var items = AnchorRegex.Matches(html)
            .Select(match => BookmarkImportItem.UrlItem(
                WebUtility.HtmlDecode(Regex.Replace(match.Groups["title"].Value, "<.*?>", string.Empty)).Trim(),
                WebUtility.HtmlDecode(match.Groups["url"].Value).Trim()))
            .Where(item => BookmarkStore.IsWebUrl(item.Url ?? string.Empty))
            .ToList();

        return new BookmarkImportTree(new List<BookmarkImportItem>(), new List<BookmarkImportItem>
        {
            BookmarkImportItem.Folder($"Import HTML {sourceName}", items)
        });
    }
}

public sealed record MigrationBrowserEntry(string Name, BrowserImportSource? Source)
{
    public string Label => Source is not null
        ? $"{Name}  —  {Source.Count} favori(s) détecté(s)"
        : $"{Name}  —  non détecté";
}

public sealed record BrowserImportSource(string Browser, string Profile, string Path, int Count)
{
    public string Label => $"{Browser} {Profile} - {Count} favoris";

    public static List<BrowserImportSource> Discover()
    {
        var sources = new List<BrowserImportSource>();
        foreach (var (browser, userData) in InstalledChromiumBrowsers.Roots())
        {
            if (!Directory.Exists(userData))
            {
                continue;
            }

            foreach (var profileDir in Directory.EnumerateDirectories(userData).Where(InstalledChromiumBrowsers.IsProfileDir))
            {
                var bookmarks = System.IO.Path.Combine(profileDir, "Bookmarks");
                if (!File.Exists(bookmarks))
                {
                    continue;
                }

                var count = CountUrls(bookmarks);
                if (count > 0)
                {
                    sources.Add(new BrowserImportSource(browser, System.IO.Path.GetFileName(profileDir), bookmarks, count));
                }
            }
        }

        // Firefox : format SQLite (places.sqlite), lu par FirefoxBookmarkReader.
        foreach (var (profileName, placesPath) in FirefoxBookmarkReader.DiscoverProfiles())
        {
            var count = FirefoxBookmarkReader.CountUrls(placesPath);
            if (count > 0)
            {
                sources.Add(new BrowserImportSource("Firefox", profileName, placesPath, count));
            }
        }

        return sources;
    }

    private bool IsFirefoxSource =>
        System.IO.Path.GetFileName(Path).Equals("places.sqlite", StringComparison.OrdinalIgnoreCase);

    public BookmarkImportTree ReadTree(IReadOnlyDictionary<string, string>? iconPathsByUrl = null)
    {
        if (IsFirefoxSource)
        {
            return FirefoxBookmarkReader.ReadTree(Path);
        }

        var json = JsonNode.Parse(File.ReadAllText(Path));
        var roots = json?["roots"];
        var other = ReadChildren(roots?["other"]?["children"], iconPathsByUrl);
        var synced = ReadChildren(roots?["synced"]?["children"], iconPathsByUrl);
        if (synced.Count > 0)
        {
            other.Add(BookmarkImportItem.Folder("Favoris mobiles Chrome", synced));
        }

        return new BookmarkImportTree(
            ReadChildren(roots?["bookmark_bar"]?["children"], iconPathsByUrl),
            other);
    }

    // wantedUrls : URLs des favoris a importer (pas tout l'historique de
    // navigation). La base Favicons d'un navigateur contient l'icone de
    // *chaque site visite depuis des annees*, pas seulement les favoris -
    // sans ce filtre, un import convertissait et ecrivait sur disque des
    // dizaines de milliers d'icones inutiles a chaque import, ce qui a bloque
    // completement l'application (signale par l'utilisateur, 2026-08-22).
    public async Task<IReadOnlyDictionary<string, string>> CopyFaviconsAsync(string destinationDir, IReadOnlyCollection<string> wantedUrls)
    {
        if (IsFirefoxSource || wantedUrls.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var profileDir = System.IO.Path.GetDirectoryName(Path);
        if (string.IsNullOrWhiteSpace(profileDir))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var faviconDb = System.IO.Path.Combine(profileDir, "Favicons");
        if (!File.Exists(faviconDb))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        // page_url en base ne correspond pas toujours exactement a l'URL du
        // favori (slash final, requete...) mais partage generalement la meme
        // origine (schema+hote) - on filtre sur les deux pour ne pas perdre
        // d'icones legitimes tout en excluant le reste de l'historique.
        var wantedOrigins = wantedUrls.Select(PublicSuffixService.OriginOf).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var wantedExact = wantedUrls.ToHashSet(StringComparer.OrdinalIgnoreCase);

        Directory.CreateDirectory(destinationDir);
        var tempDb = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nova-favicons-{Guid.NewGuid():N}.db");
        try
        {
            File.Copy(faviconDb, tempDb, overwrite: true);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={tempDb};Mode=ReadOnly");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT m.page_url, b.image_data, COALESCE(b.width, 0) AS width
                FROM icon_mapping m
                JOIN favicon_bitmaps b ON b.icon_id = m.icon_id
                WHERE m.page_url IS NOT NULL AND b.image_data IS NOT NULL
                ORDER BY m.page_url, width DESC
                """;

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var pageUrl = reader.GetString(0);
                if (!BookmarkStore.IsWebUrl(pageUrl) || result.ContainsKey(pageUrl))
                {
                    continue;
                }

                if (!wantedExact.Contains(pageUrl) && !wantedOrigins.Contains(PublicSuffixService.OriginOf(pageUrl)))
                {
                    continue;
                }

                var bytes = (byte[])reader["image_data"];
                var png = await FaviconImageConverter.ToPngAsync(bytes);
                if (png is null || !FaviconQuality.IsUsablePng(png))
                {
                    continue;
                }

                var outputPath = System.IO.Path.Combine(destinationDir, $"{HashOrigin(pageUrl)}.png");
                await File.WriteAllBytesAsync(outputPath, png);
                result[pageUrl] = outputPath;
                result[PublicSuffixService.OriginOf(pageUrl)] = outputPath;
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            try { File.Delete(tempDb); } catch { }
        }
    }

    private static int CountUrls(string bookmarksFile)
    {
        try
        {
            var source = new BrowserImportSource(string.Empty, string.Empty, bookmarksFile, 0);
            var tree = source.ReadTree();
            return CountItems(tree.Toolbar) + CountItems(tree.Other);
        }
        catch
        {
            return 0;
        }
    }

    private static int CountItems(IEnumerable<BookmarkImportItem> items) =>
        items.Sum(item => item.Url is not null ? 1 : CountItems(item.Children));

    private static List<BookmarkImportItem> ReadChildren(JsonNode? children, IReadOnlyDictionary<string, string>? iconPathsByUrl = null)
    {
        var items = new List<BookmarkImportItem>();
        if (children is not JsonArray array)
        {
            return items;
        }

        foreach (var child in array)
        {
            var type = child?["type"]?.GetValue<string>() ?? string.Empty;
            var name = child?["name"]?.GetValue<string>() ?? string.Empty;
            if (type == "url")
            {
                var url = child?["url"]?.GetValue<string>() ?? string.Empty;
                if (BookmarkStore.IsWebUrl(url))
                {
                    var iconPath = string.Empty;
                    if (iconPathsByUrl is not null)
                    {
                        iconPathsByUrl.TryGetValue(url, out iconPath);
                        if (string.IsNullOrWhiteSpace(iconPath))
                        {
                            iconPathsByUrl.TryGetValue(PublicSuffixService.OriginOf(url), out iconPath);
                        }
                    }

                    items.Add(BookmarkImportItem.UrlItem(name, url, iconPath ?? string.Empty));
                }
            }
            else if (type == "folder")
            {
                items.Add(BookmarkImportItem.Folder(name, ReadChildren(child?["children"], iconPathsByUrl)));
            }
        }

        return items;
    }

    private static string HashOrigin(string url)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(PublicSuffixService.OriginOf(url).ToLowerInvariant()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public static class BookmarkTreePresenter
{
    public static IEnumerable<BookmarkListItem> FlattenFolders(IReadOnlyList<BookmarkNode> nodes)
    {
        foreach (var rootId in new[] { BookmarkStore.ToolbarRootId, BookmarkStore.OtherRootId })
        {
            var root = nodes.First(node => node.Id == rootId);
            yield return ItemForNode(nodes, root, 0);
            foreach (var item in FlattenFolderChildren(nodes, root.Id, 1))
            {
                yield return item;
            }
        }
    }

    public static BookmarkListItem ItemForNode(IReadOnlyList<BookmarkNode> nodes, BookmarkNode node, int depth)
    {
        var prefix = new string(' ', depth * 3);
        var icon = BookmarkGlyphs.For(node);
        var title = string.IsNullOrWhiteSpace(node.Title) ? "(sans nom)" : node.Title;
        if (BookmarkStore.IsIconOnlyTitle(node.Title))
        {
            title = "(icone seule)";
        }

        // Pluriel resolu (2026-08-10, "choses a revoir" - bug releve par
        // l'utilisateur sur sa propre capture d'ecran : "2 element(s)" et
        // "0 element(s)" affiches tels quels, le texte n'etait jamais
        // realise). Meme convention que BookmarkFolderChildCountLabel
        // (MainWindow.BookmarksFlyouts.cs, menu contextuel des favoris).
        var folderChildCount = nodes.Count(candidate => candidate.ParentId == node.Id);
        var detail = node.Kind == BookmarkKind.Folder
            ? folderChildCount switch
            {
                0 => "Dossier vide",
                1 => "1 élément",
                _ => $"{folderChildCount} éléments"
            }
            : node.Url;
        var hasIcon = node.Kind == BookmarkKind.Url &&
            !string.IsNullOrWhiteSpace(node.IconPath) &&
            FaviconQuality.IsUsablePngFile(node.IconPath);
        return new BookmarkListItem(
            icon,
            hasIcon ? node.IconUri : string.Empty,
            hasIcon ? Visibility.Visible : Visibility.Collapsed,
            hasIcon ? Visibility.Collapsed : Visibility.Visible,
            prefix + title,
            detail,
            node);
    }

    public static string Breadcrumb(IReadOnlyList<BookmarkNode> nodes, string folderId)
    {
        var parts = new List<string>();
        var currentId = folderId;
        while (!string.IsNullOrWhiteSpace(currentId))
        {
            var current = nodes.FirstOrDefault(node => node.Id == currentId);
            if (current is null)
            {
                break;
            }

            parts.Insert(0, current.Title);
            currentId = current.ParentId;
        }

        return string.Join(" > ", parts);
    }

    public static IEnumerable<BookmarkListItem> Search(IReadOnlyList<BookmarkNode> nodes, string query)
    {
        var normalized = query.Trim().ToLowerInvariant();
        return nodes
            .Where(node => !node.IsRoot)
            .Where(node =>
                node.Title.ToLowerInvariant().Contains(normalized) ||
                node.Url.ToLowerInvariant().Contains(normalized))
            .OrderBy(node => node.Kind == BookmarkKind.Url ? 1 : 0)
            .ThenBy(node => node.Title)
            .Select(node => ItemForNode(nodes, node, 0));
    }

    private static IEnumerable<BookmarkListItem> FlattenFolderChildren(IReadOnlyList<BookmarkNode> nodes, string parentId, int depth)
    {
        foreach (var node in nodes
                     .Where(candidate => candidate.ParentId == parentId && candidate.Kind == BookmarkKind.Folder)
                     .OrderBy(candidate => candidate.Position))
        {
            yield return ItemForNode(nodes, node, depth);
            foreach (var child in FlattenFolderChildren(nodes, node.Id, depth + 1))
            {
                yield return child;
            }
        }
    }
}

public static class BookmarkHtmlExporter
{
    public static string Export(IReadOnlyList<BookmarkNode> nodes)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
        builder.AppendLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
        builder.AppendLine("<TITLE>Bookmarks</TITLE>");
        builder.AppendLine("<H1>Bookmarks</H1>");
        builder.AppendLine("<DL><p>");
        WriteChildren(builder, nodes, BookmarkStore.ToolbarRootId, 1, "Barre des favoris");
        WriteChildren(builder, nodes, BookmarkStore.OtherRootId, 1, "Autres favoris");
        builder.AppendLine("</DL><p>");
        return builder.ToString();
    }

    private static void WriteChildren(StringBuilder builder, IReadOnlyList<BookmarkNode> nodes, string parentId, int depth, string title)
    {
        var indent = new string(' ', depth * 4);
        builder.Append(indent).Append("<DT><H3>").Append(WebUtility.HtmlEncode(title)).AppendLine("</H3>");
        builder.Append(indent).AppendLine("<DL><p>");
        foreach (var node in nodes.Where(candidate => candidate.ParentId == parentId).OrderBy(candidate => candidate.Position))
        {
            if (node.Kind == BookmarkKind.Url)
            {
                builder.Append(indent).Append("    <DT><A HREF=\"")
                    .Append(WebUtility.HtmlEncode(node.Url))
                    .Append("\">")
                    .Append(WebUtility.HtmlEncode(node.Title))
                    .AppendLine("</A>");
            }
            else
            {
                WriteChildren(builder, nodes, node.Id, depth + 1, node.Title);
            }
        }

        builder.Append(indent).AppendLine("</DL><p>");
    }
}
