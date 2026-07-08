use std::cell::RefCell;
use std::collections::HashMap;
use std::path::PathBuf;
use std::sync::atomic::{AtomicI32, Ordering};
use std::sync::{Mutex, OnceLock};

use crate::{
    browser_data::{self, BrowserDataStore},
    credentials, local_pages, privacy,
    profile::PulseProfile,
    vault,
};
use cef::rc::Rc;
use cef::{
    App, Browser, BrowserSettings, CefString, CefStringUtf16, Client, CommandLine, Cookie,
    CookieAccessFilter, DisplayHandler, Errorcode, Frame, ImplApp, ImplBrowser, ImplBrowserHost,
    ImplClient, ImplCommandLine, ImplCookieAccessFilter, ImplDisplayHandler, ImplFrame,
    ImplLoadHandler, ImplPostData, ImplPostDataElement, ImplRequest, ImplRequestHandler,
    ImplResourceRequestHandler, LoadHandler, PostData, PostdataelementType, Rect, Request,
    RequestHandler, ResourceRequestHandler, ResourceType, Response, ReturnValue, RuntimeStyle,
    Settings, TransitionType, UrlrequestStatus, WindowInfo, WrapApp, WrapClient,
    WrapCookieAccessFilter, WrapDisplayHandler, WrapLoadHandler, WrapRequestHandler,
    WrapResourceRequestHandler, api_hash, args::Args, browser_host_create_browser_sync,
    do_message_loop_work, execute_process, initialize, shutdown, wrap_app, wrap_client,
    wrap_cookie_access_filter, wrap_display_handler, wrap_load_handler, wrap_request_handler,
    wrap_resource_request_handler,
};

#[link(name = "user32")]
unsafe extern "system" {
    fn SetWindowPos(
        hwnd: *mut std::ffi::c_void,
        insert_after: *mut std::ffi::c_void,
        x: i32,
        y: i32,
        width: i32,
        height: i32,
        flags: u32,
    ) -> i32;
    fn ShowWindow(hwnd: *mut std::ffi::c_void, command_show: i32) -> i32;
}

const SWP_SHOWWINDOW: u32 = 0x0040;
const SW_HIDE: i32 = 0;
const MAX_CAPTURED_LOGIN_POST_BYTES: usize = 64 * 1024;

// Identifiant CEF du browser actif (-1 = aucun). Écrit depuis le thread principal.
static ACTIVE_CEF_BROWSER_ID: AtomicI32 = AtomicI32::new(-1);

static UI_UPDATE: OnceLock<Mutex<Option<BrowserUiUpdate>>> = OnceLock::new();
static PULSE_ACTION: OnceLock<Mutex<Option<PulseInternalAction>>> = OnceLock::new();
static VAULT_FILE: OnceLock<PathBuf> = OnceLock::new();
static BROWSER_DATA: OnceLock<BrowserDataStore> = OnceLock::new();
static CURRENT_PAGE: OnceLock<Mutex<CurrentPage>> = OnceLock::new();

// ── Types publics ─────────────────────────────────────────────────────────────

#[derive(Clone, Debug)]
pub enum PulseInternalAction {
    DeleteBookmark(String),
    RenameBookmark(String, String),
    AddFolder(String, String),
    ClearBookmarks,
    ImportFromFile(String),
    ImportFromBrowser(usize),
    ReplaceFromBrowser(usize),
    OpenFilePicker,
    SkipImport,
    SetSetting(String, String),
    ClearHistory,
}

#[derive(Clone, Debug, Default)]
pub struct BrowserUiUpdate {
    pub status: Option<String>,
    pub address: Option<String>,
    pub can_go_back: Option<bool>,
    pub can_go_forward: Option<bool>,
    pub is_loading: Option<bool>,
    pub active_tab_title: Option<String>,
    pub active_tab_url: Option<String>,
}

pub struct SwitchTabInfo {
    pub url: String,
    pub can_go_back: bool,
    pub can_go_forward: bool,
    pub is_loading: bool,
}

pub enum CloseTabResult {
    WasInactive,
    WasActive { next_tab_id: Option<u64> },
}

#[derive(Clone, Debug, Default)]
struct CurrentPage {
    url: String,
    title: String,
}

#[derive(Clone, Debug, Default)]
pub struct CurrentBrowserPage {
    pub url: String,
    pub title: String,
}

// ── État CEF interne ──────────────────────────────────────────────────────────

struct TabBrowserEntry {
    browser: Browser,
    client: Client,
    request_handler: RequestHandler,
}

struct CefState {
    initialized: bool,
    app: Option<App>,
    // Onglet actif
    browser: Option<Browser>,
    client: Option<Client>,
    request_handler: Option<RequestHandler>,
    active_tab_id: Option<u64>,
    // Onglets inactifs (cachés)
    inactive_browsers: HashMap<u64, TabBrowserEntry>,
    next_tab_id: u64,
}

thread_local! {
    static CEF_STATE: RefCell<CefState> = RefCell::new(CefState {
        initialized: false,
        app: None,
        browser: None,
        client: None,
        request_handler: None,
        active_tab_id: None,
        inactive_browsers: HashMap::new(),
        next_tab_id: 1,
    });
}

// ── App CEF ───────────────────────────────────────────────────────────────────

wrap_app! {
    struct PulseBrowserApp;

    impl App {
        fn on_before_command_line_processing(
            &self,
            process_type: Option<&CefString>,
            command_line: Option<&mut CommandLine>,
        ) {
            let _ = process_type;
            let Some(command_line) = command_line else {
                return;
            };

            append_switch(command_line, "disable-gpu");
            append_switch(command_line, "disable-gpu-compositing");
            append_switch(command_line, "disable-gpu-rasterization");
            append_switch(command_line, "disable-gpu-watchdog");
            append_switch(command_line, "persist-session-cookies");
        }
    }
}

// ── Politique cookies ─────────────────────────────────────────────────────────

wrap_cookie_access_filter! {
    struct PulseCookieAccessFilter;

    impl CookieAccessFilter {
        fn can_send_cookie(
            &self,
            browser: Option<&mut Browser>,
            frame: Option<&mut Frame>,
            request: Option<&mut Request>,
            cookie: Option<&Cookie>,
        ) -> i32 {
            let _ = browser;
            let _ = frame;
            let _ = cookie;
            cookie_access_allowed(request)
        }

        fn can_save_cookie(
            &self,
            browser: Option<&mut Browser>,
            frame: Option<&mut Frame>,
            request: Option<&mut Request>,
            response: Option<&mut Response>,
            cookie: Option<&Cookie>,
        ) -> i32 {
            let _ = browser;
            let _ = frame;
            let _ = response;
            let _ = cookie;
            cookie_access_allowed(request)
        }
    }
}

wrap_resource_request_handler! {
    struct PulseResourceRequestHandler;

    impl ResourceRequestHandler {
        fn cookie_access_filter(
            &self,
            browser: Option<&mut Browser>,
            frame: Option<&mut Frame>,
            request: Option<&mut Request>,
        ) -> Option<CookieAccessFilter> {
            let _ = browser;
            let _ = frame;
            let _ = request;
            Some(PulseCookieAccessFilter::new())
        }

        fn on_before_resource_load(
            &self,
            browser: Option<&mut Browser>,
            frame: Option<&mut Frame>,
            request: Option<&mut Request>,
            callback: Option<&mut cef::Callback>,
        ) -> ReturnValue {
            let _ = browser;
            let _ = frame;
            let _ = request;
            let _ = callback;
            ReturnValue::CONTINUE
        }

        fn on_resource_load_complete(
            &self,
            browser: Option<&mut Browser>,
            frame: Option<&mut Frame>,
            request: Option<&mut Request>,
            response: Option<&mut Response>,
            status: UrlrequestStatus,
            received_content_length: i64,
        ) {
            let _ = browser;
            let _ = frame;
            let _ = request;
            let _ = response;
            let _ = status;
            let _ = received_content_length;
        }
    }
}

// ── Request handler ───────────────────────────────────────────────────────────

wrap_request_handler! {
    struct PulseRequestHandler;

    impl RequestHandler {
        fn on_before_browse(
            &self,
            browser: Option<&mut Browser>,
            frame: Option<&mut Frame>,
            request: Option<&mut Request>,
            user_gesture: i32,
            is_redirect: i32,
        ) -> i32 {
            let _ = browser;
            let _ = frame;
            let _ = user_gesture;
            let _ = is_redirect;
            let Some(request) = request else { return 0; };
            let url = cef_userfree_to_string(&request.url());
            if let Some(action) = parse_pulse_action(&url) {
                queue_pulse_action(action);
                return 1;
            }
            0
        }

        fn resource_request_handler(
            &self,
            browser: Option<&mut Browser>,
            frame: Option<&mut Frame>,
            request: Option<&mut Request>,
            is_navigation: i32,
            is_download: i32,
            request_initiator: Option<&CefString>,
            disable_default_handling: Option<&mut i32>,
        ) -> Option<ResourceRequestHandler> {
            let _ = browser;
            let _ = frame;
            let _ = is_navigation;
            let _ = is_download;
            let _ = request_initiator;
            let _ = disable_default_handling;
            if let Some(request) = request {
                maybe_store_login_from_request(request);
            }
            Some(PulseResourceRequestHandler::new())
        }
    }
}

// ── Load handler ──────────────────────────────────────────────────────────────

wrap_load_handler! {
    struct PulseLoadHandler;

    impl LoadHandler {
        fn on_loading_state_change(
            &self,
            browser: Option<&mut Browser>,
            is_loading: i32,
            can_go_back: i32,
            can_go_forward: i32,
        ) {
            let cef_id = browser.as_deref().map(|b| b.identifier()).unwrap_or(-1);
            if !is_active_browser_id(cef_id) {
                return;
            }
            publish_navigation_state(can_go_back == 1, can_go_forward == 1, is_loading == 1);
            if is_loading == 1 {
                publish_status("Chargement en cours...");
            } else {
                publish_status("Chargement termine.");
            }
        }

        fn on_load_start(
            &self,
            browser: Option<&mut Browser>,
            frame: Option<&mut Frame>,
            transition_type: TransitionType,
        ) {
            let cef_id = browser.as_deref().map(|b| b.identifier()).unwrap_or(-1);
            let _ = frame;
            let _ = transition_type;
            if !is_active_browser_id(cef_id) {
                return;
            }
            publish_status("Demarrage du chargement...");
        }

        fn on_load_end(
            &self,
            browser: Option<&mut Browser>,
            frame: Option<&mut Frame>,
            http_status_code: i32,
        ) {
            let cef_id = browser.as_deref().map(|b| b.identifier()).unwrap_or(-1);
            // injection + historique pour tous les onglets
            if let Some(frame) = frame {
                maybe_inject_saved_login(frame);
                maybe_record_history_visit(frame);
            }
            if is_active_browser_id(cef_id) {
                publish_status(&format!("Chargement termine. HTTP {http_status_code}."));
            }
        }

        fn on_load_error(
            &self,
            browser: Option<&mut Browser>,
            frame: Option<&mut Frame>,
            error_code: Errorcode,
            error_text: Option<&CefString>,
            failed_url: Option<&CefString>,
        ) {
            let cef_id = browser.as_deref().map(|b| b.identifier()).unwrap_or(-1);

            let text = error_text
                .map(ToString::to_string)
                .unwrap_or_else(|| "erreur inconnue".to_string());
            let url = failed_url
                .map(ToString::to_string)
                .unwrap_or_else(|| "URL inconnue".to_string());

            // page d'erreur injectée dans tous les onglets
            if error_code.get_raw() != -3 {
                if let Some(frame) = frame {
                    maybe_load_error_page(frame, &url, error_code.get_raw(), &text);
                }
            }

            if is_active_browser_id(cef_id) {
                publish_status(&format!(
                    "Erreur de chargement ({}) sur {}: {text}",
                    error_code.get_raw(),
                    privacy::display_host(&url)
                ));
            }
        }
    }
}

// ── Display handler ───────────────────────────────────────────────────────────

wrap_display_handler! {
    struct PulseDisplayHandler;

    impl DisplayHandler {
        fn on_address_change(
            &self,
            browser: Option<&mut Browser>,
            frame: Option<&mut Frame>,
            url: Option<&CefString>,
        ) {
            let cef_id = browser.as_deref().map(|b| b.identifier()).unwrap_or(-1);
            let _ = frame;

            if let Some(url) = url {
                let url = url.to_string();
                if is_active_browser_id(cef_id) {
                    update_current_url(&url);
                    if browser_data::is_web_url(&url) {
                        merge_ui_update(BrowserUiUpdate {
                            address: Some(url.clone()),
                            active_tab_url: Some(url.clone()),
                            ..BrowserUiUpdate::default()
                        });
                        publish_status(&format!(
                            "Adresse chargee: {}",
                            privacy::display_host(&url)
                        ));
                    }
                }
            }
        }

        fn on_title_change(&self, browser: Option<&mut Browser>, title: Option<&CefString>) {
            let cef_id = browser.as_deref().map(|b| b.identifier()).unwrap_or(-1);

            if let Some(title) = title {
                let title = title.to_string();
                if is_active_browser_id(cef_id) {
                    update_current_title(&title);
                    merge_ui_update(BrowserUiUpdate {
                        active_tab_title: Some(title.clone()),
                        status: Some(format!("Page chargee: {title}")),
                        ..BrowserUiUpdate::default()
                    });
                }
            }
        }
    }
}

// ── Client CEF ────────────────────────────────────────────────────────────────

wrap_client! {
    struct PulseBrowserClient {
        load_handler: LoadHandler,
        display_handler: DisplayHandler,
        request_handler: RequestHandler,
    }

    impl Client {
        fn load_handler(&self) -> Option<LoadHandler> {
            Some(self.load_handler.clone())
        }

        fn display_handler(&self) -> Option<DisplayHandler> {
            Some(self.display_handler.clone())
        }

        fn request_handler(&self) -> Option<RequestHandler> {
            Some(self.request_handler.clone())
        }
    }
}

// ── Initialisation CEF ────────────────────────────────────────────────────────

pub fn handle_subprocess_or_initialize() -> Result<(), String> {
    CEF_STATE.with(|state| {
        if state.borrow().initialized {
            return Ok(());
        }

        let _ = api_hash(cef::sys::CEF_API_VERSION_LAST, 0);

        let args = Args::new();
        let Some(command_line) = args.as_cmd_line() else {
            return Err("CEF: impossible de lire la ligne de commande.".to_string());
        };

        let process_type = CefString::from("type");
        let is_browser_process = command_line.has_switch(Some(&process_type)) != 1;
        let mut app = PulseBrowserApp::new();
        let exit_code = execute_process(
            Some(args.as_main_args()),
            Some(&mut app),
            std::ptr::null_mut(),
        );

        if !is_browser_process {
            std::process::exit(exit_code.max(0));
        }

        if exit_code != -1 {
            return Err(format!(
                "CEF: processus navigateur inattendu ({exit_code})."
            ));
        }

        let profile = PulseProfile::default()?;
        let browser_data =
            BrowserDataStore::new(profile.history_file.clone(), profile.favorites_file.clone());
        browser_data.ensure_files()?;
        let _ = BROWSER_DATA.set(browser_data);
        vault::ensure_transparent_vault(&profile.vault_file)?;
        let _ = vault::load_vault(&profile.vault_file)?.credentials().len();
        let _ = VAULT_FILE.set(profile.vault_file.clone());

        let root_cache_path = crate::profile::path_text(&profile.root_cache_dir);
        let cache_path = crate::profile::path_text(&profile.cef_cache_dir);

        let settings = Settings {
            no_sandbox: 1,
            external_message_pump: 1,
            root_cache_path: CefString::from(root_cache_path.as_str()),
            cache_path: CefString::from(cache_path.as_str()),
            persist_session_cookies: 1,
            ..Default::default()
        };

        if initialize(
            Some(args.as_main_args()),
            Some(&settings),
            Some(&mut app),
            std::ptr::null_mut(),
        ) != 1
        {
            return Err("CEF: initialisation echouee.".to_string());
        }

        let mut state = state.borrow_mut();
        state.app = Some(app);
        state.initialized = true;
        publish_status("Profil local transparent initialise.");
        Ok(())
    })
}

// ── Gestion des onglets ───────────────────────────────────────────────────────

/// Alloue un identifiant d'onglet unique.
pub fn allocate_tab_id() -> u64 {
    CEF_STATE.with(|state| {
        let mut state = state.borrow_mut();
        let id = state.next_tab_id;
        state.next_tab_id += 1;
        id
    })
}

/// Retourne l'identifiant interne de l'onglet actif.
pub fn active_tab_id() -> Option<u64> {
    CEF_STATE.with(|state| state.borrow().active_tab_id)
}

/// Crée un nouvel onglet CEF avec l'URL donnée et en fait l'onglet actif.
pub fn create_tab(
    parent: *mut std::ffi::c_void,
    url: &str,
    tab_id: u64,
    x: i32,
    y: i32,
    w: i32,
    h: i32,
) -> Result<(), String> {
    CEF_STATE.with(|state| {
        let mut state = state.borrow_mut();

        if !state.initialized {
            return Err("CEF: moteur non initialise.".to_string());
        }

        // Masquer et remiser l'onglet actif
        if let Some(current_browser) = state.browser.take() {
            hide_browser_window(&current_browser);
            if let Some(old_id) = state.active_tab_id.take() {
                let client = state.client.take();
                let rh = state.request_handler.take();
                if let (Some(client), Some(rh)) = (client, rh) {
                    state.inactive_browsers.insert(
                        old_id,
                        TabBrowserEntry {
                            browser: current_browser,
                            client,
                            request_handler: rh,
                        },
                    );
                }
            }
        }

        // Créer le nouveau navigateur CEF
        let bounds = Rect {
            x,
            y,
            width: w,
            height: h,
        };
        let parent_hwnd = cef::sys::HWND(parent.cast());
        let window_info = WindowInfo {
            runtime_style: RuntimeStyle::ALLOY,
            ..WindowInfo::default().set_as_child(parent_hwnd, &bounds)
        };
        let url_cef = CefString::from(url);
        let settings = BrowserSettings::default();
        let request_handler = PulseRequestHandler::new();
        let mut client = PulseBrowserClient::new(
            PulseLoadHandler::new(),
            PulseDisplayHandler::new(),
            request_handler.clone(),
        );
        let browser = browser_host_create_browser_sync(
            Some(&window_info),
            Some(&mut client),
            Some(&url_cef),
            Some(&settings),
            None,
            None,
        )
        .ok_or_else(|| "CEF: impossible de creer le nouvel onglet.".to_string())?;

        ACTIVE_CEF_BROWSER_ID.store(browser.identifier(), Ordering::Relaxed);

        state.client = Some(client);
        state.request_handler = Some(request_handler);
        state.active_tab_id = Some(tab_id);
        state.browser = Some(browser);

        if let Some(b) = state.browser.as_ref() {
            focus_browser(b);
        }

        Ok(())
    })
}

/// Bascule vers un onglet existant (inactif) et retourne son état de navigation.
pub fn switch_tab(tab_id: u64, x: i32, y: i32, w: i32, h: i32) -> Result<SwitchTabInfo, String> {
    CEF_STATE.with(|state| {
        let mut state = state.borrow_mut();

        // Déjà actif : retourner juste l'état courant
        if state.active_tab_id == Some(tab_id) {
            let browser = state
                .browser
                .as_ref()
                .ok_or_else(|| "Aucun navigateur actif.".to_string())?;
            let url = browser
                .main_frame()
                .map(|f| cef_userfree_to_string(&f.url()))
                .unwrap_or_default();
            return Ok(SwitchTabInfo {
                url,
                can_go_back: browser.can_go_back() == 1,
                can_go_forward: browser.can_go_forward() == 1,
                is_loading: browser.is_loading() == 1,
            });
        }

        // Remiser l'onglet actif
        if let Some(current_browser) = state.browser.take() {
            hide_browser_window(&current_browser);
            if let Some(old_id) = state.active_tab_id.take() {
                let client = state.client.take();
                let rh = state.request_handler.take();
                if let (Some(client), Some(rh)) = (client, rh) {
                    state.inactive_browsers.insert(
                        old_id,
                        TabBrowserEntry {
                            browser: current_browser,
                            client,
                            request_handler: rh,
                        },
                    );
                }
            }
        }

        // Activer le nouvel onglet
        let entry = state
            .inactive_browsers
            .remove(&tab_id)
            .ok_or_else(|| format!("Onglet {tab_id} introuvable."))?;

        // Repositionner et afficher
        if let Some(host) = entry.browser.host() {
            let handle: *mut std::ffi::c_void = host.window_handle().0.cast();
            if !handle.is_null() {
                unsafe {
                    SetWindowPos(handle, std::ptr::null_mut(), x, y, w, h, SWP_SHOWWINDOW);
                }
            }
            host.was_resized();
            host.set_focus(1);
        }

        ACTIVE_CEF_BROWSER_ID.store(entry.browser.identifier(), Ordering::Relaxed);

        let url = entry
            .browser
            .main_frame()
            .map(|f| cef_userfree_to_string(&f.url()))
            .unwrap_or_default();
        let can_go_back = entry.browser.can_go_back() == 1;
        let can_go_forward = entry.browser.can_go_forward() == 1;
        let is_loading = entry.browser.is_loading() == 1;

        state.client = Some(entry.client);
        state.request_handler = Some(entry.request_handler);
        state.browser = Some(entry.browser);
        state.active_tab_id = Some(tab_id);

        if let Some(b) = state.browser.as_ref() {
            focus_browser(b);
        }

        Ok(SwitchTabInfo {
            url,
            can_go_back,
            can_go_forward,
            is_loading,
        })
    })
}

/// Ferme un onglet. Si c'était l'onglet actif, indique le prochain onglet à activer.
pub fn close_tab(tab_id: u64) -> CloseTabResult {
    CEF_STATE.with(|state| {
        let mut state = state.borrow_mut();

        if state.active_tab_id == Some(tab_id) {
            // Chercher un autre onglet à activer
            let next_id = state.inactive_browsers.keys().next().copied();

            if let Some(browser) = state.browser.take() {
                if let Some(host) = browser.host() {
                    host.close_browser(1);
                }
            }
            state.client = None;
            state.request_handler = None;
            state.active_tab_id = None;
            ACTIVE_CEF_BROWSER_ID.store(-1, Ordering::Relaxed);

            CloseTabResult::WasActive {
                next_tab_id: next_id,
            }
        } else {
            if let Some(entry) = state.inactive_browsers.remove(&tab_id) {
                if let Some(host) = entry.browser.host() {
                    host.close_browser(1);
                }
            }
            CloseTabResult::WasInactive
        }
    })
}

// ── Navigation dans l'onglet actif ───────────────────────────────────────────

pub fn load_url_in_child(parent: *mut std::ffi::c_void, url: &str) -> Result<(), String> {
    CEF_STATE.with(|state| {
        let mut state = state.borrow_mut();

        if !state.initialized {
            return Err("CEF: moteur non initialise.".to_string());
        }

        let url_cef = CefString::from(url);

        if let Some(browser) = state.browser.as_ref() {
            if let Some(frame) = browser.main_frame() {
                frame.load_url(Some(&url_cef));
                focus_browser(browser);
                return Ok(());
            }
        }

        // Pas d'onglet actif : créer le premier onglet avec des limites initiales
        let bounds = Rect {
            x: 8,
            y: 74,
            width: 900,
            height: 400,
        };
        let parent_hwnd = cef::sys::HWND(parent.cast());
        let window_info = WindowInfo {
            runtime_style: RuntimeStyle::ALLOY,
            ..WindowInfo::default().set_as_child(parent_hwnd, &bounds)
        };
        let settings = BrowserSettings::default();
        let request_handler = PulseRequestHandler::new();
        let mut client = PulseBrowserClient::new(
            PulseLoadHandler::new(),
            PulseDisplayHandler::new(),
            request_handler.clone(),
        );
        let browser = browser_host_create_browser_sync(
            Some(&window_info),
            Some(&mut client),
            Some(&url_cef),
            Some(&settings),
            None,
            None,
        )
        .ok_or_else(|| "CEF: impossible de creer le navigateur embarque.".to_string())?;

        ACTIVE_CEF_BROWSER_ID.store(browser.identifier(), Ordering::Relaxed);

        let tab_id = state.next_tab_id;
        state.next_tab_id += 1;
        state.client = Some(client);
        state.request_handler = Some(request_handler);
        state.active_tab_id = Some(tab_id);
        state.browser = Some(browser);

        if let Some(b) = state.browser.as_ref() {
            focus_browser(b);
        }
        Ok(())
    })
}

pub fn do_message_loop_work_if_needed() {
    CEF_STATE.with(|state| {
        if state.borrow().initialized {
            do_message_loop_work();
        }
    });
}

pub fn shutdown_if_needed() {
    CEF_STATE.with(|state| {
        let mut state = state.borrow_mut();

        // Fermer tous les onglets inactifs
        let ids: Vec<u64> = state.inactive_browsers.keys().copied().collect();
        for id in ids {
            if let Some(entry) = state.inactive_browsers.remove(&id) {
                if let Some(host) = entry.browser.host() {
                    host.close_browser(1);
                }
            }
        }

        state.browser = None;
        state.client = None;
        state.request_handler = None;
        state.app = None;

        if state.initialized {
            shutdown();
            state.initialized = false;
        }
    });
}

pub fn resize_browser(x: i32, y: i32, width: i32, height: i32) -> bool {
    CEF_STATE.with(|state| {
        let state = state.borrow();
        let Some(browser) = state.browser.as_ref() else {
            return false;
        };
        let Some(host) = browser.host() else {
            return false;
        };

        let handle: *mut std::ffi::c_void = host.window_handle().0.cast();
        if handle.is_null() {
            return false;
        }

        unsafe {
            SetWindowPos(
                handle,
                std::ptr::null_mut(),
                x,
                y,
                width,
                height,
                SWP_SHOWWINDOW,
            );
        }
        host.was_resized();
        host.set_focus(1);
        true
    })
}

pub fn go_back() -> Result<&'static str, String> {
    with_browser(
        "CEF: aucune page active pour revenir en arriere.",
        |browser| {
            if browser.can_go_back() == 1 {
                browser.go_back();
                focus_browser(browser);
                Ok("Retour demande.")
            } else {
                Ok("Aucune page precedente.")
            }
        },
    )
}

pub fn go_forward() -> Result<&'static str, String> {
    with_browser("CEF: aucune page active pour avancer.", |browser| {
        if browser.can_go_forward() == 1 {
            browser.go_forward();
            focus_browser(browser);
            Ok("Avance demandee.")
        } else {
            Ok("Aucune page suivante.")
        }
    })
}

pub fn reload() -> Result<&'static str, String> {
    with_browser("CEF: aucune page active a recharger.", |browser| {
        browser.reload();
        focus_browser(browser);
        Ok("Rechargement demande.")
    })
}

pub fn stop_loading() -> Result<&'static str, String> {
    with_browser("CEF: aucune page active a arreter.", |browser| {
        if browser.is_loading() == 1 {
            browser.stop_load();
            focus_browser(browser);
            Ok("Chargement arrete.")
        } else {
            Ok("Aucun chargement en cours.")
        }
    })
}

pub fn take_pulse_action() -> Option<PulseInternalAction> {
    PULSE_ACTION
        .get_or_init(|| Mutex::new(None))
        .lock()
        .ok()
        .and_then(|mut action| action.take())
}

pub fn take_ui_update() -> Option<BrowserUiUpdate> {
    UI_UPDATE
        .get_or_init(|| Mutex::new(None))
        .lock()
        .ok()
        .and_then(|mut update| update.take())
}

pub fn current_page() -> Option<CurrentBrowserPage> {
    let page = CURRENT_PAGE
        .get_or_init(|| Mutex::new(CurrentPage::default()))
        .lock()
        .ok()?
        .clone();
    if !browser_data::is_web_url(&page.url) {
        return None;
    }
    Some(CurrentBrowserPage {
        url: page.url,
        title: page.title,
    })
}

#[allow(dead_code)]
pub fn toggle_favorite_current_page() -> Result<String, String> {
    let Some(store) = BROWSER_DATA.get() else {
        return Err("favoris locaux indisponibles.".to_string());
    };

    let page = CURRENT_PAGE
        .get_or_init(|| Mutex::new(CurrentPage::default()))
        .lock()
        .map_err(|_| "etat de page inaccessible.".to_string())?
        .clone();

    if !browser_data::is_web_url(&page.url) {
        return Err("ouvre une page web avant de l'ajouter aux favoris.".to_string());
    }

    let added = store.toggle_favorite(&page.url, &page.title)?;
    if added {
        Ok("Favori local ajoute.".to_string())
    } else {
        Ok("Favori local retire.".to_string())
    }
}

// ── Helpers internes ──────────────────────────────────────────────────────────

fn is_active_browser_id(cef_id: i32) -> bool {
    cef_id != -1 && ACTIVE_CEF_BROWSER_ID.load(Ordering::Relaxed) == cef_id
}

fn hide_browser_window(browser: &Browser) {
    if let Some(host) = browser.host() {
        let handle: *mut std::ffi::c_void = host.window_handle().0.cast();
        if !handle.is_null() {
            unsafe {
                ShowWindow(handle, SW_HIDE);
            }
        }
    }
}

fn percent_decode(s: &str) -> String {
    let bytes = s.as_bytes();
    let mut out = Vec::with_capacity(bytes.len());
    let mut i = 0;
    while i < bytes.len() {
        if bytes[i] == b'%' && i + 2 < bytes.len() {
            if let (Some(hi), Some(lo)) = (hex_val(bytes[i + 1]), hex_val(bytes[i + 2])) {
                out.push((hi << 4) | lo);
                i += 3;
                continue;
            }
        }
        out.push(bytes[i]);
        i += 1;
    }
    String::from_utf8_lossy(&out).into_owned()
}

fn hex_val(b: u8) -> Option<u8> {
    match b {
        b'0'..=b'9' => Some(b - b'0'),
        b'a'..=b'f' => Some(b - b'a' + 10),
        b'A'..=b'F' => Some(b - b'A' + 10),
        _ => None,
    }
}

fn parse_pulse_action(url: &str) -> Option<PulseInternalAction> {
    if let Some(path) = url.strip_prefix("pulse://bookmarks/") {
        if path == "clear" {
            return Some(PulseInternalAction::ClearBookmarks);
        }
        if let Some(id) = path.strip_prefix("delete/") {
            if !id.is_empty() {
                return Some(PulseInternalAction::DeleteBookmark(id.to_string()));
            }
        }
        if let Some(rest) = path.strip_prefix("rename/") {
            if let Some(slash) = rest.find('/') {
                let id = &rest[..slash];
                let encoded_title = &rest[slash + 1..];
                if !id.is_empty() {
                    return Some(PulseInternalAction::RenameBookmark(
                        id.to_string(),
                        percent_decode(encoded_title),
                    ));
                }
            }
        }
        if let Some(rest) = path.strip_prefix("add-folder/") {
            if let Some(slash) = rest.find('/') {
                let parent_id = &rest[..slash];
                let encoded_title = &rest[slash + 1..];
                if !parent_id.is_empty() {
                    return Some(PulseInternalAction::AddFolder(
                        parent_id.to_string(),
                        percent_decode(encoded_title),
                    ));
                }
            }
        }
    }
    if let Some(path) = url.strip_prefix("pulse://settings/") {
        if path == "clear-history" {
            return Some(PulseInternalAction::ClearHistory);
        }
        if let Some(rest) = path.strip_prefix("set/") {
            if let Some(slash) = rest.find('/') {
                let key = rest[..slash].to_string();
                let val = percent_decode(&rest[slash + 1..]);
                return Some(PulseInternalAction::SetSetting(key, val));
            }
        }
    }
    if let Some(path) = url.strip_prefix("pulse://import/") {
        if path == "file" {
            return Some(PulseInternalAction::OpenFilePicker);
        }
        if path == "skip" {
            return Some(PulseInternalAction::SkipImport);
        }
        if let Some(idx) = path.strip_prefix("browser/") {
            if let Ok(n) = idx.parse::<usize>() {
                return Some(PulseInternalAction::ImportFromBrowser(n));
            }
        }
        if let Some(idx) = path.strip_prefix("replace/") {
            if let Ok(n) = idx.parse::<usize>() {
                return Some(PulseInternalAction::ReplaceFromBrowser(n));
            }
        }
    }
    None
}

fn queue_pulse_action(action: PulseInternalAction) {
    if let Ok(mut pending) = PULSE_ACTION.get_or_init(|| Mutex::new(None)).lock() {
        *pending = Some(action);
    }
}

fn append_switch(command_line: &CommandLine, name: &str) {
    command_line.append_switch(Some(&CefString::from(name)));
}

fn publish_status(message: &str) {
    merge_ui_update(BrowserUiUpdate {
        status: Some(message.to_string()),
        ..BrowserUiUpdate::default()
    });
}

fn publish_navigation_state(can_go_back: bool, can_go_forward: bool, is_loading: bool) {
    merge_ui_update(BrowserUiUpdate {
        can_go_back: Some(can_go_back),
        can_go_forward: Some(can_go_forward),
        is_loading: Some(is_loading),
        ..BrowserUiUpdate::default()
    });
}

fn merge_ui_update(update: BrowserUiUpdate) {
    if let Ok(mut pending) = UI_UPDATE.get_or_init(|| Mutex::new(None)).lock() {
        let pending = pending.get_or_insert_with(BrowserUiUpdate::default);
        if update.status.is_some() {
            pending.status = update.status;
        }
        if update.address.is_some() {
            pending.address = update.address;
        }
        if update.can_go_back.is_some() {
            pending.can_go_back = update.can_go_back;
        }
        if update.can_go_forward.is_some() {
            pending.can_go_forward = update.can_go_forward;
        }
        if update.is_loading.is_some() {
            pending.is_loading = update.is_loading;
        }
        if update.active_tab_title.is_some() {
            pending.active_tab_title = update.active_tab_title;
        }
        if update.active_tab_url.is_some() {
            pending.active_tab_url = update.active_tab_url;
        }
    }
}

fn focus_browser(browser: &Browser) {
    if let Some(host) = browser.host() {
        host.set_focus(1);
    }
}

fn with_browser<F>(missing_message: &'static str, action: F) -> Result<&'static str, String>
where
    F: FnOnce(&Browser) -> Result<&'static str, String>,
{
    CEF_STATE.with(|state| {
        let state = state.borrow();
        let Some(browser) = state.browser.as_ref() else {
            return Err(missing_message.to_string());
        };

        action(browser)
    })
}

fn cookie_access_allowed(request: Option<&mut Request>) -> i32 {
    let Some(request) = request else {
        return 0;
    };

    let request_url = cef_userfree_to_string(&request.url());
    let first_party_url = cef_userfree_to_string(&request.first_party_for_cookies());

    if privacy::should_allow_cookie_access(&request_url, &first_party_url) {
        1
    } else {
        0
    }
}

fn maybe_store_login_from_request(request: &Request) {
    let method = cef_userfree_to_string(&request.method());
    if !method.eq_ignore_ascii_case("POST")
        || !is_login_capture_resource_type(request.resource_type())
    {
        return;
    }

    let Some(body) = post_body_bytes(request.post_data()) else {
        return;
    };

    let request_url = cef_userfree_to_string(&request.url());
    let first_party_url = cef_userfree_to_string(&request.first_party_for_cookies());
    let Some(login) = credentials::capture_urlencoded_login(&request_url, &first_party_url, &body)
    else {
        return;
    };

    let Some(vault_file) = VAULT_FILE.get() else {
        return;
    };

    let credential = vault::StoredCredential::new(&login.origin, &login.username, &login.password);
    let _ = vault::upsert_credential(vault_file, credential);
}

fn is_login_capture_resource_type(resource_type: ResourceType) -> bool {
    resource_type == ResourceType::MAIN_FRAME || resource_type == ResourceType::XHR
}

fn post_body_bytes(post_data: Option<PostData>) -> Option<Vec<u8>> {
    let post_data = post_data?;
    if post_data.has_excluded_elements() == 1 {
        return None;
    }

    let mut elements = vec![None; post_data.element_count()];
    post_data.elements(Some(&mut elements));

    let mut body = Vec::new();
    for element in elements.into_iter().flatten() {
        if element.get_type() != PostdataelementType::BYTES {
            return None;
        }

        let byte_count = element.bytes_count();
        if body.len().checked_add(byte_count)? > MAX_CAPTURED_LOGIN_POST_BYTES {
            return None;
        }

        let mut buffer = vec![0_u8; byte_count];
        let copied = element.bytes(buffer.len(), buffer.as_mut_ptr());
        buffer.truncate(copied);
        body.extend_from_slice(&buffer);
    }

    if body.is_empty() { None } else { Some(body) }
}

fn maybe_inject_saved_login(frame: &Frame) {
    if frame.is_main() != 1 {
        return;
    }

    let page_url = cef_userfree_to_string(&frame.url());
    let Some(origin) = credentials::origin_from_url(&page_url) else {
        return;
    };
    let Some(vault_file) = VAULT_FILE.get() else {
        return;
    };

    let Ok(saved_credentials) = vault::credentials_for_origin(vault_file, &origin) else {
        return;
    };
    let Some(credential) = saved_credentials
        .iter()
        .max_by_key(|credential| credential.updated_at_epoch_seconds)
    else {
        return;
    };

    let script = credentials::build_autofill_script(&credential.username, credential.secret());
    frame.execute_java_script(
        Some(&CefString::from(script.as_str())),
        Some(&CefString::from("pulse://local-vault-autofill")),
        1,
    );
}

fn maybe_record_history_visit(frame: &Frame) {
    if frame.is_main() != 1 {
        return;
    }

    let url = cef_userfree_to_string(&frame.url());
    if !browser_data::is_web_url(&url) {
        return;
    }

    let title = CURRENT_PAGE
        .get_or_init(|| Mutex::new(CurrentPage::default()))
        .lock()
        .ok()
        .map(|page| page.title.clone())
        .unwrap_or_default();

    if let Some(store) = BROWSER_DATA.get() {
        let _ = store.record_history_visit(&url, &title);
    }
}

fn maybe_load_error_page(frame: &Frame, failed_url: &str, error_code: i32, error_text: &str) {
    if frame.is_main() != 1 || failed_url.starts_with("data:") {
        return;
    }

    let page = local_pages::error_page_data_url(failed_url, error_code, error_text);
    frame.load_url(Some(&CefString::from(page.as_str())));
}

fn update_current_url(url: &str) {
    if let Ok(mut page) = CURRENT_PAGE
        .get_or_init(|| Mutex::new(CurrentPage::default()))
        .lock()
    {
        if browser_data::is_web_url(url) {
            page.url = url.to_string();
        } else {
            page.url.clear();
            page.title.clear();
        }
    }
}

fn update_current_title(title: &str) {
    if let Ok(mut page) = CURRENT_PAGE
        .get_or_init(|| Mutex::new(CurrentPage::default()))
        .lock()
    {
        page.title = title.trim().to_string();
    }
}

fn cef_userfree_to_string(value: &cef::CefStringUserfree) -> String {
    CefStringUtf16::from(value).to_string()
}
