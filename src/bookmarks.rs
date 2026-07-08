use std::collections::BTreeSet;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use crate::browser_data;

pub const TOOLBAR_ROOT_ID: &str = "root-toolbar";
pub const OTHER_ROOT_ID: &str = "root-other";

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum BookmarkKind {
    Folder,
    Url,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct BookmarkNode {
    pub id: String,
    pub parent_id: String,
    pub kind: BookmarkKind,
    pub title: String,
    pub url: String,
    pub position: u32,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum ImportedBookmarkItem {
    Folder {
        title: String,
        children: Vec<ImportedBookmarkItem>,
    },
    Url {
        title: String,
        url: String,
    },
}

#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct ImportedBookmarkTree {
    pub toolbar: Vec<ImportedBookmarkItem>,
    pub other: Vec<ImportedBookmarkItem>,
}

#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct BookmarkImportSummary {
    pub imported: usize,
    pub folders: usize,
    pub backup_path: Option<PathBuf>,
}

#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct BookmarkClearSummary {
    pub removed: usize,
    pub backup_path: Option<PathBuf>,
}

#[derive(Clone, Debug)]
pub struct BookmarkStore {
    bookmarks_file: PathBuf,
    legacy_favorites_file: PathBuf,
}

impl BookmarkStore {
    pub fn new(bookmarks_file: PathBuf, legacy_favorites_file: PathBuf) -> Self {
        Self {
            bookmarks_file,
            legacy_favorites_file,
        }
    }

    pub fn ensure_file(&self) -> Result<(), String> {
        ensure_parent(&self.bookmarks_file)?;
        if self.bookmarks_file.exists() {
            return Ok(());
        }

        let mut nodes = root_nodes();
        let legacy = read_legacy_favorites(&self.legacy_favorites_file)?;
        if !legacy.is_empty() {
            let archive_id = "legacy-flat-favorites".to_string();
            nodes.push(BookmarkNode {
                id: archive_id.clone(),
                parent_id: OTHER_ROOT_ID.to_string(),
                kind: BookmarkKind::Folder,
                title: "Anciens favoris importes".to_string(),
                url: String::new(),
                position: 0,
            });
            for (index, favorite) in legacy.into_iter().enumerate() {
                nodes.push(BookmarkNode {
                    id: format!("legacy-{}", index + 1),
                    parent_id: archive_id.clone(),
                    kind: BookmarkKind::Url,
                    title: clean_title(&favorite.title, &favorite.url),
                    url: favorite.url,
                    position: index as u32,
                });
            }
        }

        self.write_nodes(&nodes)
    }

    pub fn all_nodes(&self) -> Result<Vec<BookmarkNode>, String> {
        self.ensure_file()?;
        self.read_nodes()
    }

    pub fn children_of(&self, parent_id: &str) -> Result<Vec<BookmarkNode>, String> {
        let mut children: Vec<BookmarkNode> = self
            .all_nodes()?
            .into_iter()
            .filter(|node| node.parent_id == parent_id)
            .collect();
        sort_nodes(&mut children);
        Ok(children)
    }

    pub fn toolbar_children(&self) -> Result<Vec<BookmarkNode>, String> {
        self.children_of(TOOLBAR_ROOT_ID)
    }

    pub fn replace_with_import(
        &self,
        import: &ImportedBookmarkTree,
        _source_label: &str,
    ) -> Result<BookmarkImportSummary, String> {
        self.ensure_file()?;
        let backup_path = self.backup_bookmarks("before-structured-import")?;
        let mut builder = TreeBuilder::new();
        builder.add_roots();
        builder.add_items(TOOLBAR_ROOT_ID, &import.toolbar);
        builder.add_items(OTHER_ROOT_ID, &import.other);
        let summary = BookmarkImportSummary {
            imported: builder.imported,
            folders: builder.folders,
            backup_path,
        };
        self.write_nodes(&builder.nodes)?;
        Ok(summary)
    }

    pub fn merge_import(
        &self,
        import: &ImportedBookmarkTree,
        _source_label: &str,
    ) -> Result<BookmarkImportSummary, String> {
        self.ensure_file()?;
        let backup_path = self.backup_bookmarks("before-structured-merge")?;
        let mut builder = TreeBuilder::from_nodes(self.read_nodes()?);
        builder.add_items(TOOLBAR_ROOT_ID, &import.toolbar);
        builder.add_items(OTHER_ROOT_ID, &import.other);
        let summary = BookmarkImportSummary {
            imported: builder.imported,
            folders: builder.folders,
            backup_path,
        };
        self.write_nodes(&builder.nodes)?;
        Ok(summary)
    }

    pub fn toggle_toolbar_url(&self, url: &str, title: &str) -> Result<bool, String> {
        if !browser_data::is_web_url(url) {
            return Err("ouvre une page web avant de l'ajouter aux favoris.".to_string());
        }

        let mut nodes = self.read_nodes()?;
        if let Some(index) = nodes
            .iter()
            .position(|node| node.kind == BookmarkKind::Url && node.url == url)
        {
            let removed_id = nodes[index].id.clone();
            nodes.remove(index);
            remove_descendants(&mut nodes, &removed_id);
            self.write_nodes(&nodes)?;
            return Ok(false);
        }

        let position = next_position(&nodes, TOOLBAR_ROOT_ID);
        nodes.push(BookmarkNode {
            id: next_node_id(&nodes, "bookmark"),
            parent_id: TOOLBAR_ROOT_ID.to_string(),
            kind: BookmarkKind::Url,
            title: clean_title(title, url),
            url: url.to_string(),
            position,
        });
        self.write_nodes(&nodes)?;
        Ok(true)
    }

    pub fn remove_node(&self, id: &str) -> Result<bool, String> {
        if id == TOOLBAR_ROOT_ID || id == OTHER_ROOT_ID {
            return Ok(false);
        }

        let mut nodes = self.read_nodes()?;
        let Some(index) = nodes.iter().position(|node| node.id == id) else {
            return Ok(false);
        };
        let removed_id = nodes[index].id.clone();
        nodes.remove(index);
        remove_descendants(&mut nodes, &removed_id);
        self.write_nodes(&nodes)?;
        Ok(true)
    }

    /// Ajoute un lien URL sous un parent donné. Retourne l'ID créé.
    pub fn add_url(&self, parent_id: &str, url: &str, title: &str) -> Result<String, String> {
        let mut nodes = self.read_nodes()?;
        let id = next_node_id(&nodes, "bookmark");
        let position = next_position(&nodes, parent_id);
        nodes.push(BookmarkNode {
            id: id.clone(),
            parent_id: parent_id.to_string(),
            kind: BookmarkKind::Url,
            title: clean_title(title, url),
            url: url.to_string(),
            position,
        });
        self.write_nodes(&nodes)?;
        Ok(id)
    }

    /// Crée un dossier sous un parent donné. Retourne l'ID créé.
    pub fn add_folder(&self, parent_id: &str, title: &str) -> Result<String, String> {
        let mut nodes = self.read_nodes()?;
        let id = next_node_id(&nodes, "folder");
        let position = next_position(&nodes, parent_id);
        nodes.push(BookmarkNode {
            id: id.clone(),
            parent_id: parent_id.to_string(),
            kind: BookmarkKind::Folder,
            title: title.chars().take(160).collect(),
            url: String::new(),
            position,
        });
        self.write_nodes(&nodes)?;
        Ok(id)
    }

    /// Renomme un nœud existant.
    pub fn rename_node(&self, id: &str, new_title: &str) -> Result<bool, String> {
        let mut nodes = self.read_nodes()?;
        let Some(node) = nodes.iter_mut().find(|n| n.id == id) else {
            return Ok(false);
        };
        node.title = new_title.chars().take(160).collect();
        self.write_nodes(&nodes)?;
        Ok(true)
    }

    /// Déplace un nœud vers un nouveau parent.
    pub fn move_node(&self, id: &str, new_parent_id: &str) -> Result<bool, String> {
        if id == TOOLBAR_ROOT_ID || id == OTHER_ROOT_ID {
            return Ok(false);
        }
        let mut nodes = self.read_nodes()?;
        let position = next_position(&nodes, new_parent_id);
        let Some(node) = nodes.iter_mut().find(|n| n.id == id) else {
            return Ok(false);
        };
        node.parent_id = new_parent_id.to_string();
        node.position = position;
        self.write_nodes(&nodes)?;
        Ok(true)
    }

    pub fn clear_with_backup(&self) -> Result<BookmarkClearSummary, String> {
        self.ensure_file()?;
        let current = self.read_nodes()?;
        let removed = current
            .iter()
            .filter(|node| node.id != TOOLBAR_ROOT_ID && node.id != OTHER_ROOT_ID)
            .count();
        let backup_path = self.backup_bookmarks("clear-bookmarks")?;
        self.write_nodes(&root_nodes())?;
        Ok(BookmarkClearSummary {
            removed,
            backup_path,
        })
    }

    fn read_nodes(&self) -> Result<Vec<BookmarkNode>, String> {
        self.ensure_file()?;
        let content = std::fs::read_to_string(&self.bookmarks_file)
            .map_err(|error| format!("favoris structures: lecture impossible: {error}"))?;
        let mut nodes: Vec<BookmarkNode> = content.lines().filter_map(parse_node_line).collect();
        ensure_roots(&mut nodes);
        sort_nodes(&mut nodes);
        Ok(nodes)
    }

    fn write_nodes(&self, nodes: &[BookmarkNode]) -> Result<(), String> {
        ensure_parent(&self.bookmarks_file)?;
        let mut content = String::new();
        let mut ordered = nodes.to_vec();
        sort_nodes(&mut ordered);
        for node in ordered {
            content.push_str(&format!(
                "{}\t{}\t{}\t{}\t{}\t{}\n",
                encode_field(&node.id),
                encode_field(&node.parent_id),
                match node.kind {
                    BookmarkKind::Folder => "folder",
                    BookmarkKind::Url => "url",
                },
                node.position,
                encode_field(&node.title),
                encode_field(&node.url)
            ));
        }
        std::fs::write(&self.bookmarks_file, content)
            .map_err(|error| format!("favoris structures: ecriture impossible: {error}"))
    }

    fn backup_bookmarks(&self, label: &str) -> Result<Option<PathBuf>, String> {
        if !self.bookmarks_file.exists() {
            return Ok(None);
        }
        let parent = self
            .bookmarks_file
            .parent()
            .ok_or_else(|| "favoris structures: chemin invalide".to_string())?;
        let backup_path = parent.join(format!(
            "bookmarks.{}.{}.bak.tsv",
            unix_now(),
            sanitize_label(label)
        ));
        std::fs::copy(&self.bookmarks_file, &backup_path)
            .map_err(|error| format!("favoris structures: sauvegarde impossible: {error}"))?;
        Ok(Some(backup_path))
    }
}

struct TreeBuilder {
    nodes: Vec<BookmarkNode>,
    next: u64,
    imported: usize,
    folders: usize,
}

impl TreeBuilder {
    fn new() -> Self {
        Self {
            nodes: Vec::new(),
            next: 1,
            imported: 0,
            folders: 0,
        }
    }

    fn from_nodes(nodes: Vec<BookmarkNode>) -> Self {
        Self {
            nodes,
            next: unix_now(),
            imported: 0,
            folders: 0,
        }
    }

    fn add_roots(&mut self) {
        self.nodes.extend(root_nodes());
    }

    fn next_id(&mut self, prefix: &str) -> String {
        loop {
            let id = format!("{prefix}-{}", self.next);
            self.next += 1;
            if !self.nodes.iter().any(|node| node.id == id) {
                return id;
            }
        }
    }

    fn add_items(&mut self, parent_id: &str, items: &[ImportedBookmarkItem]) {
        for (position, item) in items.iter().enumerate() {
            match item {
                ImportedBookmarkItem::Folder { title, children } => {
                    let id = self.next_id("folder");
                    self.nodes.push(BookmarkNode {
                        id: id.clone(),
                        parent_id: parent_id.to_string(),
                        kind: BookmarkKind::Folder,
                        title: clean_title(title, ""),
                        url: String::new(),
                        position: position as u32,
                    });
                    self.folders += 1;
                    self.add_items(&id, children);
                }
                ImportedBookmarkItem::Url { title, url } => {
                    if !browser_data::is_web_url(url) {
                        continue;
                    }
                    let id = self.next_id("bookmark");
                    self.nodes.push(BookmarkNode {
                        id,
                        parent_id: parent_id.to_string(),
                        kind: BookmarkKind::Url,
                        title: clean_title(title, url),
                        url: url.to_string(),
                        position: position as u32,
                    });
                    self.imported += 1;
                }
            }
        }
    }
}

fn root_nodes() -> Vec<BookmarkNode> {
    vec![
        BookmarkNode {
            id: TOOLBAR_ROOT_ID.to_string(),
            parent_id: String::new(),
            kind: BookmarkKind::Folder,
            title: "Barre des favoris".to_string(),
            url: String::new(),
            position: 0,
        },
        BookmarkNode {
            id: OTHER_ROOT_ID.to_string(),
            parent_id: String::new(),
            kind: BookmarkKind::Folder,
            title: "Autres favoris".to_string(),
            url: String::new(),
            position: 1,
        },
    ]
}

fn ensure_roots(nodes: &mut Vec<BookmarkNode>) {
    for root in root_nodes() {
        if !nodes.iter().any(|node| node.id == root.id) {
            nodes.push(root);
        }
    }
}

fn parse_node_line(line: &str) -> Option<BookmarkNode> {
    let mut parts = line.split('\t');
    let id = decode_field(parts.next()?)?;
    let parent_id = decode_field(parts.next()?)?;
    let kind = match parts.next()? {
        "folder" => BookmarkKind::Folder,
        "url" => BookmarkKind::Url,
        _ => return None,
    };
    let position = parts.next()?.parse().ok()?;
    let title = decode_field(parts.next()?)?;
    let url = decode_field(parts.next()?)?;
    Some(BookmarkNode {
        id,
        parent_id,
        kind,
        title,
        url,
        position,
    })
}

fn sort_nodes(nodes: &mut [BookmarkNode]) {
    nodes.sort_by(|left, right| {
        left.parent_id
            .cmp(&right.parent_id)
            .then_with(|| left.position.cmp(&right.position))
            .then_with(|| left.title.cmp(&right.title))
    });
}

fn remove_descendants(nodes: &mut Vec<BookmarkNode>, parent_id: &str) {
    let mut to_remove = BTreeSet::from([parent_id.to_string()]);
    loop {
        let before = to_remove.len();
        for node in nodes.iter() {
            if to_remove.contains(&node.parent_id) {
                to_remove.insert(node.id.clone());
            }
        }
        if to_remove.len() == before {
            break;
        }
    }
    nodes.retain(|node| !to_remove.contains(&node.id));
}

fn next_position(nodes: &[BookmarkNode], parent_id: &str) -> u32 {
    nodes
        .iter()
        .filter(|node| node.parent_id == parent_id)
        .map(|node| node.position)
        .max()
        .map(|position| position + 1)
        .unwrap_or_default()
}

fn next_node_id(nodes: &[BookmarkNode], prefix: &str) -> String {
    let mut index = unix_now();
    loop {
        let id = format!("{prefix}-{index}");
        if !nodes.iter().any(|node| node.id == id) {
            return id;
        }
        index += 1;
    }
}

fn read_legacy_favorites(path: &Path) -> Result<Vec<LegacyFavorite>, String> {
    if !path.exists() {
        return Ok(Vec::new());
    }
    let content = std::fs::read_to_string(path)
        .map_err(|error| format!("migration favoris plats: lecture impossible: {error}"))?;
    Ok(content.lines().filter_map(parse_legacy_line).collect())
}

#[derive(Clone, Debug)]
struct LegacyFavorite {
    title: String,
    url: String,
}

fn parse_legacy_line(line: &str) -> Option<LegacyFavorite> {
    let mut parts = line.split('\t');
    let _saved_at = parts.next()?;
    let url = decode_field(parts.next()?)?;
    let title = decode_field(parts.next()?)?;
    if !browser_data::is_web_url(&url) {
        return None;
    }
    Some(LegacyFavorite { title, url })
}

fn ensure_parent(path: &Path) -> Result<(), String> {
    if let Some(parent) = path.parent() {
        std::fs::create_dir_all(parent)
            .map_err(|error| format!("favoris structures: creation dossier impossible: {error}"))?;
    }
    Ok(())
}

fn clean_title(title: &str, url: &str) -> String {
    let title = title.trim();
    if !title.is_empty() {
        return title.chars().take(160).collect();
    }
    if !url.is_empty() {
        return crate::privacy::display_host(url);
    }
    "Dossier sans nom".to_string()
}

fn encode_field(value: &str) -> String {
    let mut encoded = String::new();
    for byte in value.as_bytes() {
        match *byte {
            b'A'..=b'Z' | b'a'..=b'z' | b'0'..=b'9' | b'-' | b'_' | b'.' | b'~' => {
                encoded.push(*byte as char);
            }
            byte => encoded.push_str(&format!("%{byte:02X}")),
        }
    }
    encoded
}

fn decode_field(value: &str) -> Option<String> {
    let bytes = value.as_bytes();
    let mut decoded = Vec::with_capacity(bytes.len());
    let mut index = 0;
    while index < bytes.len() {
        match bytes[index] {
            b'%' if index + 2 < bytes.len() => {
                decoded.push(hex_pair(bytes[index + 1], bytes[index + 2])?);
                index += 3;
            }
            byte => {
                decoded.push(byte);
                index += 1;
            }
        }
    }
    String::from_utf8(decoded).ok()
}

fn hex_pair(high: u8, low: u8) -> Option<u8> {
    Some(hex_digit(high)? * 16 + hex_digit(low)?)
}

fn hex_digit(value: u8) -> Option<u8> {
    match value {
        b'0'..=b'9' => Some(value - b'0'),
        b'a'..=b'f' => Some(value - b'a' + 10),
        b'A'..=b'F' => Some(value - b'A' + 10),
        _ => None,
    }
}

fn unix_now() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|duration| duration.as_secs())
        .unwrap_or_default()
}

fn sanitize_label(label: &str) -> String {
    let cleaned: String = label
        .chars()
        .filter(|character| character.is_ascii_alphanumeric() || *character == '-')
        .take(32)
        .collect();
    if cleaned.is_empty() {
        "backup".to_string()
    } else {
        cleaned
    }
}

#[cfg(test)]
mod tests {
    use super::{BookmarkKind, BookmarkStore, ImportedBookmarkItem, ImportedBookmarkTree};

    #[test]
    fn migrates_legacy_flat_favorites_outside_toolbar() {
        let root =
            std::env::temp_dir().join(format!("pulse-bookmarks-migrate-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&root);
        std::fs::create_dir_all(&root).unwrap();
        let legacy = root.join("favorites.tsv");
        std::fs::write(&legacy, "1\thttps%3A%2F%2Fexample.com\tExample\n").unwrap();
        let store = BookmarkStore::new(root.join("bookmarks.tsv"), legacy);

        store.ensure_file().unwrap();

        assert!(store.toolbar_children().unwrap().is_empty());
        assert!(
            store
                .all_nodes()
                .unwrap()
                .iter()
                .any(|node| node.title == "Anciens favoris importes")
        );
        let _ = std::fs::remove_dir_all(&root);
    }

    #[test]
    fn replaces_with_imported_toolbar_and_folders() {
        let root =
            std::env::temp_dir().join(format!("pulse-bookmarks-import-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&root);
        let store = BookmarkStore::new(root.join("bookmarks.tsv"), root.join("favorites.tsv"));
        let import = ImportedBookmarkTree {
            toolbar: vec![
                ImportedBookmarkItem::Folder {
                    title: "Dev".to_string(),
                    children: vec![ImportedBookmarkItem::Url {
                        title: "Rust".to_string(),
                        url: "https://www.rust-lang.org".to_string(),
                    }],
                },
                ImportedBookmarkItem::Url {
                    title: "Example".to_string(),
                    url: "https://example.com".to_string(),
                },
            ],
            other: Vec::new(),
        };

        let summary = store.replace_with_import(&import, "Test").unwrap();
        let toolbar = store.toolbar_children().unwrap();

        assert_eq!(summary.imported, 2);
        assert_eq!(summary.folders, 1);
        assert_eq!(toolbar.len(), 2);
        assert!(toolbar.iter().any(|node| node.kind == BookmarkKind::Folder));
        let _ = std::fs::remove_dir_all(&root);
    }

    #[test]
    fn toggles_toolbar_bookmark() {
        let root =
            std::env::temp_dir().join(format!("pulse-bookmarks-toggle-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&root);
        let store = BookmarkStore::new(root.join("bookmarks.tsv"), root.join("favorites.tsv"));

        assert!(
            store
                .toggle_toolbar_url("https://example.com", "Example")
                .unwrap()
        );
        assert_eq!(store.toolbar_children().unwrap().len(), 1);
        assert!(
            !store
                .toggle_toolbar_url("https://example.com", "Example")
                .unwrap()
        );
        assert!(store.toolbar_children().unwrap().is_empty());
        let _ = std::fs::remove_dir_all(&root);
    }
}
