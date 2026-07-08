use std::collections::BTreeMap;
use std::path::{Path, PathBuf};

use crate::{
    bookmarks::{BookmarkStore, ImportedBookmarkItem, ImportedBookmarkTree},
    browser_data,
};

#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct BrowserImportReport {
    pub source_label: String,
    pub mode: ImportMode,
    pub parsed: usize,
    pub added: usize,
    pub folders: usize,
    pub skipped: usize,
    pub invalid: usize,
    pub backup_path: Option<PathBuf>,
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum ImportMode {
    #[default]
    Merge,
    Replace,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct BookmarkImportSource {
    pub browser_name: String,
    pub profile_name: String,
    pub path: PathBuf,
    pub favorite_count: usize,
    pub read_error: Option<String>,
}

impl BookmarkImportSource {
    pub fn label(&self) -> String {
        if let Some(error) = &self.read_error {
            format!(
                "{} {} - illisible ({error})",
                self.browser_name, self.profile_name
            )
        } else {
            format!(
                "{} {} - {} favoris",
                self.browser_name, self.profile_name, self.favorite_count
            )
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
#[allow(dead_code)]
struct ImportedFavorite {
    title: String,
    url: String,
}

#[derive(Clone, Debug, PartialEq)]
enum JsonValue {
    Null,
    Bool,
    Number,
    String(String),
    Array(Vec<JsonValue>),
    Object(BTreeMap<String, JsonValue>),
}

pub fn discover_import_sources() -> Vec<BookmarkImportSource> {
    known_chromium_bookmark_files()
        .into_iter()
        .map(|source| match std::fs::read_to_string(&source.path) {
            Ok(content) => match parse_chromium_bookmark_tree(&content) {
                Ok(tree) => BookmarkImportSource {
                    browser_name: source.browser_name,
                    profile_name: source.profile_name,
                    path: source.path,
                    favorite_count: count_imported_urls(&tree),
                    read_error: None,
                },
                Err(error) => BookmarkImportSource {
                    browser_name: source.browser_name,
                    profile_name: source.profile_name,
                    path: source.path,
                    favorite_count: 0,
                    read_error: Some(error),
                },
            },
            Err(error) => BookmarkImportSource {
                browser_name: source.browser_name,
                profile_name: source.profile_name,
                path: source.path,
                favorite_count: 0,
                read_error: Some(format!("lecture impossible ({error})")),
            },
        })
        .collect()
}

pub fn import_browser_favorites_from_source(
    store: &BookmarkStore,
    source: &BookmarkImportSource,
    mode: ImportMode,
) -> Result<BrowserImportReport, String> {
    if let Some(error) = &source.read_error {
        return Err(format!("source illisible: {error}"));
    }

    let content = std::fs::read_to_string(&source.path)
        .map_err(|error| format!("lecture favoris impossible: {error}"))?;
    let tree = parse_chromium_bookmark_tree(&content)?;
    let parsed = count_imported_urls(&tree);
    let summary = match mode {
        ImportMode::Merge => store.merge_import(
            &tree,
            &format!("{} {}", source.browser_name, source.profile_name),
        )?,
        ImportMode::Replace => store.replace_with_import(
            &tree,
            &format!("{} {}", source.browser_name, source.profile_name),
        )?,
    };

    Ok(BrowserImportReport {
        source_label: format!("{} {}", source.browser_name, source.profile_name),
        mode,
        parsed,
        added: summary.imported,
        folders: summary.folders,
        skipped: 0,
        invalid: 0,
        backup_path: summary.backup_path,
    })
}

fn known_chromium_bookmark_files() -> Vec<BookmarkSource> {
    let mut sources = Vec::new();
    if let Some(local_app_data) = std::env::var_os("LOCALAPPDATA").map(PathBuf::from) {
        let roots = [
            (
                "Chrome",
                local_app_data
                    .join("Google")
                    .join("Chrome")
                    .join("User Data"),
            ),
            (
                "Edge",
                local_app_data
                    .join("Microsoft")
                    .join("Edge")
                    .join("User Data"),
            ),
            (
                "Brave",
                local_app_data
                    .join("BraveSoftware")
                    .join("Brave-Browser")
                    .join("User Data"),
            ),
            (
                "Chromium",
                local_app_data.join("Chromium").join("User Data"),
            ),
            ("Vivaldi", local_app_data.join("Vivaldi").join("User Data")),
        ];

        for (browser_name, root) in roots {
            sources.extend(chromium_profile_sources(browser_name, &root));
        }
    }
    if let Some(app_data) = std::env::var_os("APPDATA").map(PathBuf::from) {
        add_direct_bookmark_source(
            &mut sources,
            "Opera",
            "Stable",
            app_data
                .join("Opera Software")
                .join("Opera Stable")
                .join("Bookmarks"),
        );
        add_direct_bookmark_source(
            &mut sources,
            "Opera GX",
            "Stable",
            app_data
                .join("Opera Software")
                .join("Opera GX Stable")
                .join("Bookmarks"),
        );
    }
    sources.sort_by(|left, right| {
        left.browser_name
            .cmp(&right.browser_name)
            .then_with(|| left.profile_name.cmp(&right.profile_name))
    });
    sources
}

fn add_direct_bookmark_source(
    sources: &mut Vec<BookmarkSource>,
    browser_name: &str,
    profile_name: &str,
    path: PathBuf,
) {
    if path.is_file() {
        sources.push(BookmarkSource {
            browser_name: browser_name.to_string(),
            profile_name: profile_name.to_string(),
            path,
        });
    }
}

fn chromium_profile_sources(browser_name: &str, root: &Path) -> Vec<BookmarkSource> {
    let mut sources = Vec::new();
    if !root.is_dir() {
        return sources;
    }

    if let Ok(entries) = std::fs::read_dir(root) {
        for entry in entries.flatten() {
            let path = entry.path();
            if !path.is_dir() {
                continue;
            }

            let Some(profile_name) = path.file_name().and_then(|name| name.to_str()) else {
                continue;
            };
            if profile_name != "Default" && !profile_name.starts_with("Profile ") {
                continue;
            }

            let bookmarks = path.join("Bookmarks");
            if bookmarks.is_file() {
                sources.push(BookmarkSource {
                    browser_name: browser_name.to_string(),
                    profile_name: profile_name.to_string(),
                    path: bookmarks,
                });
            }
        }
    }

    sources.sort_by(|left, right| left.profile_name.cmp(&right.profile_name));
    sources
}

#[derive(Clone, Debug, PartialEq, Eq)]
struct BookmarkSource {
    browser_name: String,
    profile_name: String,
    path: PathBuf,
}

#[allow(dead_code)]
fn parse_chromium_bookmarks(content: &str) -> Result<Vec<ImportedFavorite>, String> {
    let value = JsonParser::new(content).parse()?;
    let mut favorites = Vec::new();
    collect_url_bookmarks(&value, &mut favorites);
    favorites.sort_by(|left, right| {
        left.url
            .cmp(&right.url)
            .then_with(|| left.title.cmp(&right.title))
    });
    favorites.dedup_by(|left, right| left.url == right.url);
    Ok(favorites)
}

fn parse_chromium_bookmark_tree(content: &str) -> Result<ImportedBookmarkTree, String> {
    let value = JsonParser::new(content).parse()?;
    let roots = object_field(&value, "roots")
        .ok_or_else(|| "fichier Bookmarks sans racines lisibles".to_string())?;
    let toolbar = object_field(roots, "bookmark_bar")
        .map(import_items_from_root)
        .transpose()?
        .unwrap_or_default();
    let mut other = object_field(roots, "other")
        .map(import_items_from_root)
        .transpose()?
        .unwrap_or_default();
    if let Some(synced) = object_field(roots, "synced")
        .map(import_items_from_root)
        .transpose()?
    {
        if !synced.is_empty() {
            other.push(ImportedBookmarkItem::Folder {
                title: "Favoris mobiles".to_string(),
                children: synced,
            });
        }
    }

    Ok(ImportedBookmarkTree { toolbar, other })
}

fn object_field<'a>(value: &'a JsonValue, field: &str) -> Option<&'a JsonValue> {
    match value {
        JsonValue::Object(object) => object.get(field),
        _ => None,
    }
}

fn import_items_from_root(root: &JsonValue) -> Result<Vec<ImportedBookmarkItem>, String> {
    let Some(JsonValue::Array(children)) = object_field(root, "children") else {
        return Ok(Vec::new());
    };

    children.iter().filter_map(import_item).collect()
}

fn import_item(value: &JsonValue) -> Option<Result<ImportedBookmarkItem, String>> {
    let JsonValue::Object(object) = value else {
        return None;
    };
    match json_string(object.get("type")) {
        Some("url") => {
            let url = json_string(object.get("url"))?;
            if !browser_data::is_web_url(url) {
                return None;
            }
            Some(Ok(ImportedBookmarkItem::Url {
                title: json_string(object.get("name"))
                    .unwrap_or(url)
                    .trim()
                    .to_string(),
                url: url.to_string(),
            }))
        }
        Some("folder") => {
            let title = json_string(object.get("name")).unwrap_or("Dossier").trim();
            let children = match object.get("children") {
                Some(JsonValue::Array(children)) => children
                    .iter()
                    .filter_map(import_item)
                    .collect::<Result<Vec<_>, _>>(),
                _ => Ok(Vec::new()),
            };
            Some(children.map(|children| ImportedBookmarkItem::Folder {
                title: if title.is_empty() {
                    "Dossier".to_string()
                } else {
                    title.to_string()
                },
                children,
            }))
        }
        _ => None,
    }
}

fn count_imported_urls(tree: &ImportedBookmarkTree) -> usize {
    fn count_items(items: &[ImportedBookmarkItem]) -> usize {
        items
            .iter()
            .map(|item| match item {
                ImportedBookmarkItem::Folder { children, .. } => count_items(children),
                ImportedBookmarkItem::Url { .. } => 1,
            })
            .sum()
    }

    count_items(&tree.toolbar) + count_items(&tree.other)
}

#[allow(dead_code)]
fn collect_url_bookmarks(value: &JsonValue, favorites: &mut Vec<ImportedFavorite>) {
    match value {
        JsonValue::Array(items) => {
            for item in items {
                collect_url_bookmarks(item, favorites);
            }
        }
        JsonValue::Object(object) => {
            let kind = json_string(object.get("type"));
            let url = json_string(object.get("url"));
            if kind == Some("url") {
                if let Some(url) = url {
                    if browser_data::is_web_url(url) {
                        favorites.push(ImportedFavorite {
                            title: json_string(object.get("name"))
                                .unwrap_or(url)
                                .trim()
                                .to_string(),
                            url: url.to_string(),
                        });
                    }
                }
            }

            for value in object.values() {
                collect_url_bookmarks(value, favorites);
            }
        }
        _ => {}
    }
}

fn json_string(value: Option<&JsonValue>) -> Option<&str> {
    match value {
        Some(JsonValue::String(value)) => Some(value.as_str()),
        _ => None,
    }
}

struct JsonParser<'a> {
    input: &'a [u8],
    index: usize,
}

impl<'a> JsonParser<'a> {
    fn new(input: &'a str) -> Self {
        Self {
            input: input.as_bytes(),
            index: 0,
        }
    }

    fn parse(mut self) -> Result<JsonValue, String> {
        let value = self.parse_value()?;
        self.skip_whitespace();
        if self.index != self.input.len() {
            return Err("contenu JSON inattendu apres les favoris".to_string());
        }
        Ok(value)
    }

    fn parse_value(&mut self) -> Result<JsonValue, String> {
        self.skip_whitespace();
        match self.peek() {
            Some(b'{') => self.parse_object(),
            Some(b'[') => self.parse_array(),
            Some(b'"') => self.parse_string().map(JsonValue::String),
            Some(b't') => self.parse_literal(b"true", JsonValue::Bool),
            Some(b'f') => self.parse_literal(b"false", JsonValue::Bool),
            Some(b'n') => self.parse_literal(b"null", JsonValue::Null),
            Some(b'-' | b'0'..=b'9') => self.parse_number(),
            _ => Err("valeur JSON invalide".to_string()),
        }
    }

    fn parse_object(&mut self) -> Result<JsonValue, String> {
        self.expect(b'{')?;
        let mut object = BTreeMap::new();
        loop {
            self.skip_whitespace();
            if self.consume_if(b'}') {
                break;
            }

            let key = self.parse_string()?;
            self.skip_whitespace();
            self.expect(b':')?;
            let value = self.parse_value()?;
            object.insert(key, value);
            self.skip_whitespace();
            if self.consume_if(b'}') {
                break;
            }
            self.expect(b',')?;
        }
        Ok(JsonValue::Object(object))
    }

    fn parse_array(&mut self) -> Result<JsonValue, String> {
        self.expect(b'[')?;
        let mut array = Vec::new();
        loop {
            self.skip_whitespace();
            if self.consume_if(b']') {
                break;
            }
            array.push(self.parse_value()?);
            self.skip_whitespace();
            if self.consume_if(b']') {
                break;
            }
            self.expect(b',')?;
        }
        Ok(JsonValue::Array(array))
    }

    fn parse_string(&mut self) -> Result<String, String> {
        self.expect(b'"')?;
        let mut output = String::new();
        let mut raw = Vec::new();
        while let Some(byte) = self.next() {
            match byte {
                b'"' => {
                    push_raw_utf8(&mut output, &mut raw)?;
                    return Ok(output);
                }
                b'\\' => {
                    push_raw_utf8(&mut output, &mut raw)?;
                    output.push(self.parse_escape()?);
                }
                0x00..=0x1F => return Err("chaine JSON invalide".to_string()),
                byte => raw.push(byte),
            }
        }
        Err("chaine JSON non terminee".to_string())
    }

    fn parse_escape(&mut self) -> Result<char, String> {
        match self.next() {
            Some(b'"') => Ok('"'),
            Some(b'\\') => Ok('\\'),
            Some(b'/') => Ok('/'),
            Some(b'b') => Ok('\u{0008}'),
            Some(b'f') => Ok('\u{000C}'),
            Some(b'n') => Ok('\n'),
            Some(b'r') => Ok('\r'),
            Some(b't') => Ok('\t'),
            Some(b'u') => self.parse_unicode_escape(),
            _ => Err("echappement JSON invalide".to_string()),
        }
    }

    fn parse_unicode_escape(&mut self) -> Result<char, String> {
        let mut value = 0_u32;
        for _ in 0..4 {
            let Some(byte) = self.next() else {
                return Err("echappement unicode incomplet".to_string());
            };
            value = value * 16 + hex_value(byte).ok_or("echappement unicode invalide")? as u32;
        }
        char::from_u32(value).ok_or_else(|| "code unicode invalide".to_string())
    }

    fn parse_number(&mut self) -> Result<JsonValue, String> {
        let start = self.index;
        if self.peek() == Some(b'-') {
            self.index += 1;
        }
        self.consume_digits();
        if self.peek() == Some(b'.') {
            self.index += 1;
            self.consume_digits();
        }
        if matches!(self.peek(), Some(b'e' | b'E')) {
            self.index += 1;
            if matches!(self.peek(), Some(b'+' | b'-')) {
                self.index += 1;
            }
            self.consume_digits();
        }
        if self.index == start {
            return Err("nombre JSON invalide".to_string());
        }
        Ok(JsonValue::Number)
    }

    fn parse_literal(&mut self, literal: &[u8], value: JsonValue) -> Result<JsonValue, String> {
        if self.input.get(self.index..self.index + literal.len()) == Some(literal) {
            self.index += literal.len();
            Ok(value)
        } else {
            Err("litteral JSON invalide".to_string())
        }
    }

    fn consume_digits(&mut self) {
        while matches!(self.peek(), Some(b'0'..=b'9')) {
            self.index += 1;
        }
    }

    fn skip_whitespace(&mut self) {
        while matches!(self.peek(), Some(b' ' | b'\n' | b'\r' | b'\t')) {
            self.index += 1;
        }
    }

    fn expect(&mut self, expected: u8) -> Result<(), String> {
        if self.consume_if(expected) {
            Ok(())
        } else {
            Err("structure JSON invalide".to_string())
        }
    }

    fn consume_if(&mut self, expected: u8) -> bool {
        if self.peek() == Some(expected) {
            self.index += 1;
            true
        } else {
            false
        }
    }

    fn peek(&self) -> Option<u8> {
        self.input.get(self.index).copied()
    }

    fn next(&mut self) -> Option<u8> {
        let byte = self.peek()?;
        self.index += 1;
        Some(byte)
    }
}

fn hex_value(byte: u8) -> Option<u8> {
    match byte {
        b'0'..=b'9' => Some(byte - b'0'),
        b'a'..=b'f' => Some(byte - b'a' + 10),
        b'A'..=b'F' => Some(byte - b'A' + 10),
        _ => None,
    }
}

fn push_raw_utf8(output: &mut String, raw: &mut Vec<u8>) -> Result<(), String> {
    if raw.is_empty() {
        return Ok(());
    }

    let text = std::str::from_utf8(raw).map_err(|_| "chaine JSON non UTF-8".to_string())?;
    output.push_str(text);
    raw.clear();
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::{
        BookmarkImportSource, count_imported_urls, parse_chromium_bookmark_tree,
        parse_chromium_bookmarks,
    };

    #[test]
    fn parses_chromium_bookmark_urls() {
        let content = r#"{
            "roots": {
                "bookmark_bar": {
                    "children": [
                        {"type": "url", "name": "Rust", "url": "https://www.rust-lang.org/"},
                        {"type": "folder", "name": "Folder", "children": [
                            {"type": "url", "name": "Example", "url": "https://example.com"}
                        ]},
                        {"type": "url", "name": "Internal", "url": "chrome://settings"}
                    ]
                }
            }
        }"#;

        let favorites = parse_chromium_bookmarks(content).unwrap();

        assert_eq!(favorites.len(), 2);
        assert!(
            favorites
                .iter()
                .any(|favorite| favorite.url == "https://example.com")
        );
        assert!(favorites.iter().any(|favorite| favorite.title == "Rust"));
    }

    #[test]
    fn rejects_invalid_json() {
        assert!(parse_chromium_bookmarks("{").is_err());
    }

    #[test]
    fn parses_chromium_bookmark_tree_with_folders() {
        let content = r#"{
            "roots": {
                "bookmark_bar": {
                    "children": [
                        {"type": "folder", "name": "Dev", "children": [
                            {"type": "url", "name": "Rust", "url": "https://www.rust-lang.org/"}
                        ]},
                        {"type": "url", "name": "Example", "url": "https://example.com"}
                    ]
                },
                "other": {"children": []}
            }
        }"#;

        let tree = parse_chromium_bookmark_tree(content).unwrap();

        assert_eq!(tree.toolbar.len(), 2);
        assert_eq!(count_imported_urls(&tree), 2);
    }

    #[test]
    fn labels_supported_source() {
        let supported = BookmarkImportSource {
            browser_name: "Vivaldi".to_string(),
            profile_name: "Default".to_string(),
            path: "Bookmarks".into(),
            favorite_count: 42,
            read_error: None,
        };

        assert_eq!(supported.label(), "Vivaldi Default - 42 favoris");
    }
}
