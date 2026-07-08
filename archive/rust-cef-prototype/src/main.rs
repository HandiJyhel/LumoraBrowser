#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod bookmarks;
mod bookmarks_import;
mod ipc_server;
mod browser_data;
mod cef_runtime;
mod credentials;
mod history;
mod import_wizard;
mod local_pages;
mod privacy;
mod profile;
mod settings;
mod store;
mod tabs;
mod ui_bookmarks_bar;
mod ui_menu;
mod ui_tabs;
mod vault;
mod vault_lock;
mod win32;

use std::cell::{Cell, RefCell};
use std::ptr::null;
use win32::*;

// ── Constantes de contrôle ────────────────────────────────────────────────────

pub const APP_NAME: &str = "Pulse Browser";
pub const APP_VERSION: &str = env!("CARGO_PKG_VERSION");

pub const ADDRESS_CONTROL_ID: i32 = 1001;
pub const OPEN_BUTTON_ID: i32 = 1002;
pub const STATUS_CONTROL_ID: i32 = 1003;
pub const RENDER_AREA_CONTROL_ID: i32 = 1004;
pub const BACK_BUTTON_ID: i32 = 1005;
pub const FORWARD_BUTTON_ID: i32 = 1006;
pub const RELOAD_BUTTON_ID: i32 = 1007;
pub const STOP_BUTTON_ID: i32 = 1008;
pub const HOME_BUTTON_ID: i32 = 1009;
pub const FAVORITE_BUTTON_ID: i32 = 1010;
pub const MENU_BUTTON_ID: i32 = 1011;

// ── État onglets (thread principal) ──────────────────────────────────────────

thread_local! {
    static TABS: RefCell<tabs::TabManager> = RefCell::new(tabs::TabManager::new());
    // Dernières limites calculées pour la zone de rendu CEF
    static RENDER_BOUNDS: Cell<(i32, i32, i32, i32)> = Cell::new((8, 74, 900, 400));
}

// ── Point d'entrée ────────────────────────────────────────────────────────────

fn main() {
    if std::env::args().any(|a| a == "--serve") {
        if let Err(e) = ipc_server::run() {
            eprintln!("Pulse Browser core: {e}");
            std::process::exit(1);
        }
        return;
    }

    if let Err(error) = run() {
        eprintln!("Erreur Pulse Browser: {error}");
        std::process::exit(1);
    }
}

fn run() -> Result<(), String> {
    cef_runtime::handle_subprocess_or_initialize()?;

    let class_name = wide("PulseBrowserWindow");
    let window_title = wide(&format!("{APP_NAME} {APP_VERSION}"));

    let instance = unsafe { GetModuleHandleW(null()) };
    if instance.is_null() {
        return Err("impossible de recuperer le module Windows courant".to_string());
    }

    let window_class = WndClassW {
        style: 0,
        lpfn_wnd_proc: Some(window_proc),
        cb_cls_extra: 0,
        cb_wnd_extra: 0,
        h_instance: instance,
        h_icon: null_mut(),
        h_cursor: unsafe { LoadCursorW(null_mut(), IDC_ARROW as *const u16) },
        hbr_background: (COLOR_WINDOW + 1) as Hbrush,
        lpsz_menu_name: null(),
        lpsz_class_name: class_name.as_ptr(),
    };

    if unsafe { RegisterClassW(&window_class) } == 0 {
        return Err("impossible d'enregistrer la classe de fenetre".to_string());
    }

    let hwnd = unsafe {
        CreateWindowExW(
            0,
            class_name.as_ptr(),
            window_title.as_ptr(),
            WS_OVERLAPPEDWINDOW | WS_VISIBLE,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            1024,
            720,
            null_mut(),
            null_mut(),
            instance,
            null_mut(),
        )
    };

    if hwnd.is_null() {
        return Err("impossible de creer la fenetre principale".to_string());
    }

    unsafe {
        ShowWindow(hwnd, SW_SHOW);
        UpdateWindow(hwnd);
    }

    message_loop(hwnd);
    Ok(())
}

// ── Procédure de fenêtre ──────────────────────────────────────────────────────

unsafe extern "system" fn window_proc(
    hwnd: Hwnd,
    msg: u32,
    w_param: Wparam,
    l_param: Lparam,
) -> Lresult {
    match msg {
        WM_CREATE => {
            // Charger le layout d'onglets depuis les paramètres
            if let Ok(p) = profile::PulseProfile::default() {
                let s = settings::PulseSettings::load(&p.settings_file);
                let layout = s.tab_bar.to_tab_layout();
                ui_tabs::set_layout(layout);
                TABS.with(|tabs| tabs.borrow_mut().set_layout(layout));
            }

            create_initial_controls(hwnd);
            ui_bookmarks_bar::refresh(hwnd);
            layout_controls(hwnd);

            // Créer le premier onglet CEF
            let (rx, ry, rw, rh) = RENDER_BOUNDS.with(|rb| rb.get());
            let tab_id = tabs::TabId(cef_runtime::allocate_tab_id());
            TABS.with(|tabs| {
                tabs.borrow_mut()
                    .open_tab_with_id(tab_id, local_pages::HOME_ADDRESS, "Accueil");
            });

            match cef_runtime::create_tab(hwnd, local_pages::HOME_ADDRESS, tab_id.0, rx, ry, rw, rh)
            {
                Ok(()) => {
                    refresh_tab_bar(hwnd);
                    open_start_page(hwnd);
                }
                Err(e) => set_status(hwnd, &format!("Demarrage CEF: {e}")),
            }
            0
        }
        WM_SIZE => {
            layout_controls(hwnd);
            0
        }
        WM_COMMAND => {
            let control_id = low_word(w_param);
            let notification = high_word(w_param);

            if notification != BN_CLICKED {
                return unsafe { DefWindowProcW(hwnd, msg, w_param, l_param) };
            }

            match control_id {
                id if id == OPEN_BUTTON_ID => open_address_from_bar(hwnd),
                id if id == MENU_BUTTON_ID => {
                    let has_page = cef_runtime::current_page().is_some();
                    if let Some(cmd) = ui_menu::show_browser_menu(hwnd, has_page) {
                        dispatch_menu_command(hwnd, cmd);
                    }
                }
                id if id == ui_tabs::TAB_NEW_ID => {
                    open_new_tab(hwnd, local_pages::HOME_ADDRESS);
                }
                id if ui_tabs::is_tab_control(id) => {
                    if let Some(slot) = ui_tabs::tab_index_for_control(id) {
                        if let Some(raw_id) = ui_tabs::tab_id_for_slot(slot) {
                            switch_tab_to(hwnd, tabs::TabId(raw_id));
                        }
                    }
                }
                id if ui_bookmarks_bar::is_bar_control(id) => {
                    if let Some(cmd) = ui_bookmarks_bar::handle_click(hwnd, id) {
                        dispatch_menu_command(hwnd, cmd);
                    }
                }
                _ => {
                    handle_nav_button(hwnd, control_id);
                }
            }
            0
        }
        WM_DESTROY => {
            cef_runtime::shutdown_if_needed();
            unsafe { PostQuitMessage(0) };
            0
        }
        _ => unsafe { DefWindowProcW(hwnd, msg, w_param, l_param) },
    }
}

// ── Boucle de messages ────────────────────────────────────────────────────────

fn message_loop(main_window: Hwnd) {
    let mut msg = Msg {
        hwnd: null_mut(),
        message: 0,
        w_param: 0,
        l_param: 0,
        time: 0,
        pt: Point { x: 0, y: 0 },
    };

    loop {
        while unsafe { PeekMessageW(&mut msg, null_mut(), 0, 0, PM_REMOVE) } > 0 {
            if msg.message == WM_QUIT {
                return;
            }
            if handle_address_bar_enter(&msg) {
                continue;
            }
            if msg.message == WM_RBUTTONUP {
                let control_id = unsafe { GetDlgCtrlID(msg.hwnd) };

                // Clic droit sur onglet → fermer
                if ui_tabs::is_tab_control(control_id) && control_id != ui_tabs::TAB_NEW_ID {
                    if let Some(slot) = ui_tabs::tab_index_for_control(control_id) {
                        if let Some(raw_id) = ui_tabs::tab_id_for_slot(slot) {
                            let tab_id = tabs::TabId(raw_id);
                            if show_close_tab_menu(main_window, msg.pt.x, msg.pt.y) {
                                do_close_tab(main_window, tab_id);
                            }
                        }
                    }
                    continue;
                }

                // Clic droit sur barre de favoris
                if ui_bookmarks_bar::is_bar_control(control_id)
                    && control_id != ui_bookmarks_bar::BAR_OTHER_ID
                    && control_id != ui_bookmarks_bar::BAR_OVERFLOW_ID
                {
                    if let Some(cmd) = ui_menu::show_bar_item_context_menu(
                        main_window,
                        control_id,
                        msg.pt.x,
                        msg.pt.y,
                    ) {
                        dispatch_menu_command(main_window, cmd);
                    }
                    continue;
                }
            }
            unsafe {
                TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
        }

        cef_runtime::do_message_loop_work_if_needed();

        if let Some(update) = cef_runtime::take_ui_update() {
            apply_browser_ui_update(main_window, update);
        }

        if let Some(action) = cef_runtime::take_pulse_action() {
            handle_pulse_action(main_window, action);
        }

        unsafe { Sleep(10) };
    }
}

// ── Création des contrôles initiaux ──────────────────────────────────────────

fn create_initial_controls(parent: Hwnd) {
    let instance = unsafe { GetWindowLongPtrW(parent, GWLP_HINSTANCE) as Hinstance };

    make_button(parent, instance, BACK_BUTTON_ID, 8, 8, 64, 30, "Retour");
    make_button(
        parent,
        instance,
        FORWARD_BUTTON_ID,
        78,
        8,
        70,
        30,
        "Avancer",
    );
    make_button(
        parent,
        instance,
        RELOAD_BUTTON_ID,
        154,
        8,
        84,
        30,
        "Recharger",
    );
    make_button(parent, instance, STOP_BUTTON_ID, 244, 8, 56, 30, "Stop");
    make_button(parent, instance, HOME_BUTTON_ID, 306, 8, 70, 30, "Accueil");
    make_edit(
        parent,
        instance,
        ADDRESS_CONTROL_ID,
        382,
        8,
        388,
        30,
        local_pages::HOME_ADDRESS,
    );
    make_button(
        parent,
        instance,
        FAVORITE_BUTTON_ID,
        776,
        8,
        78,
        30,
        "Favori",
    );
    make_button(parent, instance, OPEN_BUTTON_ID, 860, 8, 74, 30, "Ouvrir");
    make_button(parent, instance, MENU_BUTTON_ID, 940, 8, 64, 30, "Menu");

    // Contrôles d'onglets
    ui_tabs::init(parent, instance);

    for i in 0..ui_bookmarks_bar::BAR_ITEM_COUNT {
        make_button(
            parent,
            instance,
            ui_bookmarks_bar::BAR_FIRST_ID + i,
            8,
            44,
            120,
            28,
            "",
        );
    }
    make_button(
        parent,
        instance,
        ui_bookmarks_bar::BAR_OTHER_ID,
        8,
        44,
        130,
        28,
        "Autres favoris >",
    );
    make_button(
        parent,
        instance,
        ui_bookmarks_bar::BAR_OVERFLOW_ID,
        8,
        44,
        36,
        28,
        ">>",
    );

    make_label(
        parent,
        instance,
        STATUS_CONTROL_ID,
        8,
        646,
        860,
        22,
        "Pret.",
    );
    make_label(
        parent,
        instance,
        RENDER_AREA_CONTROL_ID,
        8,
        46,
        904,
        300,
        "",
    );
}

// ── Layout ────────────────────────────────────────────────────────────────────

fn layout_controls(parent: Hwnd) {
    let Some((cw, ch)) = client_size(parent) else {
        return;
    };

    let layout = ui_tabs::current_layout();
    let (x_content_offset, tab_strip_h, sidebar_w) = ui_tabs::layout_geometry(layout);

    let m = 8;
    let ty = 8;
    let ch_btn = 30;
    let gap = 6;
    let bar_h = 28;

    // Décalage horizontal pour la zone de contenu et la barre d'outils
    let x_off = if sidebar_w > 0 { sidebar_w + m } else { 0 };
    let _ = x_content_offset; // identique à x_off

    let content_w = (cw - m - x_off - m).max(320);

    let back_w = 64;
    let fwd_w = 70;
    let reload_w = 84;
    let stop_w = 56;
    let home_w = 70;
    let fav_w = 78;
    let open_w = 74;
    let menu_w = 64;

    let back_x = m + x_off;
    let fwd_x = back_x + back_w + gap;
    let reload_x = fwd_x + fwd_w + gap;
    let stop_x = reload_x + reload_w + gap;
    let home_x = stop_x + stop_w + gap;
    let addr_x = home_x + home_w + gap;
    let menu_x = (cw - m - menu_w).max(addr_x + 260);
    let open_x = menu_x - open_w - gap;
    let favbtn_x = open_x - fav_w - gap;
    let addr_w = (favbtn_x - addr_x - gap).max(180);

    // Y de la bande d'onglets horizontale (juste sous la barre d'outils)
    let tab_strip_y = ty + ch_btn + 4; // = 42

    // Y de la barre de favoris : décalé si bande horizontale présente
    let bar_y = if tab_strip_h > 0 {
        tab_strip_y + tab_strip_h // = 42 + 32 = 74 (horizontal)
    } else {
        ty + ch_btn + 8 // = 46 (vertical — comme avant)
    };

    let render_y = if ui_bookmarks_bar::is_visible() {
        bar_y + bar_h + 4
    } else {
        bar_y
    };
    let status_h = 22;
    let status_y = (ch - status_h - 4).max(render_y + 40);
    let render_h = (status_y - render_y - 4).max(180);

    move_control(parent, BACK_BUTTON_ID, back_x, ty, back_w, ch_btn);
    move_control(parent, FORWARD_BUTTON_ID, fwd_x, ty, fwd_w, ch_btn);
    move_control(parent, RELOAD_BUTTON_ID, reload_x, ty, reload_w, ch_btn);
    move_control(parent, STOP_BUTTON_ID, stop_x, ty, stop_w, ch_btn);
    move_control(parent, HOME_BUTTON_ID, home_x, ty, home_w, ch_btn);
    move_control(parent, ADDRESS_CONTROL_ID, addr_x, ty, addr_w, ch_btn);
    move_control(parent, FAVORITE_BUTTON_ID, favbtn_x, ty, fav_w, ch_btn);
    move_control(parent, OPEN_BUTTON_ID, open_x, ty, open_w, ch_btn);
    move_control(parent, MENU_BUTTON_ID, menu_x, ty, menu_w, ch_btn);

    // Barre d'onglets
    let tab_count = TABS.with(|tabs| tabs.borrow().tabs().len());
    ui_tabs::layout(
        parent,
        tab_count,
        layout,
        m + x_off,
        content_w,
        ch,
        tab_strip_y,
    );

    ui_bookmarks_bar::layout(parent, m + x_off, bar_y, content_w, bar_h);

    move_control(
        parent,
        STATUS_CONTROL_ID,
        m + x_off,
        status_y,
        content_w,
        status_h,
    );
    move_control(
        parent,
        RENDER_AREA_CONTROL_ID,
        m + x_off,
        render_y,
        content_w,
        render_h,
    );

    // Mémoriser les limites de rendu pour la création des futurs onglets
    RENDER_BOUNDS.with(|rb| rb.set((m + x_off, render_y, content_w, render_h)));

    cef_runtime::resize_browser(m + x_off, render_y, content_w, render_h);
}

// ── Gestion des onglets ───────────────────────────────────────────────────────

fn open_new_tab(parent: Hwnd, url: &str) {
    let (rx, ry, rw, rh) = RENDER_BOUNDS.with(|rb| rb.get());
    let tab_id = tabs::TabId(cef_runtime::allocate_tab_id());

    TABS.with(|tabs| {
        tabs.borrow_mut()
            .open_tab_with_id(tab_id, url, "Nouvel onglet");
    });

    match cef_runtime::create_tab(parent, url, tab_id.0, rx, ry, rw, rh) {
        Ok(()) => {
            layout_controls(parent);
            refresh_tab_bar(parent);
        }
        Err(e) => {
            TABS.with(|tabs| {
                tabs.borrow_mut().close_tab(tab_id);
            });
            set_status(parent, &format!("Impossible d'ouvrir l'onglet: {e}"));
        }
    }
}

fn switch_tab_to(parent: Hwnd, tab_id: tabs::TabId) {
    let (rx, ry, rw, rh) = RENDER_BOUNDS.with(|rb| rb.get());
    TABS.with(|tabs| {
        tabs.borrow_mut().activate_tab(tab_id);
    });

    match cef_runtime::switch_tab(tab_id.0, rx, ry, rw, rh) {
        Ok(info) => {
            if !info.url.is_empty() {
                set_control_text(parent, ADDRESS_CONTROL_ID, &info.url);
            }
            set_control_enabled(parent, BACK_BUTTON_ID, info.can_go_back);
            set_control_enabled(parent, FORWARD_BUTTON_ID, info.can_go_forward);
            set_control_enabled(parent, STOP_BUTTON_ID, info.is_loading);
            refresh_tab_bar(parent);
        }
        Err(e) => set_status(parent, &e),
    }
}

fn do_close_tab(parent: Hwnd, tab_id: tabs::TabId) {
    let result = cef_runtime::close_tab(tab_id.0);
    TABS.with(|tabs| {
        tabs.borrow_mut().close_tab(tab_id);
    });

    match result {
        cef_runtime::CloseTabResult::WasInactive => {
            layout_controls(parent);
            refresh_tab_bar(parent);
        }
        cef_runtime::CloseTabResult::WasActive { next_tab_id } => {
            if let Some(next_raw) = next_tab_id {
                switch_tab_to(parent, tabs::TabId(next_raw));
            } else {
                // Tous les onglets fermés → ouvrir un nouvel onglet
                open_new_tab(parent, local_pages::HOME_ADDRESS);
            }
            layout_controls(parent);
        }
    }
}

fn refresh_tab_bar(parent: Hwnd) {
    TABS.with(|tabs| {
        let tabs = tabs.borrow();
        ui_tabs::refresh(parent, tabs.tabs(), tabs.active_tab_id());
    });
}

fn show_close_tab_menu(parent: Hwnd, x: i32, y: i32) -> bool {
    let menu = unsafe { CreatePopupMenu() };
    if menu.is_null() {
        return false;
    }
    let text = wide("Fermer l'onglet");
    unsafe {
        AppendMenuW(menu, MF_STRING, 1, text.as_ptr());
    }
    let cmd = unsafe { TrackPopupMenu(menu, TPM_RETURNCMD, x, y, 0, parent, null()) };
    unsafe { DestroyMenu(menu) };
    cmd == 1
}

// ── Navigation ────────────────────────────────────────────────────────────────

fn open_start_page(parent: Hwnd) {
    if store::open_bookmarks()
        .and_then(|s| s.all_nodes())
        .map(|nodes| {
            let has_real = nodes.iter().any(|n| n.kind == bookmarks::BookmarkKind::Url);
            !has_real
        })
        .unwrap_or(false)
    {
        open_local_page(parent, &import_wizard::wizard_page_data_url());
        return;
    }
    let startup = load_settings().startup_url();
    if startup == local_pages::HOME_ADDRESS {
        open_home_page(parent);
    } else {
        open_url(parent, &startup);
    }
}

pub fn open_home_page(parent: Hwnd) {
    set_control_text(parent, ADDRESS_CONTROL_ID, local_pages::HOME_ADDRESS);
    let page = local_pages::home_page_data_url(APP_VERSION);
    load_local_page(parent, &page, "Accueil local ouvert.");
}

pub fn open_history_page(parent: Hwnd) {
    match store::open_browser_data().and_then(|s| s.recent_history(200)) {
        Ok(entries) => open_local_page(parent, &local_pages::history_page_data_url(&entries)),
        Err(e) => set_status(parent, &format!("Historique indisponible: {e}")),
    }
}

pub fn open_favorites_page(parent: Hwnd) {
    match store::open_bookmarks().and_then(|s| s.all_nodes()) {
        Ok(nodes) => open_local_page(parent, &local_pages::bookmarks_page_data_url(&nodes)),
        Err(e) => set_status(parent, &format!("Favoris indisponibles: {e}")),
    }
}

pub fn open_url(parent: Hwnd, url: &str) {
    let host = privacy::display_host(url);
    set_status(parent, &format!("Ouverture de {host}..."));
    match cef_runtime::load_url_in_child(parent, url) {
        Ok(()) => {
            set_render_area(parent, "");
            set_status(parent, &format!("Navigation vers {host}."));
        }
        Err(e) => {
            set_render_area(parent, &format!("Erreur de rendu interne:\r\n{e}"));
            set_status(parent, "Impossible de charger la page dans Pulse Browser.");
        }
    }
}

fn load_settings() -> settings::PulseSettings {
    match profile::PulseProfile::default() {
        Ok(p) => settings::PulseSettings::load(&p.settings_file),
        Err(_) => settings::PulseSettings::load(std::path::Path::new("")),
    }
}

fn open_settings_page(parent: Hwnd) {
    match profile::PulseProfile::default() {
        Ok(p) => {
            let s = settings::PulseSettings::load(&p.settings_file);
            open_local_page(parent, &local_pages::settings_page_data_url(&s, &p));
        }
        Err(e) => set_status(parent, &format!("Impossible d'ouvrir les parametres: {e}")),
    }
}

#[allow(dead_code)]
fn open_profile_page(parent: Hwnd) {
    match profile::PulseProfile::default() {
        Ok(p) => open_local_page(parent, &local_pages::profile_page_data_url(&p)),
        Err(e) => set_status(parent, &format!("Profil indisponible: {e}")),
    }
}

fn open_about_page(parent: Hwnd) {
    open_local_page(parent, &local_pages::about_page_data_url(APP_VERSION));
}

pub fn open_local_page(parent: Hwnd, page_url: &str) {
    load_local_page(parent, page_url, "Page locale ouverte.");
}

pub fn open_import_page(parent: Hwnd) {
    let sources = bookmarks_import::discover_import_sources();
    open_local_page(parent, &local_pages::import_page_data_url(&sources));
}

fn load_local_page(parent: Hwnd, page_url: &str, ok_status: &str) {
    match cef_runtime::load_url_in_child(parent, page_url) {
        Ok(()) => {
            set_render_area(parent, "");
            set_status(parent, ok_status);
        }
        Err(e) => {
            set_render_area(parent, &format!("Erreur de rendu interne:\r\n{e}"));
            set_status(parent, "Impossible d'ouvrir la page locale.");
        }
    }
}

// ── Commandes de navigation ───────────────────────────────────────────────────

fn handle_nav_button(parent: Hwnd, control_id: i32) {
    match control_id {
        id if id == HOME_BUTTON_ID => open_home_page(parent),
        id if id == FAVORITE_BUTTON_ID => match toggle_current_bookmark() {
            Ok(msg) => {
                ui_bookmarks_bar::refresh(parent);
                layout_controls(parent);
                set_status(parent, &msg);
            }
            Err(e) => set_status(parent, &e),
        },
        id if id == BACK_BUTTON_ID => match cef_runtime::go_back() {
            Ok(s) => set_status(parent, s),
            Err(e) => set_status(parent, &e),
        },
        id if id == FORWARD_BUTTON_ID => match cef_runtime::go_forward() {
            Ok(s) => set_status(parent, s),
            Err(e) => set_status(parent, &e),
        },
        id if id == RELOAD_BUTTON_ID => match cef_runtime::reload() {
            Ok(s) => set_status(parent, s),
            Err(e) => set_status(parent, &e),
        },
        id if id == STOP_BUTTON_ID => match cef_runtime::stop_loading() {
            Ok(s) => set_status(parent, s),
            Err(e) => set_status(parent, &e),
        },
        _ => {}
    }
}

fn toggle_current_bookmark() -> Result<String, String> {
    let page = cef_runtime::current_page()
        .ok_or_else(|| "Ouvre une page web avant de l'ajouter aux favoris.".to_string())?;
    let store = store::open_bookmarks()?;
    if store.toggle_toolbar_url(&page.url, &page.title)? {
        Ok("Favori ajoute dans la barre.".to_string())
    } else {
        Ok("Favori retire.".to_string())
    }
}

// ── Dispatch des commandes menu ───────────────────────────────────────────────

fn dispatch_menu_command(parent: Hwnd, cmd: ui_menu::MenuCommand) {
    match cmd {
        ui_menu::MenuCommand::Home => open_home_page(parent),
        ui_menu::MenuCommand::ToggleFavorite => match toggle_current_bookmark() {
            Ok(msg) => {
                ui_bookmarks_bar::refresh(parent);
                layout_controls(parent);
                set_status(parent, &msg);
            }
            Err(e) => set_status(parent, &e),
        },
        ui_menu::MenuCommand::History => open_history_page(parent),
        ui_menu::MenuCommand::FavoritesPage => open_favorites_page(parent),
        ui_menu::MenuCommand::ImportPage => open_import_page(parent),
        ui_menu::MenuCommand::ToggleFavoritesBar => {
            ui_bookmarks_bar::toggle_visibility();
            ui_bookmarks_bar::refresh(parent);
            layout_controls(parent);
        }
        ui_menu::MenuCommand::Profile => open_settings_page(parent),
        ui_menu::MenuCommand::About => open_about_page(parent),
        ui_menu::MenuCommand::Quit => unsafe { PostQuitMessage(0) },
        ui_menu::MenuCommand::OpenUrl(url) => open_url(parent, &url),
        ui_menu::MenuCommand::OpenFolder(id) => {
            if let Some(cmd) = ui_menu::show_folder_menu(parent, &id) {
                dispatch_menu_command(parent, cmd);
            }
        }
        ui_menu::MenuCommand::RemoveBookmark(id) => {
            match store::open_bookmarks().and_then(|s| s.remove_node(&id)) {
                Ok(_) => {
                    ui_bookmarks_bar::refresh(parent);
                    layout_controls(parent);
                }
                Err(e) => set_status(parent, &e),
            }
        }
        ui_menu::MenuCommand::OpenInNewTab(url) => {
            open_new_tab(parent, &url);
        }
        ui_menu::MenuCommand::RenameBookmark(node_id, current_title) => {
            if let Some(new_title) = win32::show_rename_input_dialog(parent, &current_title) {
                let new_title = new_title.trim().to_string();
                if !new_title.is_empty() {
                    match store::open_bookmarks().and_then(|s| s.rename_node(&node_id, &new_title))
                    {
                        Ok(_) => {
                            ui_bookmarks_bar::refresh(parent);
                            layout_controls(parent);
                            set_status(parent, "Favori renomme.");
                        }
                        Err(e) => set_status(parent, &e),
                    }
                }
            }
        }
    }
}

// ── Import de favoris ─────────────────────────────────────────────────────────

fn import_from_source(parent: Hwnd, index: usize, mode: bookmarks_import::ImportMode) {
    let sources = bookmarks_import::discover_import_sources();
    let Some(source) = sources.into_iter().nth(index) else {
        set_status(parent, "Source d'import introuvable.");
        return;
    };
    match store::open_bookmarks()
        .and_then(|s| bookmarks_import::import_browser_favorites_from_source(&s, &source, mode))
    {
        Ok(report) => {
            ui_bookmarks_bar::refresh(parent);
            layout_controls(parent);
            open_favorites_page(parent);
            set_status(parent, &format_import_report(&report));
        }
        Err(e) => set_status(parent, &format!("Import favoris impossible: {e}")),
    }
}

fn format_import_report(report: &bookmarks_import::BrowserImportReport) -> String {
    match report.mode {
        bookmarks_import::ImportMode::Merge => {
            if report.added == 0 {
                format!(
                    "Import {}: aucun nouveau favori, {} deja presents.",
                    report.source_label, report.skipped
                )
            } else {
                format!(
                    "Import {}: {} favoris et {} dossiers ajoutes.",
                    report.source_label, report.added, report.folders
                )
            }
        }
        bookmarks_import::ImportMode::Replace => {
            let backup = if report.backup_path.is_some() {
                " Sauvegarde locale creee."
            } else {
                ""
            };
            format!(
                "Favoris remplaces depuis {}: {} favoris, {} dossiers.{}",
                report.source_label, report.added, report.folders, backup
            )
        }
    }
}

// ── Actions internes pulse:// ─────────────────────────────────────────────────

fn handle_pulse_action(parent: Hwnd, action: cef_runtime::PulseInternalAction) {
    match action {
        cef_runtime::PulseInternalAction::DeleteBookmark(id) => {
            match store::open_bookmarks().and_then(|s| s.remove_node(&id)) {
                Ok(true) => {
                    ui_bookmarks_bar::refresh(parent);
                    layout_controls(parent);
                    open_favorites_page(parent);
                    set_status(parent, "Favori supprime.");
                }
                Ok(false) => open_favorites_page(parent),
                Err(e) => set_status(parent, &format!("Suppression impossible: {e}")),
            }
        }
        cef_runtime::PulseInternalAction::RenameBookmark(id, title) => {
            match store::open_bookmarks().and_then(|s| s.rename_node(&id, &title)) {
                Ok(true) => {
                    ui_bookmarks_bar::refresh(parent);
                    layout_controls(parent);
                    open_favorites_page(parent);
                    set_status(parent, "Favori renomme.");
                }
                Ok(false) => open_favorites_page(parent),
                Err(e) => set_status(parent, &format!("Renommage impossible: {e}")),
            }
        }
        cef_runtime::PulseInternalAction::AddFolder(parent_id, title) => {
            match store::open_bookmarks().and_then(|s| s.add_folder(&parent_id, &title)) {
                Ok(_) => {
                    ui_bookmarks_bar::refresh(parent);
                    layout_controls(parent);
                    open_favorites_page(parent);
                }
                Err(e) => set_status(parent, &format!("Impossible de creer le dossier: {e}")),
            }
        }
        cef_runtime::PulseInternalAction::ClearBookmarks => {
            match store::open_bookmarks().and_then(|s| s.clear_with_backup()) {
                Ok(summary) => {
                    ui_bookmarks_bar::refresh(parent);
                    layout_controls(parent);
                    open_favorites_page(parent);
                    let backup = if summary.backup_path.is_some() {
                        " Sauvegarde locale creee."
                    } else {
                        ""
                    };
                    set_status(
                        parent,
                        &format!("{} favoris supprimes.{}", summary.removed, backup),
                    );
                }
                Err(e) => set_status(parent, &format!("Vidage impossible: {e}")),
            }
        }
        cef_runtime::PulseInternalAction::ImportFromFile(path) => {
            match import_wizard::import_from_html_file(&path) {
                Ok(report) => {
                    ui_bookmarks_bar::refresh(parent);
                    layout_controls(parent);
                    open_favorites_page(parent);
                    set_status(
                        parent,
                        &format!("Import fichier: {} favoris importes.", report.added),
                    );
                }
                Err(e) => set_status(parent, &format!("Import fichier impossible: {e}")),
            }
        }
        cef_runtime::PulseInternalAction::OpenFilePicker => {
            if let Some(path) = pick_file(
                parent,
                "Importer des favoris",
                "Fichiers HTML\0*.html;*.htm\0Tous les fichiers\0*.*\0\0",
            ) {
                match import_wizard::import_from_html_file(&path) {
                    Ok(report) => {
                        ui_bookmarks_bar::refresh(parent);
                        layout_controls(parent);
                        open_favorites_page(parent);
                        set_status(
                            parent,
                            &format!("Import: {} favoris importes.", report.added),
                        );
                    }
                    Err(e) => set_status(parent, &format!("Import impossible: {e}")),
                }
            }
        }
        cef_runtime::PulseInternalAction::SetSetting(key, val) => {
            match profile::PulseProfile::default() {
                Ok(p) => {
                    let mut s = settings::PulseSettings::load(&p.settings_file);
                    s.apply_key(&key, &val);
                    // Appliquer immédiatement si c'est le layout d'onglets
                    if key == "tab_bar" {
                        let layout = s.tab_bar.to_tab_layout();
                        ui_tabs::set_layout(layout);
                        TABS.with(|tabs| tabs.borrow_mut().set_layout(layout));
                        layout_controls(parent);
                        refresh_tab_bar(parent);
                    }
                    if let Err(e) = s.save(&p.settings_file) {
                        set_status(parent, &format!("Erreur parametres: {e}"));
                    }
                    open_local_page(parent, &local_pages::settings_page_data_url(&s, &p));
                }
                Err(e) => set_status(parent, &format!("Profil indisponible: {e}")),
            }
        }
        cef_runtime::PulseInternalAction::ClearHistory => {
            match history::clear_all() {
                Ok(n) => {
                    set_status(parent, &format!("{n} entrees supprimees."));
                }
                Err(e) => set_status(parent, &format!("Impossible d'effacer l'historique: {e}")),
            }
            open_settings_page(parent);
        }
        cef_runtime::PulseInternalAction::SkipImport => {
            open_home_page(parent);
        }
        cef_runtime::PulseInternalAction::ImportFromBrowser(index) => {
            import_from_source(parent, index, bookmarks_import::ImportMode::Merge);
        }
        cef_runtime::PulseInternalAction::ReplaceFromBrowser(index) => {
            import_from_source(parent, index, bookmarks_import::ImportMode::Replace);
        }
    }
}

// ── Barre d'adresse ───────────────────────────────────────────────────────────

fn open_address_from_bar(parent: Hwnd) {
    let Some(raw) = get_control_text(parent, ADDRESS_CONTROL_ID) else {
        set_status(parent, "Impossible de lire la barre d'adresse.");
        return;
    };
    let Some(url) = normalize_address(&raw) else {
        set_status(parent, "Entre une adresse avant d'ouvrir.");
        return;
    };
    if url == local_pages::HOME_ADDRESS {
        open_home_page(parent);
        return;
    }
    open_url(parent, &url);
}

fn handle_address_bar_enter(msg: &Msg) -> bool {
    if msg.message != WM_KEYDOWN || msg.w_param != VK_RETURN {
        return false;
    }
    let parent = unsafe { GetParent(msg.hwnd) };
    if parent.is_null() {
        return false;
    }
    let addr_ctrl = unsafe { GetDlgItem(parent, ADDRESS_CONTROL_ID) };
    if addr_ctrl != msg.hwnd {
        return false;
    }
    open_address_from_bar(parent);
    true
}

fn normalize_address(address: &str) -> Option<String> {
    let address = address.trim();
    if address.is_empty() {
        return None;
    }
    if address.eq_ignore_ascii_case(local_pages::HOME_ADDRESS) {
        return Some(local_pages::HOME_ADDRESS.to_string());
    }
    if address.contains("://") {
        return Some(address.to_string());
    }
    // Domain-like: no spaces, has a dot → add https://
    if !address.contains(' ') && address.contains('.') {
        return Some(format!("https://{address}"));
    }
    // Otherwise treat as a search query
    Some(load_settings().search_url(address))
}

// ── Mise à jour UI depuis CEF ─────────────────────────────────────────────────

fn apply_browser_ui_update(parent: Hwnd, update: cef_runtime::BrowserUiUpdate) {
    if let Some(address) = update.address {
        set_control_text(parent, ADDRESS_CONTROL_ID, &address);
    }
    if let Some(status) = update.status {
        set_status(parent, &status);
    }
    if let Some(can_go_back) = update.can_go_back {
        set_control_enabled(parent, BACK_BUTTON_ID, can_go_back);
    }
    if let Some(can_go_forward) = update.can_go_forward {
        set_control_enabled(parent, FORWARD_BUTTON_ID, can_go_forward);
    }
    if let Some(is_loading) = update.is_loading {
        set_control_enabled(parent, STOP_BUTTON_ID, is_loading);
    }
    // Titre de l'onglet actif → mettre à jour le bouton dans la barre d'onglets
    if let Some(title) = update.active_tab_title {
        if let Some(tab_raw) = cef_runtime::active_tab_id() {
            let tid = tabs::TabId(tab_raw);
            TABS.with(|tabs| {
                tabs.borrow_mut().update_tab_title(tid, &title);
            });
            refresh_tab_bar(parent);
        }
    }
    // URL de l'onglet actif → mettre à jour le store interne
    if let Some(url) = update.active_tab_url {
        if let Some(tab_raw) = cef_runtime::active_tab_id() {
            let tid = tabs::TabId(tab_raw);
            TABS.with(|tabs| {
                tabs.borrow_mut().update_tab_url(tid, &url);
            });
        }
    }
}

// ── Raccourcis affichage ──────────────────────────────────────────────────────

pub fn set_status(parent: Hwnd, text: &str) {
    set_control_text(parent, STATUS_CONTROL_ID, text);
}

fn set_render_area(parent: Hwnd, text: &str) {
    set_control_text(parent, RENDER_AREA_CONTROL_ID, text);
}

// ── Création de contrôles Win32 ───────────────────────────────────────────────

fn make_button(
    parent: Hwnd,
    instance: Hinstance,
    id: i32,
    x: i32,
    y: i32,
    w: i32,
    h: i32,
    text: &str,
) {
    make_control(
        "BUTTON",
        text,
        WS_CHILD | WS_VISIBLE,
        0,
        parent,
        instance,
        id,
        x,
        y,
        w,
        h,
    );
}

fn make_edit(
    parent: Hwnd,
    instance: Hinstance,
    id: i32,
    x: i32,
    y: i32,
    w: i32,
    h: i32,
    text: &str,
) {
    make_control(
        "EDIT",
        text,
        WS_CHILD | WS_VISIBLE | WS_BORDER | ES_LEFT,
        WS_EX_CLIENTEDGE,
        parent,
        instance,
        id,
        x,
        y,
        w,
        h,
    );
}

fn make_label(
    parent: Hwnd,
    instance: Hinstance,
    id: i32,
    x: i32,
    y: i32,
    w: i32,
    h: i32,
    text: &str,
) {
    make_control(
        "STATIC",
        text,
        WS_CHILD | WS_VISIBLE,
        0,
        parent,
        instance,
        id,
        x,
        y,
        w,
        h,
    );
}

fn make_control(
    class: &str,
    text: &str,
    style: u32,
    ex_style: u32,
    parent: Hwnd,
    instance: Hinstance,
    id: i32,
    x: i32,
    y: i32,
    w: i32,
    h: i32,
) {
    let class = wide(class);
    let text = wide(text);
    unsafe {
        CreateWindowExW(
            ex_style,
            class.as_ptr(),
            text.as_ptr(),
            style,
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

// ── Tests ─────────────────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rejects_empty_address() {
        assert_eq!(normalize_address(""), None);
        assert_eq!(normalize_address("   "), None);
    }

    #[test]
    fn prefixes_https_when_scheme_is_missing() {
        assert_eq!(
            normalize_address("example.com"),
            Some("https://example.com".to_string())
        );
    }

    #[test]
    fn keeps_existing_scheme() {
        assert_eq!(
            normalize_address("ftp://example.com"),
            Some("ftp://example.com".to_string())
        );
    }

    #[test]
    fn keeps_internal_home_address() {
        assert_eq!(
            normalize_address(local_pages::HOME_ADDRESS),
            Some(local_pages::HOME_ADDRESS.to_string())
        );
    }
}
