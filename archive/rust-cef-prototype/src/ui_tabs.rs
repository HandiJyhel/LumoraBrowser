use std::sync::{Mutex, OnceLock};

use crate::tabs::{BrowserTab, TabId, TabLayout};
use crate::win32::*;

// ── Identifiants de contrôles ─────────────────────────────────────────────────

pub const TAB_NEW_ID: i32 = 2000;
pub const TAB_FIRST_ID: i32 = 2001;
pub const MAX_TAB_SLOTS: usize = 20;

// ── Constantes de géométrie ───────────────────────────────────────────────────

pub const SIDEBAR_W: i32 = 180;
pub const TAB_BTN_H: i32 = 28;
pub const TAB_STRIP_H: i32 = TAB_BTN_H + 4;

const GAP: i32 = 4;
const TAB_W_MIN: i32 = 80;
const TAB_W_MAX: i32 = 180;
const NEW_BTN_W: i32 = 36;

// ── État global ───────────────────────────────────────────────────────────────

static CURRENT_LAYOUT: OnceLock<Mutex<TabLayout>> = OnceLock::new();

// Mapping slot → tab_id (mis à jour dans refresh)
static TAB_SLOT_IDS: OnceLock<Mutex<Vec<u64>>> = OnceLock::new();

// ── API publique ──────────────────────────────────────────────────────────────

pub fn set_layout(layout: TabLayout) {
    if let Ok(mut g) = CURRENT_LAYOUT
        .get_or_init(|| Mutex::new(TabLayout::Horizontal))
        .lock()
    {
        *g = layout;
    }
}

pub fn current_layout() -> TabLayout {
    CURRENT_LAYOUT
        .get_or_init(|| Mutex::new(TabLayout::Horizontal))
        .lock()
        .map(|g| *g)
        .unwrap_or(TabLayout::Horizontal)
}

/// Retourne (x_offset du contenu, hauteur de la bande horizontale, largeur du panneau latéral).
pub fn layout_geometry(layout: TabLayout) -> (i32, i32, i32) {
    match layout {
        TabLayout::Horizontal => (0, TAB_STRIP_H, 0),
        TabLayout::Vertical => (SIDEBAR_W + GAP, 0, SIDEBAR_W),
    }
}

pub fn is_tab_control(id: i32) -> bool {
    id == TAB_NEW_ID || (TAB_FIRST_ID..TAB_FIRST_ID + MAX_TAB_SLOTS as i32).contains(&id)
}

pub fn tab_index_for_control(id: i32) -> Option<usize> {
    if (TAB_FIRST_ID..TAB_FIRST_ID + MAX_TAB_SLOTS as i32).contains(&id) {
        Some((id - TAB_FIRST_ID) as usize)
    } else {
        None
    }
}

/// Retourne le tab_id associé au slot donné (d'après le dernier refresh).
pub fn tab_id_for_slot(slot: usize) -> Option<u64> {
    TAB_SLOT_IDS
        .get_or_init(|| Mutex::new(Vec::new()))
        .lock()
        .ok()
        .and_then(|v| v.get(slot).copied())
}

/// Crée les contrôles boutons d'onglets (à appeler depuis create_initial_controls).
pub fn init(parent: Hwnd, instance: Hinstance) {
    create_tab_button(
        parent, instance, TAB_NEW_ID, 0, 0, NEW_BTN_W, TAB_BTN_H, "+",
    );
    for i in 0..MAX_TAB_SLOTS {
        create_tab_button(
            parent,
            instance,
            TAB_FIRST_ID + i as i32,
            0,
            0,
            TAB_W_MAX,
            TAB_BTN_H,
            "",
        );
        show_control(parent, TAB_FIRST_ID + i as i32, false);
    }
}

/// Positionne les boutons selon le layout et le nombre d'onglets.
pub fn layout(
    parent: Hwnd,
    tab_count: usize,
    layout_mode: TabLayout,
    x_base: i32,
    avail_w: i32,
    ch: i32,
    toolbar_bottom_y: i32,
) {
    match layout_mode {
        TabLayout::Horizontal => {
            layout_horizontal(parent, tab_count, x_base, avail_w, toolbar_bottom_y)
        }
        TabLayout::Vertical => layout_vertical(parent, tab_count, ch),
    }
}

/// Met à jour les libellés et la correspondance slot → tab_id.
pub fn refresh(parent: Hwnd, tabs: &[BrowserTab], active_id: Option<TabId>) {
    let ids: Vec<u64> = tabs.iter().map(|t| t.id.0).collect();
    if let Ok(mut slot_ids) = TAB_SLOT_IDS.get_or_init(|| Mutex::new(Vec::new())).lock() {
        *slot_ids = ids;
    }

    for (i, tab) in tabs.iter().enumerate().take(MAX_TAB_SLOTS) {
        let is_active = active_id == Some(tab.id);
        set_control_text(parent, TAB_FIRST_ID + i as i32, &tab_label(tab, is_active));
    }
}

// ── Layout interne ────────────────────────────────────────────────────────────

fn layout_horizontal(parent: Hwnd, tab_count: usize, x_base: i32, avail_w: i32, y: i32) {
    let tab_w = if tab_count == 0 {
        TAB_W_MAX
    } else {
        let remaining = (avail_w - NEW_BTN_W - GAP).max(0);
        (remaining / tab_count as i32).clamp(TAB_W_MIN, TAB_W_MAX)
    };

    move_control(parent, TAB_NEW_ID, x_base, y, NEW_BTN_W, TAB_BTN_H);
    show_control(parent, TAB_NEW_ID, true);

    let n = tab_count.min(MAX_TAB_SLOTS);
    for i in 0..n {
        let cx = x_base + NEW_BTN_W + GAP + i as i32 * (tab_w + GAP);
        move_control(parent, TAB_FIRST_ID + i as i32, cx, y, tab_w, TAB_BTN_H);
        show_control(parent, TAB_FIRST_ID + i as i32, true);
    }
    for i in n..MAX_TAB_SLOTS {
        show_control(parent, TAB_FIRST_ID + i as i32, false);
    }
}

fn layout_vertical(parent: Hwnd, tab_count: usize, _ch: i32) {
    let btn_w = SIDEBAR_W - GAP * 2;

    move_control(parent, TAB_NEW_ID, GAP, GAP, btn_w, TAB_BTN_H);
    show_control(parent, TAB_NEW_ID, true);

    let n = tab_count.min(MAX_TAB_SLOTS);
    for i in 0..n {
        let cy = GAP + (TAB_BTN_H + GAP) * (i as i32 + 1);
        move_control(parent, TAB_FIRST_ID + i as i32, GAP, cy, btn_w, TAB_BTN_H);
        show_control(parent, TAB_FIRST_ID + i as i32, true);
    }
    for i in n..MAX_TAB_SLOTS {
        show_control(parent, TAB_FIRST_ID + i as i32, false);
    }
}

// ── Helpers ───────────────────────────────────────────────────────────────────

fn tab_label(tab: &BrowserTab, is_active: bool) -> String {
    let title = tab.title.trim();
    let title = if title.is_empty() {
        "Nouvel onglet"
    } else {
        title
    };

    let limit = 20usize;
    let mut label: String = title.chars().take(limit).collect();
    if title.chars().count() > limit {
        label.push_str("..");
    }
    if tab.is_loading {
        label = format!("[..] {label}");
    }
    if is_active {
        format!("> {label}")
    } else {
        label
    }
}

fn create_tab_button(
    parent: Hwnd,
    instance: Hinstance,
    id: i32,
    x: i32,
    y: i32,
    w: i32,
    h: i32,
    text: &str,
) {
    let class = wide("BUTTON");
    let text_w = wide(text);
    unsafe {
        CreateWindowExW(
            0,
            class.as_ptr(),
            text_w.as_ptr(),
            WS_CHILD | WS_VISIBLE,
            x,
            y,
            w,
            h,
            parent,
            id as *mut _,
            instance,
            null_mut(),
        );
    }
}
