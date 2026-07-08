use std::path::PathBuf;

const APP_DATA_DIR: &str = "PulseBrowser";
const DEFAULT_PROFILE_ID: &str = "default";

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct PulseProfile {
    pub id: String,
    pub base_dir: PathBuf,
    pub root_cache_dir: PathBuf,
    pub cef_cache_dir: PathBuf,
    pub navigation_dir: PathBuf,
    pub history_file: PathBuf,
    pub favorites_file: PathBuf,
    pub bookmarks_file: PathBuf,
    pub vault_dir: PathBuf,
    pub vault_file: PathBuf,
    pub settings_file: PathBuf,
}

impl PulseProfile {
    pub fn default() -> Result<Self, String> {
        let base_dir = local_app_data_dir().join(APP_DATA_DIR);
        let profiles_dir = base_dir.join("profiles");
        let profile_dir = profiles_dir.join(DEFAULT_PROFILE_ID);

        let profile = Self {
            id: DEFAULT_PROFILE_ID.to_string(),
            root_cache_dir: base_dir.clone(),
            cef_cache_dir: profile_dir.join("cef-profile"),
            navigation_dir: profile_dir.join("navigation"),
            history_file: profile_dir.join("navigation").join("history.tsv"),
            favorites_file: profile_dir.join("navigation").join("favorites.tsv"),
            bookmarks_file: profile_dir.join("navigation").join("bookmarks.tsv"),
            vault_dir: profile_dir.join("vault"),
            vault_file: profile_dir.join("vault").join("default.pbvault"),
            settings_file: profile_dir.join("settings.json"),
            base_dir,
        };

        profile.ensure_directories()?;
        Ok(profile)
    }

    fn ensure_directories(&self) -> Result<(), String> {
        for directory in [
            &self.base_dir,
            &self.root_cache_dir,
            &self.cef_cache_dir,
            &self.navigation_dir,
            &self.vault_dir,
        ] {
            std::fs::create_dir_all(directory)
                .map_err(|error| format!("profil local: creation impossible: {error}"))?;
        }

        Ok(())
    }
}

fn local_app_data_dir() -> PathBuf {
    std::env::var_os("LOCALAPPDATA")
        .map(PathBuf::from)
        .unwrap_or_else(std::env::temp_dir)
}

pub fn path_text(path: &PathBuf) -> String {
    path.to_string_lossy().replace('\\', "/")
}

#[cfg(test)]
mod tests {
    use super::{PulseProfile, path_text};
    use std::path::PathBuf;

    #[test]
    fn path_text_uses_forward_slashes_for_cef() {
        assert_eq!(
            path_text(&PathBuf::from(r"C:\PulseBrowser\profile")),
            "C:/PulseBrowser/profile"
        );
    }

    #[test]
    fn cef_cache_path_is_inside_root_cache_path() {
        let profile = PulseProfile::default().expect("default profile");

        assert!(
            profile.cef_cache_dir.starts_with(&profile.root_cache_dir),
            "CEF requires cache_path to be under root_cache_path"
        );
    }
}
