use std::cell::Cell;
use std::ptr::null;
use std::sync::{Mutex, OnceLock};

use crate::bookmarks;
use crate::store;
use crate::win32::*;

// ── Hook WH_MSGFILTER pour clic droit dans les popups ────────────────────────

thread_local! {
    static HOOK_MENU_PTR:   Cell<usize> = Cell::new(0);
    static HOOK_PARENT_PTR: Cell<usize> = Cell::new(0);
    static HOOK_HANDLE_PTR: Cell<usize> = Cell::new(0);
    static HOOK_RIGHT_CLICK: Cell<Option<(i32, i32, i32)>> = Cell::new(None);
}

#[derive(Clone)]
struct MenuItemData {
    node_id: String,
    title: String,
    is_folder: bool,
    url: Option<String>,
}

unsafe extern "system" fn msg_filter_hook_proc(
    code: i32,
    w_param: Wparam,
    l_param: Lparam,
) -> Lresult {
    if code == 2
    /* MSGF_MENU */
    {
        let msg = unsafe { &*(l_param as *const Msg) };
        if msg.message == WM_RBUTTONUP {
            let menu = HOOK_MENU_PTR.with(|m| m.get()) as Hmenu;
            let parent = HOOK_PARENT_PTR.with(|p| p.get()) as Hwnd;
            let pt = msg.pt;
            let item_pos = unsafe { MenuItemFromPoint(parent, menu, pt) };
            if item_pos >= 0 {
                HOOK_RIGHT_CLICK.with(|r| r.set(Some((item_pos, pt.x, pt.y))));
                unsafe { EndMenu() };
                return 1;
            }
        }
    }
    let hook = HOOK_HANDLE_PTR.with(|h| h.get()) as *mut std::ffi::c_void;
    unsafe { CallNextHookEx(hook, code, w_param, l_param) }
}

fn install_msg_hook(parent: Hwnd, menu: Hmenu) {
    HOOK_MENU_PTR.with(|m| m.set(menu as usize));
    HOOK_PARENT_PTR.with(|p| p.set(parent as usize));
    HOOK_RIGHT_CLICK.with(|r| r.set(None));
    let thread_id = unsafe { GetCurrentThreadId() };
    let hook = unsafe { SetWindowsHookExW(-1, Some(msg_filter_hook_proc), null_mut(), thread_id) };
    HOOK_HANDLE_PTR.with(|h| h.set(hook as usize));
}

fn uninstall_msg_hook() {
    let hook = HOOK_HANDLE_PTR.with(|h| h.get()) as *mut std::ffi::c_void;
    if !hook.is_null() {
        unsafe { UnhookWindowsHookEx(hook) };
        HOOK_HANDLE_PTR.with(|h| h.set(0));
    }
}

fn collect_top_level_menu_items(
    nodes: &[bookmarks::BookmarkNode],
    folder_id: &str,
) -> Vec<MenuItemData> {
    let mut children: Vec<_> = nodes
        .iter()
        .filter(|n| n.parent_id == folder_id)
        .cloned()
        .collect();
    children.sort_by_key(|n| n.position);
    children
        .iter()
        .map(|n| MenuItemData {
            node_id: n.id.clone(),
            title: n.title.trim().to_string(),
            is_folder: n.kind == bookmarks::BookmarkKind::Folder,
            url: if n.kind == bookmarks::BookmarkKind::Url {
                Some(n.url.clone())
            } else {
                None
            },
        })
        .collect()
}

fn show_popup_item_context_menu(
    parent: Hwnd,
    item: &MenuItemData,
    x: i32,
    y: i32,
) -> Option<MenuCommand> {
    let menu = unsafe { CreatePopupMenu() };
    if menu.is_null() {
        return None;
    }

    append_item(menu, MENU_CTX_OPEN_ID, "Ouvrir");
    if !item.is_folder {
        append_item(menu, MENU_CTX_NEW_TAB_ID, "Ouvrir dans un nouvel onglet");
    }
    append_separator(menu);
    append_item(menu, MENU_CTX_RENAME_ID, "Renommer...");
    append_item(menu, MENU_CTX_DELETE_ID, "Supprimer");

    let cmd = unsafe { TrackPopupMenu(menu, TPM_RETURNCMD, x, y, 0, parent, null()) };
    unsafe { DestroyMenu(menu) };

    match cmd {
        c if c == MENU_CTX_OPEN_ID => {
            if item.is_folder {
                Some(MenuCommand::OpenFolder(item.node_id.clone()))
            } else {
                item.url.clone().map(MenuCommand::OpenUrl)
            }
        }
        c if c == MENU_CTX_NEW_TAB_ID => item.url.clone().map(MenuCommand::OpenInNewTab),
        c if c == MENU_CTX_RENAME_ID => Some(MenuCommand::RenameBookmark(
            item.node_id.clone(),
            item.title.clone(),
        )),
        c if c == MENU_CTX_DELETE_ID => Some(MenuCommand::RemoveBookmark(item.node_id.clone())),
        _ => None,
    }
}

// ── Identifiants de commande menu (privés) ────────────────────────────────────

const MENU_HOME_ID: i32 = 2001;
const MENU_TOGGLE_FAVORITE_ID: i32 = 2002;
const MENU_HISTORY_ID: i32 = 2003;
const MENU_FAVORITES_ID: i32 = 2004;
const MENU_IMPORT_PAGE_ID: i32 = 2007;
const MENU_PROFILE_ID: i32 = 2005;
const MENU_ABOUT_ID: i32 = 2006;
const MENU_TOGGLE_FAVORITES_BAR_ID: i32 = 2008;
const MENU_QUIT_ID: i32 = 2012;
const MENU_CTX_OPEN_ID: i32 = 2100;
const MENU_CTX_DELETE_ID: i32 = 2101;
const MENU_CTX_NEW_TAB_ID: i32 = 2102;
const MENU_CTX_RENAME_ID: i32 = 2103;

pub const BOOKMARK_ACTION_FIRST_ID: i32 = 2400;
pub const BOOKMARK_ACTION_LIMIT: i32 = 400;

// ── Types publics ────────────────────────────────────────────────────────────

/// Action déclenchée depuis un menu de favoris.
#[derive(Clone, Debug)]
pub enum BookmarkMenuAction {
    OpenUrl(String),
    OpenFolder(String),
}

/// Commande retournée par les fonctions de menu — main.rs décide quoi faire.
#[derive(Debug)]
pub enum MenuCommand {
    Home,
    ToggleFavorite,
    History,
    FavoritesPage,
    ImportPage,
    ToggleFavoritesBar,
    Profile,
    About,
    Quit,
    OpenUrl(String),
    OpenInNewTab(String),
    OpenFolder(String),
    RemoveBookmark(String),
    RenameBookmark(String, String), // (node_id, current_title)
}

// ── Registre d'actions bookmark (partagé menu + barre) ───────────────────────

static BOOKMARK_ACTIONS: OnceLock<Mutex<Vec<BookmarkMenuAction>>> = OnceLock::new();

pub fn reset_bookmark_actions() {
    if let Ok(mut v) = BOOKMARK_ACTIONS
        .get_or_init(|| Mutex::new(Vec::new()))
        .lock()
    {
        v.clear();
    }
}

pub fn register_bookmark_action(action: BookmarkMenuAction) -> i32 {
    if let Ok(mut v) = BOOKMARK_ACTIONS
        .get_or_init(|| Mutex::new(Vec::new()))
        .lock()
    {
        if v.len() < BOOKMARK_ACTION_LIMIT as usize {
            v.push(action);
            return BOOKMARK_ACTION_FIRST_ID + v.len() as i32 - 1;
        }
    }
    0
}

pub fn take_bookmark_action(index: usize) -> Option<BookmarkMenuAction> {
    BOOKMARK_ACTIONS
        .get_or_init(|| Mutex::new(Vec::new()))
        .lock()
        .ok()
        .and_then(|v| v.get(index).cloned())
}

// ── Menu principal du navigateur ─────────────────────────────────────────────

/// Affiche le menu principal sous le bouton "Menu" et retourne la commande choisie.
pub fn show_browser_menu(parent: Hwnd, has_current_page: bool) -> Option<MenuCommand> {
    let menu = unsafe { CreatePopupMenu() };
    if menu.is_null() {
        return None;
    }

    append_item(menu, MENU_HOME_ID, "Accueil");
    append_separator(menu);
    append_item(menu, MENU_HISTORY_ID, "Historique");
    build_favorites_submenu(parent, menu, has_current_page);
    append_separator(menu);
    append_item(menu, MENU_PROFILE_ID, "Parametres");
    append_separator(menu);
    append_item(menu, MENU_ABOUT_ID, "A propos de Pulse Browser");
    append_item(menu, MENU_QUIT_ID, "Quitter");

    let button = unsafe { GetDlgItem(parent, crate::MENU_BUTTON_ID) };
    let mut rect = WinRect {
        left: 0,
        top: 0,
        right: 0,
        bottom: 0,
    };
    if button.is_null() || unsafe { GetWindowRect(button, &mut rect) } == 0 {
        unsafe { DestroyMenu(menu) };
        return None;
    }

    let command = unsafe {
        TrackPopupMenu(
            menu,
            TPM_RETURNCMD,
            rect.left,
            rect.bottom,
            0,
            parent,
            null(),
        )
    };
    unsafe { DestroyMenu(menu) };
    resolve_command(command)
}

fn build_favorites_submenu(parent: Hwnd, menu: Hmenu, has_current_page: bool) {
    let _ = parent;
    let sub = unsafe { CreatePopupMenu() };
    if sub.is_null() {
        return;
    }

    if has_current_page {
        append_item(sub, MENU_TOGGLE_FAVORITE_ID, "Ajouter / Retirer ce favori");
    } else {
        append_disabled(sub, "Ajouter / Retirer ce favori");
    }
    append_separator(sub);
    append_item(sub, MENU_FAVORITES_ID, "Gerer les favoris...");
    append_item(sub, MENU_IMPORT_PAGE_ID, "Importer des favoris...");
    append_separator(sub);
    append_item(
        sub,
        MENU_TOGGLE_FAVORITES_BAR_ID,
        "Afficher / masquer la barre",
    );

    append_popup(menu, sub, "Favoris");
}

// ── Menus contextuels de dossiers bookmark ───────────────────────────────────

/// Affiche un menu de dossier bookmark positionné sous un bouton spécifique.
pub fn show_folder_menu_at_button(
    parent: Hwnd,
    button_id: i32,
    folder_id: &str,
) -> Option<MenuCommand> {
    let nodes = store::open_bookmarks().and_then(|s| s.all_nodes()).ok()?;
    let menu = unsafe { CreatePopupMenu() };
    if menu.is_null() {
        return None;
    }

    let items = collect_top_level_menu_items(&nodes, folder_id);
    reset_bookmark_actions();
    append_bookmark_folder_items(menu, &nodes, folder_id, 0);

    let (popup_x, popup_y) =
        button_screen_bottom(parent, button_id).unwrap_or_else(|| window_fallback_pos(parent));

    install_msg_hook(parent, menu);
    let command =
        unsafe { TrackPopupMenu(menu, TPM_RETURNCMD, popup_x, popup_y, 0, parent, null()) };
    uninstall_msg_hook();
    unsafe { DestroyMenu(menu) };

    if let Some((item_pos, x, y)) = HOOK_RIGHT_CLICK.with(|r| r.get()) {
        if let Some(item) = items.get(item_pos as usize) {
            return show_popup_item_context_menu(parent, item, x, y);
        }
    }

    resolve_command(command)
}

/// Affiche un menu de dossier bookmark positionné en haut-gauche de la fenêtre.
pub fn show_folder_menu(parent: Hwnd, folder_id: &str) -> Option<MenuCommand> {
    let nodes = store::open_bookmarks().and_then(|s| s.all_nodes()).ok()?;
    let menu = unsafe { CreatePopupMenu() };
    if menu.is_null() {
        return None;
    }

    let items = collect_top_level_menu_items(&nodes, folder_id);
    reset_bookmark_actions();
    append_bookmark_folder_items(menu, &nodes, folder_id, 0);

    let (popup_x, popup_y) = window_fallback_pos(parent);
    install_msg_hook(parent, menu);
    let command =
        unsafe { TrackPopupMenu(menu, TPM_RETURNCMD, popup_x, popup_y, 0, parent, null()) };
    uninstall_msg_hook();
    unsafe { DestroyMenu(menu) };

    if let Some((item_pos, x, y)) = HOOK_RIGHT_CLICK.with(|r| r.get()) {
        if let Some(item) = items.get(item_pos as usize) {
            return show_popup_item_context_menu(parent, item, x, y);
        }
    }

    resolve_command(command)
}

/// Affiche un menu à partir d'une liste d'actions (pour l'overflow de barre).
pub fn show_actions_menu_at_button(
    parent: Hwnd,
    button_id: i32,
    actions: &[BookmarkMenuAction],
) -> Option<MenuCommand> {
    let menu = unsafe { CreatePopupMenu() };
    if menu.is_null() {
        return None;
    }

    reset_bookmark_actions();
    for action in actions {
        let label = match action {
            BookmarkMenuAction::OpenUrl(url) => crate::privacy::display_host(url),
            BookmarkMenuAction::OpenFolder(id) => store::open_bookmarks()
                .and_then(|s| s.all_nodes())
                .ok()
                .and_then(|nodes| nodes.iter().find(|n| n.id == *id).map(|n| n.title.clone()))
                .unwrap_or_else(|| id.clone()),
        };
        let cmd = register_bookmark_action(action.clone());
        if cmd > 0 {
            append_item(menu, cmd, &label);
        }
    }

    let (popup_x, popup_y) =
        button_screen_bottom(parent, button_id).unwrap_or_else(|| window_fallback_pos(parent));

    let command =
        unsafe { TrackPopupMenu(menu, TPM_RETURNCMD, popup_x, popup_y, 0, parent, null()) };
    unsafe { DestroyMenu(menu) };
    resolve_command(command)
}

// ── Menu contextuel clic droit sur la barre ──────────────────────────────────

/// Affiche le menu contextuel clic droit sur un item de la barre de favoris.
/// `cursor_x` / `cursor_y` sont des coordonnées écran.
pub fn show_bar_item_context_menu(
    parent: Hwnd,
    control_id: i32,
    cursor_x: i32,
    cursor_y: i32,
) -> Option<MenuCommand> {
    use crate::ui_bookmarks_bar;

    let node_id = ui_bookmarks_bar::node_id_for_control(control_id)?;
    let action = ui_bookmarks_bar::action_for_control(control_id)?;

    let is_folder = matches!(action, BookmarkMenuAction::OpenFolder(_));

    // Récupérer le titre actuel depuis le store pour le pré-remplissage du renommage
    let current_title = store::open_bookmarks()
        .and_then(|s| s.all_nodes())
        .ok()
        .and_then(|nodes| {
            nodes
                .iter()
                .find(|n| n.id == node_id)
                .map(|n| n.title.clone())
        })
        .unwrap_or_default();

    let menu = unsafe { CreatePopupMenu() };
    if menu.is_null() {
        return None;
    }

    let open_label = if is_folder { "Ouvrir" } else { "Ouvrir" };
    append_item(menu, MENU_CTX_OPEN_ID, open_label);

    if !is_folder {
        append_item(menu, MENU_CTX_NEW_TAB_ID, "Ouvrir dans un nouvel onglet");
    }

    append_separator(menu);
    append_item(menu, MENU_CTX_RENAME_ID, "Renommer...");
    append_item(menu, MENU_CTX_DELETE_ID, "Supprimer de la barre");

    let command =
        unsafe { TrackPopupMenu(menu, TPM_RETURNCMD, cursor_x, cursor_y, 0, parent, null()) };
    unsafe { DestroyMenu(menu) };

    match command {
        c if c == MENU_CTX_OPEN_ID => match action {
            BookmarkMenuAction::OpenUrl(url) => Some(MenuCommand::OpenUrl(url)),
            BookmarkMenuAction::OpenFolder(id) => Some(MenuCommand::OpenFolder(id)),
        },
        c if c == MENU_CTX_NEW_TAB_ID => match action {
            BookmarkMenuAction::OpenUrl(url) => Some(MenuCommand::OpenInNewTab(url)),
            _ => None,
        },
        c if c == MENU_CTX_RENAME_ID => Some(MenuCommand::RenameBookmark(node_id, current_title)),
        c if c == MENU_CTX_DELETE_ID => Some(MenuCommand::RemoveBookmark(node_id)),
        _ => None,
    }
}

// ── Résolution de commande ────────────────────────────────────────────────────

fn resolve_command(command: i32) -> Option<MenuCommand> {
    if command == 0 {
        return None;
    }

    let action_range = BOOKMARK_ACTION_FIRST_ID..BOOKMARK_ACTION_FIRST_ID + BOOKMARK_ACTION_LIMIT;

    if action_range.contains(&command) {
        let index = (command - BOOKMARK_ACTION_FIRST_ID) as usize;
        return match take_bookmark_action(index) {
            Some(BookmarkMenuAction::OpenUrl(url)) => Some(MenuCommand::OpenUrl(url)),
            Some(BookmarkMenuAction::OpenFolder(id)) => Some(MenuCommand::OpenFolder(id)),
            None => None,
        };
    }

    match command {
        MENU_HOME_ID => Some(MenuCommand::Home),
        MENU_TOGGLE_FAVORITE_ID => Some(MenuCommand::ToggleFavorite),
        MENU_HISTORY_ID => Some(MenuCommand::History),
        MENU_FAVORITES_ID => Some(MenuCommand::FavoritesPage),
        MENU_IMPORT_PAGE_ID => Some(MenuCommand::ImportPage),
        MENU_TOGGLE_FAVORITES_BAR_ID => Some(MenuCommand::ToggleFavoritesBar),
        MENU_PROFILE_ID => Some(MenuCommand::Profile),
        MENU_ABOUT_ID => Some(MenuCommand::About),
        MENU_QUIT_ID => Some(MenuCommand::Quit),
        _ => None,
    }
}

// ── Constructions de sous-menus bookmark ─────────────────────────────────────

pub fn append_bookmark_folder_items(
    menu: Hmenu,
    nodes: &[bookmarks::BookmarkNode],
    parent_id: &str,
    depth: usize,
) {
    let mut children: Vec<_> = nodes
        .iter()
        .filter(|n| n.parent_id == parent_id)
        .cloned()
        .collect();
    children.sort_by_key(|n| n.position);

    for node in children {
        match node.kind {
            bookmarks::BookmarkKind::Url => {
                let id = register_bookmark_action(BookmarkMenuAction::OpenUrl(node.url.clone()));
                append_item(menu, id, &node.title);
            }
            bookmarks::BookmarkKind::Folder => {
                let label = if node.title.trim().is_empty() {
                    "\u{1F4C1}".to_string()
                } else {
                    format!("\u{1F4C1} {}", node.title)
                };
                let submenu = unsafe { CreatePopupMenu() };
                if submenu.is_null() || depth >= 5 {
                    append_disabled(menu, &label);
                } else {
                    append_bookmark_folder_items(submenu, nodes, &node.id, depth + 1);
                    append_popup(menu, submenu, &label);
                }
            }
        }
    }
}

// ── Helpers Win32 (privés) ───────────────────────────────────────────────────

fn append_item(menu: Hmenu, id: i32, text: &str) {
    let text = wide(text);
    unsafe { AppendMenuW(menu, MF_STRING, id as usize, text.as_ptr()) };
}

fn append_popup(menu: Hmenu, submenu: Hmenu, text: &str) {
    let text = wide(text);
    unsafe { AppendMenuW(menu, MF_POPUP, submenu as usize, text.as_ptr()) };
}

fn append_disabled(menu: Hmenu, text: &str) {
    let text = wide(text);
    unsafe { AppendMenuW(menu, MF_STRING | MF_GRAYED, 0, text.as_ptr()) };
}

fn append_separator(menu: Hmenu) {
    unsafe { AppendMenuW(menu, MF_SEPARATOR, 0, null()) };
}

fn button_screen_bottom(parent: Hwnd, button_id: i32) -> Option<(i32, i32)> {
    let button = unsafe { GetDlgItem(parent, button_id) };
    if button.is_null() {
        return None;
    }
    let mut rect = WinRect {
        left: 0,
        top: 0,
        right: 0,
        bottom: 0,
    };
    if unsafe { GetWindowRect(button, &mut rect) } == 0 {
        return None;
    }
    Some((rect.left, rect.bottom))
}

fn window_fallback_pos(parent: Hwnd) -> (i32, i32) {
    let mut rect = WinRect {
        left: 0,
        top: 0,
        right: 0,
        bottom: 0,
    };
    let _ = unsafe { GetWindowRect(parent, &mut rect) };
    (rect.left + 16, rect.top + 86)
}
