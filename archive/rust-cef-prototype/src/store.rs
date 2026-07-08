use crate::{bookmarks, browser_data, profile};

pub fn open_bookmarks() -> Result<bookmarks::BookmarkStore, String> {
    let p = profile::PulseProfile::default()?;
    let store = bookmarks::BookmarkStore::new(p.bookmarks_file, p.favorites_file);
    store.ensure_file()?;
    Ok(store)
}

pub fn open_browser_data() -> Result<browser_data::BrowserDataStore, String> {
    let p = profile::PulseProfile::default()?;
    let store = browser_data::BrowserDataStore::new(p.history_file, p.favorites_file);
    store.ensure_files()?;
    Ok(store)
}
