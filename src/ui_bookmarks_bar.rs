use std::sync::{Mutex, OnceLock};

use crate::bookmarks;
use crate::privacy;
use crate::store;
use crate::ui_menu::{BookmarkMenuAction, MenuCommand};
use crate::win32::*;

// ── Constantes ────────────────────────────────────────────────────────────────

pub const BAR_FIRST_ID: i32 = 3000;
pub const BAR_ITEM_COUNT: i32 = 24;
pub const BAR_OTHER_ID: i32 = 3100;
pub const BAR_OVERFLOW_ID: i32 = 3101;

const ITEM_W: i32 = 120;
const OTHER_W: i32 = 130;
const OVERFLOW_W: i32 = 36;
const GAP: i32 = 6;

// ── Statics ───────────────────────────────────────────────────────────────────

static BAR_VISIBLE: OnceLock<Mutex<bool>> = OnceLock::new();
static BAR_ACTIONS: OnceLock<Mutex<Vec<BookmarkMenuAction>>> = OnceLock::new();
static BAR_NODE_IDS: OnceLock<Mutex<Vec<String>>> = OnceLock::new();
static OTHER_HAS_CONTENT: OnceLock<Mutex<bool>> = OnceLock::new();
static BAR_SHOWN_COUNT: OnceLock<Mutex<usize>> = OnceLock::new();

// ── API publique ──────────────────────────────────────────────────────────────

/// Retourne vrai si le contrôle appartient à la barre de favoris.
pub fn is_bar_control(control_id: i32) -> bool {
    (BAR_FIRST_ID..BAR_FIRST_ID + BAR_ITEM_COUNT).contains(&control_id)
        || control_id == BAR_OTHER_ID
        || control_id == BAR_OVERFLOW_ID
}

pub fn is_visible() -> bool {
    BAR_VISIBLE
        .get_or_init(|| Mutex::new(true))
        .lock()
        .map(|v| *v)
        .unwrap_or(true)
}

/// Bascule l'affichage de la barre et retourne le nouvel état.
pub fn toggle_visibility() -> bool {
    let mut guard = BAR_VISIBLE.get_or_init(|| Mutex::new(true)).lock().ok();
    if let Some(ref mut v) = guard {
        **v = !**v;
        **v
    } else {
        true
    }
}

/// Relit les favoris de la barre depuis le store, met à jour les textes de contrôles.
pub fn refresh(parent: Hwnd) {
    let store = store::open_bookmarks().ok();
    let toolbar_items = store
        .as_ref()
        .and_then(|s| s.toolbar_children().ok())
        .unwrap_or_default();
    let other_has_content = store
        .as_ref()
        .and_then(|s| s.children_of(bookmarks::OTHER_ROOT_ID).ok())
        .map(|c| !c.is_empty())
        .unwrap_or(false);

    if let Ok(mut v) = OTHER_HAS_CONTENT.get_or_init(|| Mutex::new(false)).lock() {
        *v = other_has_content;
    }

    let mut actions: Vec<BookmarkMenuAction> = Vec::new();
    let mut node_ids: Vec<String> = Vec::new();
    for (i, item) in toolbar_items
        .iter()
        .take(BAR_ITEM_COUNT as usize)
        .enumerate()
    {
        let action = match item.kind {
            bookmarks::BookmarkKind::Url => BookmarkMenuAction::OpenUrl(item.url.clone()),
            bookmarks::BookmarkKind::Folder => BookmarkMenuAction::OpenFolder(item.id.clone()),
        };
        actions.push(action);
        node_ids.push(item.id.clone());
        set_control_text(
            parent,
            BAR_FIRST_ID + i as i32,
            &bar_item_label(
                &item.title,
                &item.url,
                item.kind == bookmarks::BookmarkKind::Folder,
            ),
        );
    }
    for i in toolbar_items.len()..BAR_ITEM_COUNT as usize {
        set_control_text(parent, BAR_FIRST_ID + i as i32, "");
    }

    if let Ok(mut stored) = BAR_ACTIONS.get_or_init(|| Mutex::new(Vec::new())).lock() {
        *stored = actions;
    }
    if let Ok(mut stored) = BAR_NODE_IDS.get_or_init(|| Mutex::new(Vec::new())).lock() {
        *stored = node_ids;
    }
}

/// Retourne l'identifiant de nœud du favori à la position donnée dans la barre.
pub fn node_id_for_control(control_id: i32) -> Option<String> {
    if !is_bar_control(control_id) || control_id == BAR_OTHER_ID || control_id == BAR_OVERFLOW_ID {
        return None;
    }
    let index = (control_id - BAR_FIRST_ID) as usize;
    BAR_NODE_IDS
        .get_or_init(|| Mutex::new(Vec::new()))
        .lock()
        .ok()
        .and_then(|v| v.get(index).cloned())
}

/// Retourne l'action associée au contrôle de barre (sans l'exécuter).
pub fn action_for_control(control_id: i32) -> Option<BookmarkMenuAction> {
    if !is_bar_control(control_id) || control_id == BAR_OTHER_ID || control_id == BAR_OVERFLOW_ID {
        return None;
    }
    let index = (control_id - BAR_FIRST_ID) as usize;
    BAR_ACTIONS
        .get_or_init(|| Mutex::new(Vec::new()))
        .lock()
        .ok()
        .and_then(|v| v.get(index).cloned())
}

/// Gère le clic sur un contrôle de la barre — retourne la commande à exécuter.
pub fn handle_click(parent: Hwnd, control_id: i32) -> Option<MenuCommand> {
    if control_id == BAR_OTHER_ID {
        return crate::ui_menu::show_folder_menu_at_button(
            parent,
            control_id,
            bookmarks::OTHER_ROOT_ID,
        );
    }

    if control_id == BAR_OVERFLOW_ID {
        return show_overflow_menu(parent);
    }

    let index = (control_id - BAR_FIRST_ID) as usize;
    let action = BAR_ACTIONS
        .get_or_init(|| Mutex::new(Vec::new()))
        .lock()
        .ok()
        .and_then(|v| v.get(index).cloned())?;

    match action {
        BookmarkMenuAction::OpenUrl(url) => Some(MenuCommand::OpenUrl(url)),
        BookmarkMenuAction::OpenFolder(id) => {
            crate::ui_menu::show_folder_menu_at_button(parent, control_id, &id)
        }
    }
}

/// Positionne et affiche/masque tous les contrôles de la barre selon la largeur disponible.
pub fn layout(parent: Hwnd, x: i32, y: i32, width: i32, height: i32) {
    if !is_visible() {
        for i in 0..BAR_ITEM_COUNT {
            show_control(parent, BAR_FIRST_ID + i, false);
        }
        show_control(parent, BAR_OTHER_ID, false);
        show_control(parent, BAR_OVERFLOW_ID, false);
        return;
    }

    let n_actions = BAR_ACTIONS
        .get_or_init(|| Mutex::new(Vec::new()))
        .lock()
        .map(|v| v.len())
        .unwrap_or(0);
    let other_has_content = OTHER_HAS_CONTENT
        .get_or_init(|| Mutex::new(false))
        .lock()
        .map(|v| *v)
        .unwrap_or(false);

    let other_reserved = if other_has_content { OTHER_W + GAP } else { 0 };
    let avail = (width - other_reserved).max(0);

    let n_fit_all = if avail < ITEM_W {
        0
    } else {
        (((avail + GAP) / (ITEM_W + GAP)) as usize).min(n_actions)
    };

    let (n_fit, show_overflow) = if n_fit_all >= n_actions {
        (n_actions, false)
    } else {
        let avail_with_ov = (avail - OVERFLOW_W - GAP).max(0);
        let n = if avail_with_ov < ITEM_W {
            0
        } else {
            (((avail_with_ov + GAP) / (ITEM_W + GAP)) as usize).min(n_actions.saturating_sub(1))
        };
        (n, true)
    };

    if let Ok(mut v) = BAR_SHOWN_COUNT.get_or_init(|| Mutex::new(0)).lock() {
        *v = n_fit;
    }

    for i in 0..BAR_ITEM_COUNT as usize {
        let cid = BAR_FIRST_ID + i as i32;
        let show = i < n_fit;
        if show {
            move_control(
                parent,
                cid,
                x + i as i32 * (ITEM_W + GAP),
                y,
                ITEM_W,
                height,
            );
        }
        show_control(parent, cid, show);
        set_control_enabled(parent, cid, show);
    }

    let items_end = x + n_fit as i32 * (ITEM_W + GAP);
    if show_overflow {
        move_control(parent, BAR_OVERFLOW_ID, items_end, y, OVERFLOW_W, height);
    }
    show_control(parent, BAR_OVERFLOW_ID, show_overflow);
    set_control_enabled(parent, BAR_OVERFLOW_ID, show_overflow);

    if other_has_content {
        move_control(
            parent,
            BAR_OTHER_ID,
            x + width - OTHER_W,
            y,
            OTHER_W,
            height,
        );
    }
    show_control(parent, BAR_OTHER_ID, other_has_content);
    set_control_enabled(parent, BAR_OTHER_ID, other_has_content);
}

// ── Helpers privés ────────────────────────────────────────────────────────────

fn show_overflow_menu(parent: Hwnd) -> Option<MenuCommand> {
    let shown_count = BAR_SHOWN_COUNT
        .get_or_init(|| Mutex::new(0))
        .lock()
        .map(|v| *v)
        .unwrap_or(0);

    let actions = BAR_ACTIONS
        .get_or_init(|| Mutex::new(Vec::new()))
        .lock()
        .ok()?;

    let overflow: Vec<BookmarkMenuAction> = actions[shown_count.min(actions.len())..].to_vec();
    drop(actions);

    if overflow.is_empty() {
        return None;
    }

    crate::ui_menu::show_actions_menu_at_button(parent, BAR_OVERFLOW_ID, &overflow)
}

fn bar_item_label(title: &str, url: &str, is_folder: bool) -> String {
    let title = title.trim();
    let has_title = !title.is_empty() && title != "Page sans titre";

    if is_folder && !has_title {
        return "\u{1F4C1}".to_string();
    }

    let label = if has_title {
        title.to_string()
    } else {
        privacy::display_host(url)
    };

    let limit = if is_folder { 18 } else { 22 };
    let mut clipped: String = label.chars().take(limit).collect();
    if label.chars().count() > limit {
        clipped.push_str("...");
    }
    if is_folder {
        format!("\u{1F4C1} {clipped}")
    } else {
        clipped
    }
}
