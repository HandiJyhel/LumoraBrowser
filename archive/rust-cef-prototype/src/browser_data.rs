use std::collections::BTreeMap;
use std::fs::OpenOptions;
use std::io::Write;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

#[derive(Clone, Debug)]
pub struct BrowserDataStore {
    history_file: PathBuf,
    favorites_file: PathBuf,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct FavoriteEntry {
    pub url: String,
    pub title: String,
    pub saved_at_epoch_seconds: u64,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct HistoryEntry {
    pub url: String,
    pub title: String,
    pub visited_at_epoch_seconds: u64,
}

#[derive(Clone, Debug, Default, PartialEq, Eq)]
#[allow(dead_code)]
pub struct FavoriteImportSummary {
    pub added: usize,
    pub skipped: usize,
    pub invalid: usize,
}

#[derive(Clone, Debug, Default, PartialEq, Eq)]
#[allow(dead_code)]
pub struct FavoriteClearSummary {
    pub removed: usize,
    pub backup_path: Option<PathBuf>,
}

impl BrowserDataStore {
    pub fn new(history_file: PathBuf, favorites_file: PathBuf) -> Self {
        Self {
            history_file,
            favorites_file,
        }
    }

    pub fn ensure_files(&self) -> Result<(), String> {
        ensure_parent(&self.history_file)?;
        ensure_parent(&self.favorites_file)?;
        OpenOptions::new()
            .create(true)
            .append(true)
            .open(&self.history_file)
            .map_err(|error| format!("donnees navigateur: historique inaccessible: {error}"))?;
        OpenOptions::new()
            .create(true)
            .append(true)
            .open(&self.favorites_file)
            .map_err(|error| format!("donnees navigateur: favoris inaccessible: {error}"))?;
        Ok(())
    }

    pub fn record_history_visit(&self, url: &str, title: &str) -> Result<(), String> {
        if !is_web_url(url) {
            return Ok(());
        }

        let mut file = OpenOptions::new()
            .create(true)
            .append(true)
            .open(&self.history_file)
            .map_err(|error| format!("donnees navigateur: historique inaccessible: {error}"))?;
        writeln!(
            file,
            "{}\t{}\t{}",
            unix_now(),
            encode_field(url),
            encode_field(&clean_title(title))
        )
        .map_err(|error| format!("donnees navigateur: ecriture historique impossible: {error}"))
    }

    #[allow(dead_code)]
    pub fn toggle_favorite(&self, url: &str, title: &str) -> Result<bool, String> {
        if !is_web_url(url) {
            return Err("seules les pages web peuvent etre ajoutees aux favoris.".to_string());
        }

        let mut favorites = self.read_favorites()?;
        if favorites.remove(url).is_some() {
            self.write_favorites(&favorites)?;
            return Ok(false);
        }

        favorites.insert(
            url.to_string(),
            FavoriteEntry {
                url: url.to_string(),
                title: clean_title(title),
                saved_at_epoch_seconds: unix_now(),
            },
        );
        self.write_favorites(&favorites)?;
        Ok(true)
    }

    #[allow(dead_code)]
    pub fn import_favorites<I>(&self, entries: I) -> Result<FavoriteImportSummary, String>
    where
        I: IntoIterator<Item = (String, String)>,
    {
        let mut favorites = self.read_favorites()?;
        let mut summary = FavoriteImportSummary::default();
        let now = unix_now();

        for (url, title) in entries {
            if !is_web_url(&url) {
                summary.invalid += 1;
                continue;
            }

            if favorites.contains_key(&url) {
                summary.skipped += 1;
                continue;
            }

            favorites.insert(
                url.clone(),
                FavoriteEntry {
                    url,
                    title: clean_title(&title),
                    saved_at_epoch_seconds: now + summary.added as u64,
                },
            );
            summary.added += 1;
        }

        if summary.added > 0 {
            self.write_favorites(&favorites)?;
        }

        Ok(summary)
    }

    #[allow(dead_code)]
    pub fn replace_favorites<I>(&self, entries: I) -> Result<FavoriteImportSummary, String>
    where
        I: IntoIterator<Item = (String, String)>,
    {
        let mut favorites = BTreeMap::new();
        let mut summary = FavoriteImportSummary::default();
        let now = unix_now();

        for (url, title) in entries {
            if !is_web_url(&url) {
                summary.invalid += 1;
                continue;
            }

            if favorites.contains_key(&url) {
                summary.skipped += 1;
                continue;
            }

            favorites.insert(
                url.clone(),
                FavoriteEntry {
                    url,
                    title: clean_title(&title),
                    saved_at_epoch_seconds: now + summary.added as u64,
                },
            );
            summary.added += 1;
        }

        self.write_favorites(&favorites)?;
        Ok(summary)
    }

    #[allow(dead_code)]
    pub fn backup_favorites(&self, label: &str) -> Result<Option<PathBuf>, String> {
        if !self.favorites_file.exists() {
            return Ok(None);
        }

        let parent = self
            .favorites_file
            .parent()
            .ok_or_else(|| "donnees navigateur: chemin favoris invalide".to_string())?;
        let backup_name = format!("favorites.{}.{}.bak.tsv", unix_now(), sanitize_label(label));
        let backup_path = parent.join(backup_name);
        std::fs::copy(&self.favorites_file, &backup_path).map_err(|error| {
            format!("donnees navigateur: sauvegarde favoris impossible: {error}")
        })?;
        Ok(Some(backup_path))
    }

    #[allow(dead_code)]
    pub fn remove_favorite(&self, url: &str) -> Result<bool, String> {
        let mut favorites = self.read_favorites()?;
        let removed = favorites.remove(url).is_some();
        if removed {
            self.write_favorites(&favorites)?;
        }
        Ok(removed)
    }

    #[allow(dead_code)]
    pub fn clear_favorites_with_backup(&self) -> Result<FavoriteClearSummary, String> {
        let removed = self.read_favorites()?.len();
        let backup_path = self.backup_favorites("clear-favorites")?;
        self.write_favorites(&BTreeMap::new())?;
        Ok(FavoriteClearSummary {
            removed,
            backup_path,
        })
    }

    pub fn recent_history(&self, limit: usize) -> Result<Vec<HistoryEntry>, String> {
        if !self.history_file.exists() {
            return Ok(Vec::new());
        }

        let content = std::fs::read_to_string(&self.history_file).map_err(|error| {
            format!("donnees navigateur: lecture historique impossible: {error}")
        })?;
        let mut history: Vec<HistoryEntry> = content
            .lines()
            .filter_map(parse_history_line)
            .filter(|entry| is_web_url(&entry.url))
            .collect();
        history.sort_by_key(|entry| std::cmp::Reverse(entry.visited_at_epoch_seconds));
        history.truncate(limit);
        Ok(history)
    }

    pub fn clear_history(&self) -> Result<usize, String> {
        if !self.history_file.exists() {
            return Ok(0);
        }
        let content = std::fs::read_to_string(&self.history_file)
            .map_err(|e| format!("historique: lecture impossible: {e}"))?;
        let count = content.lines().filter(|l| !l.trim().is_empty()).count();
        std::fs::write(&self.history_file, "")
            .map_err(|e| format!("historique: vidage impossible: {e}"))?;
        Ok(count)
    }

    #[allow(dead_code)]
    pub fn favorites(&self) -> Result<Vec<FavoriteEntry>, String> {
        let mut favorites: Vec<FavoriteEntry> = self.read_favorites()?.into_values().collect();
        favorites.sort_by_key(|entry| std::cmp::Reverse(entry.saved_at_epoch_seconds));
        Ok(favorites)
    }

    fn read_favorites(&self) -> Result<BTreeMap<String, FavoriteEntry>, String> {
        if !self.favorites_file.exists() {
            return Ok(BTreeMap::new());
        }

        let content = std::fs::read_to_string(&self.favorites_file)
            .map_err(|error| format!("donnees navigateur: lecture favoris impossible: {error}"))?;
        let mut favorites = BTreeMap::new();
        for line in content.lines() {
            if let Some(entry) = parse_favorite_line(line) {
                favorites.insert(entry.url.clone(), entry);
            }
        }
        Ok(favorites)
    }

    fn write_favorites(&self, favorites: &BTreeMap<String, FavoriteEntry>) -> Result<(), String> {
        ensure_parent(&self.favorites_file)?;
        let mut content = String::new();
        for favorite in favorites.values() {
            content.push_str(&format!(
                "{}\t{}\t{}\n",
                favorite.saved_at_epoch_seconds,
                encode_field(&favorite.url),
                encode_field(&favorite.title)
            ));
        }

        std::fs::write(&self.favorites_file, content)
            .map_err(|error| format!("donnees navigateur: ecriture favoris impossible: {error}"))
    }
}

pub fn is_web_url(url: &str) -> bool {
    let lower = url.trim().to_ascii_lowercase();
    lower.starts_with("http://") || lower.starts_with("https://")
}

fn parse_history_line(line: &str) -> Option<HistoryEntry> {
    let mut parts = line.split('\t');
    let visited_at_epoch_seconds = parts.next()?.parse().ok()?;
    let url = decode_field(parts.next()?)?;
    let title = decode_field(parts.next()?)?;
    if !is_web_url(&url) {
        return None;
    }

    Some(HistoryEntry {
        url,
        title,
        visited_at_epoch_seconds,
    })
}

fn parse_favorite_line(line: &str) -> Option<FavoriteEntry> {
    let mut parts = line.split('\t');
    let saved_at_epoch_seconds = parts.next()?.parse().ok()?;
    let url = decode_field(parts.next()?)?;
    let title = decode_field(parts.next()?)?;
    if !is_web_url(&url) {
        return None;
    }

    Some(FavoriteEntry {
        url,
        title,
        saved_at_epoch_seconds,
    })
}

fn ensure_parent(path: &Path) -> Result<(), String> {
    if let Some(parent) = path.parent() {
        std::fs::create_dir_all(parent)
            .map_err(|error| format!("donnees navigateur: creation dossier impossible: {error}"))?;
    }
    Ok(())
}

fn clean_title(title: &str) -> String {
    let title = title.trim();
    if title.is_empty() {
        return "Page sans titre".to_string();
    }

    title.chars().take(140).collect()
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

#[cfg(test)]
mod tests {
    use super::{
        BrowserDataStore, decode_field, encode_field, is_web_url, parse_favorite_line,
        parse_history_line,
    };

    #[test]
    fn detects_web_urls_only() {
        assert!(is_web_url("https://example.com"));
        assert!(is_web_url("http://example.com"));
        assert!(!is_web_url("data:text/html,hello"));
        assert!(!is_web_url("pulse://home"));
    }

    #[test]
    fn field_encoding_round_trips_tabs_and_unicode() {
        let encoded = encode_field("https://example.com/a b\tc?x=1");

        assert_eq!(
            decode_field(&encoded),
            Some("https://example.com/a b\tc?x=1".to_string())
        );
    }

    #[test]
    fn parses_valid_favorite_line() {
        let line = format!(
            "10\t{}\t{}",
            encode_field("https://example.com"),
            encode_field("Example")
        );
        let entry = parse_favorite_line(&line).unwrap();

        assert_eq!(entry.url, "https://example.com");
        assert_eq!(entry.title, "Example");
        assert_eq!(entry.saved_at_epoch_seconds, 10);
    }

    #[test]
    fn parses_valid_history_line() {
        let line = format!(
            "20\t{}\t{}",
            encode_field("https://example.com/page"),
            encode_field("Example page")
        );
        let entry = parse_history_line(&line).unwrap();

        assert_eq!(entry.url, "https://example.com/page");
        assert_eq!(entry.title, "Example page");
        assert_eq!(entry.visited_at_epoch_seconds, 20);
    }

    #[test]
    fn import_skips_duplicates_and_invalid_urls() {
        let root =
            std::env::temp_dir().join(format!("pulse-browser-data-test-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&root);
        let store = BrowserDataStore::new(root.join("history.tsv"), root.join("favorites.tsv"));

        let first = store
            .import_favorites(vec![
                ("https://example.com".to_string(), "Example".to_string()),
                ("data:text/html,hello".to_string(), "Bad".to_string()),
            ])
            .unwrap();
        let second = store
            .import_favorites(vec![
                (
                    "https://example.com".to_string(),
                    "Example again".to_string(),
                ),
                ("https://www.rust-lang.org".to_string(), "Rust".to_string()),
            ])
            .unwrap();

        assert_eq!(first.added, 1);
        assert_eq!(first.invalid, 1);
        assert_eq!(second.added, 1);
        assert_eq!(second.skipped, 1);
        assert_eq!(store.favorites().unwrap().len(), 2);
        let _ = std::fs::remove_dir_all(&root);
    }

    #[test]
    fn replace_favorites_removes_old_entries() {
        let root = std::env::temp_dir().join(format!(
            "pulse-browser-data-replace-test-{}",
            std::process::id()
        ));
        let _ = std::fs::remove_dir_all(&root);
        let store = BrowserDataStore::new(root.join("history.tsv"), root.join("favorites.tsv"));

        store
            .import_favorites(vec![("https://old.example".to_string(), "Old".to_string())])
            .unwrap();
        let summary = store
            .replace_favorites(vec![("https://new.example".to_string(), "New".to_string())])
            .unwrap();
        let favorites = store.favorites().unwrap();

        assert_eq!(summary.added, 1);
        assert_eq!(favorites.len(), 1);
        assert_eq!(favorites[0].url, "https://new.example");
        let _ = std::fs::remove_dir_all(&root);
    }

    #[test]
    fn remove_and_clear_favorites_update_store() {
        let root = std::env::temp_dir().join(format!(
            "pulse-browser-data-remove-test-{}",
            std::process::id()
        ));
        let _ = std::fs::remove_dir_all(&root);
        let store = BrowserDataStore::new(root.join("history.tsv"), root.join("favorites.tsv"));

        store
            .import_favorites(vec![
                ("https://one.example".to_string(), "One".to_string()),
                ("https://two.example".to_string(), "Two".to_string()),
            ])
            .unwrap();

        assert!(store.remove_favorite("https://one.example").unwrap());
        assert!(!store.remove_favorite("https://missing.example").unwrap());
        assert_eq!(store.favorites().unwrap().len(), 1);

        let summary = store.clear_favorites_with_backup().unwrap();
        assert_eq!(summary.removed, 1);
        assert!(summary.backup_path.is_some());
        assert!(store.favorites().unwrap().is_empty());
        let _ = std::fs::remove_dir_all(&root);
    }
}

#[allow(dead_code)]
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
