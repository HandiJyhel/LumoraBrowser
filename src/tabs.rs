#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub struct TabId(pub u64);

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum TabLayout {
    Horizontal,
    Vertical,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct BrowserTab {
    pub id: TabId,
    pub title: String,
    pub url: String,
    pub is_loading: bool,
}

#[derive(Clone, Debug)]
pub struct TabManager {
    tabs: Vec<BrowserTab>,
    active_tab: Option<TabId>,
    layout: TabLayout,
}

impl TabManager {
    pub fn new() -> Self {
        Self {
            tabs: Vec::new(),
            active_tab: None,
            layout: TabLayout::Horizontal,
        }
    }

    pub fn open_tab(&mut self, url: impl Into<String>, title: impl Into<String>) -> TabId {
        let id = self.next_available_id();
        self.open_tab_with_id(id, url, title)
    }

    pub fn open_tab_with_id(
        &mut self,
        id: TabId,
        url: impl Into<String>,
        title: impl Into<String>,
    ) -> TabId {
        self.tabs.push(BrowserTab {
            id,
            title: title.into(),
            url: url.into(),
            is_loading: false,
        });
        self.active_tab = Some(id);
        id
    }

    pub fn close_tab(&mut self, id: TabId) -> bool {
        let Some(index) = self.tabs.iter().position(|tab| tab.id == id) else {
            return false;
        };

        self.tabs.remove(index);
        if self.active_tab == Some(id) {
            self.active_tab = self
                .tabs
                .get(index.saturating_sub(1))
                .or_else(|| self.tabs.first())
                .map(|tab| tab.id);
        }
        true
    }

    pub fn activate_tab(&mut self, id: TabId) -> bool {
        if self.tabs.iter().any(|tab| tab.id == id) {
            self.active_tab = Some(id);
            true
        } else {
            false
        }
    }

    pub fn update_tab(
        &mut self,
        id: TabId,
        url: impl Into<String>,
        title: impl Into<String>,
        is_loading: bool,
    ) -> bool {
        let Some(tab) = self.tabs.iter_mut().find(|tab| tab.id == id) else {
            return false;
        };
        tab.url = url.into();
        tab.title = title.into();
        tab.is_loading = is_loading;
        true
    }

    pub fn update_tab_title(&mut self, id: TabId, title: impl Into<String>) -> bool {
        let Some(tab) = self.tabs.iter_mut().find(|t| t.id == id) else {
            return false;
        };
        tab.title = title.into();
        true
    }

    pub fn update_tab_url(&mut self, id: TabId, url: impl Into<String>) -> bool {
        let Some(tab) = self.tabs.iter_mut().find(|t| t.id == id) else {
            return false;
        };
        tab.url = url.into();
        true
    }

    pub fn set_tab_loading(&mut self, id: TabId, loading: bool) -> bool {
        let Some(tab) = self.tabs.iter_mut().find(|t| t.id == id) else {
            return false;
        };
        tab.is_loading = loading;
        true
    }

    pub fn active_tab(&self) -> Option<&BrowserTab> {
        let active_tab = self.active_tab?;
        self.tabs.iter().find(|tab| tab.id == active_tab)
    }

    pub fn active_tab_id(&self) -> Option<TabId> {
        self.active_tab
    }

    pub fn tabs(&self) -> &[BrowserTab] {
        &self.tabs
    }

    pub fn layout(&self) -> TabLayout {
        self.layout
    }

    pub fn set_layout(&mut self, layout: TabLayout) {
        self.layout = layout;
    }

    fn next_available_id(&self) -> TabId {
        let max = self.tabs.iter().map(|t| t.id.0).max().unwrap_or(0);
        TabId(max + 1)
    }
}

impl Default for TabManager {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::{TabId, TabLayout, TabManager};

    #[test]
    fn opens_and_activates_tabs() {
        let mut tabs = TabManager::new();
        let first = tabs.open_tab("pulse://accueil", "Accueil");
        let second = tabs.open_tab("https://example.com", "Example");

        assert_eq!(tabs.tabs().len(), 2);
        assert_eq!(tabs.active_tab().map(|tab| tab.id), Some(second));

        assert!(tabs.activate_tab(first));
        assert_eq!(tabs.active_tab().map(|tab| tab.id), Some(first));
    }

    #[test]
    fn closes_active_tab_and_keeps_a_neighbor_active() {
        let mut tabs = TabManager::new();
        let first = tabs.open_tab("pulse://accueil", "Accueil");
        let second = tabs.open_tab("https://example.com", "Example");

        assert!(tabs.close_tab(second));

        assert_eq!(tabs.tabs().len(), 1);
        assert_eq!(tabs.active_tab().map(|tab| tab.id), Some(first));
    }

    #[test]
    fn stores_vertical_layout_preference() {
        let mut tabs = TabManager::new();

        tabs.set_layout(TabLayout::Vertical);

        assert_eq!(tabs.layout(), TabLayout::Vertical);
    }

    #[test]
    fn open_tab_with_id_uses_given_id() {
        let mut tabs = TabManager::new();
        let id = TabId(42);
        tabs.open_tab_with_id(id, "https://example.com", "Example");

        assert_eq!(tabs.active_tab().map(|t| t.id), Some(id));
    }

    #[test]
    fn update_tab_title_changes_title() {
        let mut tabs = TabManager::new();
        let id = tabs.open_tab("https://example.com", "Avant");
        tabs.update_tab_title(id, "Apres");

        assert_eq!(tabs.active_tab().map(|t| t.title.as_str()), Some("Apres"));
    }
}
