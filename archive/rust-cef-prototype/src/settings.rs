use std::path::Path;

use crate::tabs::TabLayout;

#[derive(Debug, Clone, PartialEq)]
pub enum SearchEngine {
    Google,
    DuckDuckGo,
    Brave,
    Bing,
    Qwant,
}

impl SearchEngine {
    pub fn from_key(s: &str) -> Self {
        match s {
            "duckduckgo" => Self::DuckDuckGo,
            "brave" => Self::Brave,
            "bing" => Self::Bing,
            "qwant" => Self::Qwant,
            _ => Self::Google,
        }
    }

    pub fn key(&self) -> &'static str {
        match self {
            Self::Google => "google",
            Self::DuckDuckGo => "duckduckgo",
            Self::Brave => "brave",
            Self::Bing => "bing",
            Self::Qwant => "qwant",
        }
    }

    pub fn label(&self) -> &'static str {
        match self {
            Self::Google => "Google",
            Self::DuckDuckGo => "DuckDuckGo",
            Self::Brave => "Brave Search",
            Self::Bing => "Bing",
            Self::Qwant => "Qwant",
        }
    }

    pub fn search_url(&self, query: &str) -> String {
        let q = url_encode_query(query);
        match self {
            Self::Google => format!("https://www.google.com/search?q={q}"),
            Self::DuckDuckGo => format!("https://duckduckgo.com/?q={q}"),
            Self::Brave => format!("https://search.brave.com/search?q={q}"),
            Self::Bing => format!("https://www.bing.com/search?q={q}"),
            Self::Qwant => format!("https://www.qwant.com/?q={q}"),
        }
    }

    pub fn all() -> &'static [SearchEngine] {
        &[
            SearchEngine::Google,
            SearchEngine::DuckDuckGo,
            SearchEngine::Brave,
            SearchEngine::Bing,
            SearchEngine::Qwant,
        ]
    }
}

#[derive(Debug, Clone, PartialEq)]
pub enum StartupBehavior {
    Home,
    Custom(String),
}

impl StartupBehavior {
    fn from_json_val(v: &str) -> Self {
        if v == "home" || v.is_empty() {
            Self::Home
        } else {
            Self::Custom(v.to_string())
        }
    }

    fn to_json_val(&self) -> String {
        match self {
            Self::Home => "home".to_string(),
            Self::Custom(url) => url.clone(),
        }
    }

    pub fn url(&self) -> &str {
        match self {
            Self::Home => crate::local_pages::HOME_ADDRESS,
            Self::Custom(url) => url.as_str(),
        }
    }

    pub fn custom_url(&self) -> &str {
        match self {
            Self::Custom(url) => url.as_str(),
            _ => "",
        }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub enum TabBarMode {
    Horizontal,
    Vertical,
}

impl TabBarMode {
    pub fn from_key(s: &str) -> Self {
        match s {
            "vertical" => Self::Vertical,
            _ => Self::Horizontal,
        }
    }

    pub fn key(&self) -> &'static str {
        match self {
            Self::Horizontal => "horizontal",
            Self::Vertical => "vertical",
        }
    }

    pub fn label(&self) -> &'static str {
        match self {
            Self::Horizontal => "Horizontaux (en haut)",
            Self::Vertical => "Verticaux (panneau gauche)",
        }
    }

    pub fn to_tab_layout(&self) -> TabLayout {
        match self {
            Self::Horizontal => TabLayout::Horizontal,
            Self::Vertical => TabLayout::Vertical,
        }
    }
}

#[derive(Debug, Clone)]
pub struct PulseSettings {
    pub search_engine: SearchEngine,
    pub startup: StartupBehavior,
    pub tab_bar: TabBarMode,
}

impl PulseSettings {
    fn default() -> Self {
        Self {
            search_engine: SearchEngine::Google,
            startup: StartupBehavior::Home,
            tab_bar: TabBarMode::Horizontal,
        }
    }

    pub fn load(path: &Path) -> Self {
        let mut s = Self::default();
        let Ok(content) = std::fs::read_to_string(path) else {
            return s;
        };
        if let Some(engine) = extract_json_str(&content, "search_engine") {
            s.search_engine = SearchEngine::from_key(&engine);
        }
        if let Some(startup) = extract_json_str(&content, "startup") {
            s.startup = StartupBehavior::from_json_val(&startup);
        }
        if let Some(tab_bar) = extract_json_str(&content, "tab_bar") {
            s.tab_bar = TabBarMode::from_key(&tab_bar);
        }
        s
    }

    pub fn save(&self, path: &Path) -> Result<(), String> {
        if let Some(parent) = path.parent() {
            std::fs::create_dir_all(parent).map_err(|e| format!("Dossier parametres: {e}"))?;
        }
        let json = format!(
            r#"{{"search_engine":"{}","startup":"{}","tab_bar":"{}"}}"#,
            self.search_engine.key(),
            json_escape_str(&self.startup.to_json_val()),
            self.tab_bar.key(),
        );
        std::fs::write(path, json).map_err(|e| format!("Sauvegarde parametres: {e}"))
    }

    pub fn apply_key(&mut self, key: &str, value: &str) -> bool {
        match key {
            "search_engine" => {
                self.search_engine = SearchEngine::from_key(value);
                true
            }
            "startup" => {
                self.startup = StartupBehavior::from_json_val(value);
                true
            }
            "tab_bar" => {
                self.tab_bar = TabBarMode::from_key(value);
                true
            }
            _ => false,
        }
    }

    pub fn search_url(&self, query: &str) -> String {
        self.search_engine.search_url(query)
    }

    pub fn startup_url(&self) -> String {
        self.startup.url().to_string()
    }
}

fn extract_json_str(json: &str, key: &str) -> Option<String> {
    let needle = format!("\"{}\":\"", key);
    let start = json.find(&needle)? + needle.len();
    let rest = &json[start..];
    let mut result = String::new();
    let mut chars = rest.chars();
    loop {
        match chars.next()? {
            '"' => break,
            '\\' => match chars.next()? {
                '"' => result.push('"'),
                '\\' => result.push('\\'),
                'n' => result.push('\n'),
                c => result.push(c),
            },
            c => result.push(c),
        }
    }
    Some(result)
}

fn json_escape_str(s: &str) -> String {
    let mut out = String::new();
    for c in s.chars() {
        match c {
            '"' => out.push_str("\\\""),
            '\\' => out.push_str("\\\\"),
            '\n' => out.push_str("\\n"),
            c => out.push(c),
        }
    }
    out
}

fn url_encode_query(s: &str) -> String {
    let mut out = String::new();
    for byte in s.as_bytes() {
        match *byte {
            b'A'..=b'Z' | b'a'..=b'z' | b'0'..=b'9' | b'-' | b'_' | b'.' | b'~' => {
                out.push(*byte as char);
            }
            b' ' => out.push('+'),
            b => out.push_str(&format!("%{b:02X}")),
        }
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn default_is_google() {
        let s = PulseSettings::default();
        assert_eq!(s.search_engine, SearchEngine::Google);
    }

    #[test]
    fn search_url_google() {
        let s = PulseSettings::default();
        let url = s.search_url("pulse browser");
        assert!(url.contains("google.com") && url.contains("pulse+browser"));
    }

    #[test]
    fn apply_key_changes_engine() {
        let mut s = PulseSettings::default();
        assert!(s.apply_key("search_engine", "duckduckgo"));
        assert_eq!(s.search_engine, SearchEngine::DuckDuckGo);
    }

    #[test]
    fn load_from_json_string() {
        let json = r#"{"search_engine":"brave","startup":"home","tab_bar":"horizontal"}"#;
        let mut s = PulseSettings::default();
        if let Some(engine) = extract_json_str(json, "search_engine") {
            s.search_engine = SearchEngine::from_key(&engine);
        }
        assert_eq!(s.search_engine, SearchEngine::Brave);
    }

    #[test]
    fn apply_key_tab_bar_vertical() {
        let mut s = PulseSettings::default();
        assert!(s.apply_key("tab_bar", "vertical"));
        assert_eq!(s.tab_bar, TabBarMode::Vertical);
    }

    #[test]
    fn tab_bar_roundtrip() {
        assert_eq!(TabBarMode::from_key("vertical"), TabBarMode::Vertical);
        assert_eq!(TabBarMode::from_key("horizontal"), TabBarMode::Horizontal);
        assert_eq!(TabBarMode::Vertical.key(), "vertical");
        assert_eq!(TabBarMode::Horizontal.key(), "horizontal");
    }
}
