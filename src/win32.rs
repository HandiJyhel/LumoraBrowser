#![allow(dead_code)]

use std::cell::RefCell;
use std::ffi::c_void;
use std::ptr::null;

// ── Types opaques Win32 ──────────────────────────────────────────────────────

pub type Bool = i32;
pub type Hbrush = *mut c_void;
pub type Hcursor = *mut c_void;
pub type Hicon = *mut c_void;
pub type Hinstance = *mut c_void;
pub type Hmenu = *mut c_void;
pub type Hwnd = *mut c_void;
pub type Lparam = isize;
pub type Lresult = isize;
pub type Wparam = usize;

// ── Constantes Win32 ─────────────────────────────────────────────────────────

pub const BN_CLICKED: u32 = 0;
pub const COLOR_WINDOW: isize = 5;
pub const CW_USEDEFAULT: i32 = 0x80000000_u32 as i32;
pub const ES_LEFT: u32 = 0x0000;
pub const GWLP_HINSTANCE: i32 = -6;
pub const IDC_ARROW: usize = 32512;
pub const MF_CHECKED: u32 = 0x00000008;
pub const MF_GRAYED: u32 = 0x00000001;
pub const MF_POPUP: u32 = 0x00000010;
pub const MF_SEPARATOR: u32 = 0x00000800;
pub const MF_STRING: u32 = 0x00000000;
pub const OFN_FILEMUSTEXIST: u32 = 0x00001000;
pub const OFN_PATHMUSTEXIST: u32 = 0x00000800;
pub const PM_REMOVE: u32 = 0x0001;
pub const SW_HIDE: i32 = 0;
pub const SW_SHOW: i32 = 5;
pub const TPM_RETURNCMD: u32 = 0x0100;
pub const VK_RETURN: Wparam = 0x0D;
pub const WM_COMMAND: u32 = 0x0111;
pub const WM_CONTEXTMENU: u32 = 0x007B;
pub const WM_CREATE: u32 = 0x0001;
pub const WM_DESTROY: u32 = 0x0002;
pub const WM_KEYDOWN: u32 = 0x0100;
pub const WM_QUIT: u32 = 0x0012;
pub const WM_RBUTTONUP: u32 = 0x0205;
pub const WM_SIZE: u32 = 0x0005;
pub const WS_BORDER: u32 = 0x00800000;
pub const WS_CHILD: u32 = 0x40000000;
pub const WS_EX_CLIENTEDGE: u32 = 0x00000200;
pub const WS_OVERLAPPEDWINDOW: u32 = 0x00CF0000;
pub const WS_VISIBLE: u32 = 0x10000000;

// ── Structures C ─────────────────────────────────────────────────────────────

#[derive(Clone, Copy)]
#[repr(C)]
pub struct Point {
    pub x: i32,
    pub y: i32,
}

#[repr(C)]
pub struct Msg {
    pub hwnd: Hwnd,
    pub message: u32,
    pub w_param: Wparam,
    pub l_param: Lparam,
    pub time: u32,
    pub pt: Point,
}

#[repr(C)]
pub struct WndClassW {
    pub style: u32,
    pub lpfn_wnd_proc: Option<unsafe extern "system" fn(Hwnd, u32, Wparam, Lparam) -> Lresult>,
    pub cb_cls_extra: i32,
    pub cb_wnd_extra: i32,
    pub h_instance: Hinstance,
    pub h_icon: Hicon,
    pub h_cursor: Hcursor,
    pub hbr_background: Hbrush,
    pub lpsz_menu_name: *const u16,
    pub lpsz_class_name: *const u16,
}

#[repr(C)]
pub struct WinRect {
    pub left: i32,
    pub top: i32,
    pub right: i32,
    pub bottom: i32,
}

// Dialogue "Ouvrir un fichier"
#[repr(C)]
pub struct OpenFileName {
    pub lstructsize: u32,
    pub hwnd_owner: Hwnd,
    pub hinstance: Hinstance,
    pub lpstr_filter: *const u16,
    pub lpstr_cust_filter: *mut u16,
    pub n_max_cust_filter: u32,
    pub n_filter_index: u32,
    pub lpstr_file: *mut u16,
    pub n_max_file: u32,
    pub lpstr_file_title: *mut u16,
    pub n_max_file_title: u32,
    pub lpstr_initial_dir: *const u16,
    pub lpstr_title: *const u16,
    pub flags: u32,
    pub n_file_offset: u16,
    pub n_file_extension: u16,
    pub lpstr_def_ext: *const u16,
    pub l_cust_data: isize,
    pub lpfn_hook: *mut c_void,
    pub lp_template_name: *const u16,
    pub pv_reserved: *mut c_void,
    pub dw_reserved: u32,
    pub flags_ex: u32,
}

// ── Extern Win32 ─────────────────────────────────────────────────────────────

#[link(name = "kernel32")]
unsafe extern "system" {
    pub fn GetCurrentThreadId() -> u32;
    pub fn GetModuleHandleW(module_name: *const u16) -> Hinstance;
    pub fn Sleep(milliseconds: u32);
}

#[link(name = "user32")]
unsafe extern "system" {
    pub fn AppendMenuW(menu: Hmenu, flags: u32, id: usize, text: *const u16) -> Bool;
    pub fn CreatePopupMenu() -> Hmenu;
    pub fn CreateWindowExW(
        ex_style: u32,
        class_name: *const u16,
        window_name: *const u16,
        style: u32,
        x: i32,
        y: i32,
        width: i32,
        height: i32,
        parent: Hwnd,
        menu: *mut c_void,
        instance: Hinstance,
        param: *mut c_void,
    ) -> Hwnd;
    pub fn DefWindowProcW(hwnd: Hwnd, msg: u32, w_param: Wparam, l_param: Lparam) -> Lresult;
    pub fn DestroyMenu(menu: Hmenu) -> Bool;
    pub fn DispatchMessageW(msg: *const Msg) -> Lresult;
    pub fn EnableWindow(hwnd: Hwnd, enable: Bool) -> Bool;
    pub fn GetClientRect(hwnd: Hwnd, rect: *mut WinRect) -> Bool;
    pub fn GetCursorPos(point: *mut Point) -> Bool;
    pub fn GetDlgCtrlID(hwnd: Hwnd) -> i32;
    pub fn GetDlgItem(hwnd: Hwnd, control_id: i32) -> Hwnd;
    pub fn GetParent(hwnd: Hwnd) -> Hwnd;
    pub fn GetWindowRect(hwnd: Hwnd, rect: *mut WinRect) -> Bool;
    pub fn GetWindowTextLengthW(hwnd: Hwnd) -> i32;
    pub fn GetWindowTextW(hwnd: Hwnd, text: *mut u16, max_count: i32) -> i32;
    pub fn GetWindowLongPtrW(hwnd: Hwnd, index: i32) -> isize;
    pub fn LoadCursorW(instance: Hinstance, cursor_name: *const u16) -> Hcursor;
    pub fn MoveWindow(hwnd: Hwnd, x: i32, y: i32, width: i32, height: i32, repaint: Bool) -> Bool;
    pub fn PeekMessageW(
        msg: *mut Msg,
        hwnd: Hwnd,
        min_filter: u32,
        max_filter: u32,
        remove_msg: u32,
    ) -> Bool;
    pub fn PostQuitMessage(exit_code: i32);
    pub fn RegisterClassW(wnd_class: *const WndClassW) -> u16;
    pub fn SetWindowTextW(hwnd: Hwnd, text: *const u16) -> Bool;
    pub fn ShowWindow(hwnd: Hwnd, command_show: i32) -> Bool;
    pub fn DialogBoxIndirectParamW(
        instance: Hinstance,
        dialog_template: *const c_void,
        parent: Hwnd,
        dialog_proc: Option<unsafe extern "system" fn(Hwnd, u32, Wparam, Lparam) -> Lresult>,
        init_param: Lparam,
    ) -> isize;
    pub fn CallNextHookEx(
        hook: *mut c_void,
        code: i32,
        w_param: Wparam,
        l_param: Lparam,
    ) -> Lresult;
    pub fn EndDialog(dialog: Hwnd, result: isize) -> Bool;
    pub fn EndMenu() -> Bool;
    pub fn MenuItemFromPoint(hwnd: Hwnd, menu: Hmenu, pt: Point) -> i32;
    pub fn SendMessageW(hwnd: Hwnd, msg: u32, w_param: Wparam, l_param: Lparam) -> Lresult;
    pub fn SetWindowsHookExW(
        id_hook: i32,
        fn_: Option<unsafe extern "system" fn(i32, Wparam, Lparam) -> Lresult>,
        module: Hinstance,
        thread_id: u32,
    ) -> *mut c_void;
    pub fn UnhookWindowsHookEx(hook: *mut c_void) -> Bool;
    pub fn TrackPopupMenu(
        menu: Hmenu,
        flags: u32,
        x: i32,
        y: i32,
        reserved: i32,
        hwnd: Hwnd,
        rect: *const WinRect,
    ) -> i32;
    pub fn TranslateMessage(msg: *const Msg) -> Bool;
    pub fn UpdateWindow(hwnd: Hwnd) -> Bool;
}

#[link(name = "comdlg32")]
unsafe extern "system" {
    pub fn GetOpenFileNameW(ofn: *mut OpenFileName) -> Bool;
}

// ── Helpers partagés ─────────────────────────────────────────────────────────

pub fn wide(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(std::iter::once(0)).collect()
}

pub fn null_mut<T>() -> *mut T {
    null::<T>() as *mut T
}

pub fn low_word(value: Wparam) -> i32 {
    (value & 0xFFFF) as i32
}

pub fn high_word(value: Wparam) -> u32 {
    ((value >> 16) & 0xFFFF) as u32
}

/// Lit le texte d'un contrôle enfant.
pub fn get_control_text(parent: Hwnd, id: i32) -> Option<String> {
    let control = unsafe { GetDlgItem(parent, id) };
    if control.is_null() {
        return None;
    }
    let length = unsafe { GetWindowTextLengthW(control) };
    if length < 0 {
        return None;
    }
    let mut buf: Vec<u16> = vec![0u16; length as usize + 1];
    unsafe { GetWindowTextW(control, buf.as_mut_ptr(), buf.len() as i32) };
    buf.truncate(length as usize);
    Some(String::from_utf16_lossy(&buf))
}

/// Définit le texte d'un contrôle enfant.
pub fn set_control_text(parent: Hwnd, control_id: i32, text: &str) {
    let control = unsafe { GetDlgItem(parent, control_id) };
    if control.is_null() {
        return;
    }
    let text = wide(text);
    unsafe {
        SetWindowTextW(control, text.as_ptr());
    }
}

/// Déplace et redimensionne un contrôle enfant.
pub fn move_control(parent: Hwnd, control_id: i32, x: i32, y: i32, width: i32, height: i32) {
    let control = unsafe { GetDlgItem(parent, control_id) };
    if control.is_null() {
        return;
    }
    unsafe {
        MoveWindow(control, x, y, width, height, 1);
    }
}

/// Active ou désactive un contrôle enfant.
pub fn set_control_enabled(parent: Hwnd, control_id: i32, enabled: bool) {
    let control = unsafe { GetDlgItem(parent, control_id) };
    if control.is_null() {
        return;
    }
    unsafe {
        EnableWindow(control, i32::from(enabled));
    }
}

/// Affiche ou masque un contrôle enfant.
pub fn show_control(parent: Hwnd, control_id: i32, visible: bool) {
    let control = unsafe { GetDlgItem(parent, control_id) };
    if control.is_null() {
        return;
    }
    unsafe {
        ShowWindow(control, if visible { SW_SHOW } else { SW_HIDE });
    }
}

/// Retourne la taille client de la fenêtre.
pub fn client_size(parent: Hwnd) -> Option<(i32, i32)> {
    let mut rect = WinRect {
        left: 0,
        top: 0,
        right: 0,
        bottom: 0,
    };
    if unsafe { GetClientRect(parent, &mut rect) } == 0 {
        return None;
    }
    Some((
        (rect.right - rect.left).max(0),
        (rect.bottom - rect.top).max(0),
    ))
}

/// Ouvre un dialogue de sélection de fichier et retourne le chemin choisi.
pub fn pick_file(parent: Hwnd, title: &str, filter: &str) -> Option<String> {
    let filter_wide: Vec<u16> = filter.encode_utf16().chain(std::iter::once(0)).collect();
    let title_wide = wide(title);
    let mut file_buf: Vec<u16> = vec![0u16; 1024];

    let mut ofn = OpenFileName {
        lstructsize: std::mem::size_of::<OpenFileName>() as u32,
        hwnd_owner: parent,
        hinstance: null_mut(),
        lpstr_filter: filter_wide.as_ptr(),
        lpstr_cust_filter: null_mut(),
        n_max_cust_filter: 0,
        n_filter_index: 1,
        lpstr_file: file_buf.as_mut_ptr(),
        n_max_file: file_buf.len() as u32,
        lpstr_file_title: null_mut(),
        n_max_file_title: 0,
        lpstr_initial_dir: null(),
        lpstr_title: title_wide.as_ptr(),
        flags: OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST,
        n_file_offset: 0,
        n_file_extension: 0,
        lpstr_def_ext: null(),
        l_cust_data: 0,
        lpfn_hook: null_mut(),
        lp_template_name: null(),
        pv_reserved: null_mut(),
        dw_reserved: 0,
        flags_ex: 0,
    };

    let ok = unsafe { GetOpenFileNameW(&mut ofn) };
    if ok == 0 {
        return None;
    }

    let len = file_buf.iter().position(|&c| c == 0).unwrap_or(0);
    file_buf.truncate(len);
    Some(String::from_utf16_lossy(&file_buf))
}

// ── Boîte de dialogue de renommage (DLGTEMPLATE en mémoire) ──────────────────

const RENAME_EDIT_CTL_ID: i32 = 200;

thread_local! {
    // (initial_text_utf16, résultat après dialogue)
    static RENAME_DLG_STATE: RefCell<(Vec<u16>, Option<String>)> =
        RefCell::new((Vec::new(), None));
}

/// Procedure de dialogue pour la boîte de renommage.
unsafe extern "system" fn rename_dlg_proc(
    hwnd: Hwnd,
    msg: u32,
    w_param: Wparam,
    l_param: Lparam,
) -> Lresult {
    let _ = l_param;
    match msg {
        0x0110 => {
            // WM_INITDIALOG — pré-remplir le champ avec le nom actuel + tout sélectionner
            RENAME_DLG_STATE.with(|state| {
                let initial = state.borrow().0.clone();
                let edit = unsafe { GetDlgItem(hwnd, RENAME_EDIT_CTL_ID) };
                if !edit.is_null() {
                    unsafe {
                        SetWindowTextW(edit, initial.as_ptr());
                        SendMessageW(edit, 0x00B1 /* EM_SETSEL */, 0, -1isize as Lparam);
                    }
                }
            });
            1
        }
        0x0111 => {
            // WM_COMMAND
            let ctl_id = (w_param & 0xFFFF) as i32;
            if ctl_id == 1 {
                // IDOK
                let edit = unsafe { GetDlgItem(hwnd, RENAME_EDIT_CTL_ID) };
                if !edit.is_null() {
                    let len = unsafe { GetWindowTextLengthW(edit) };
                    let mut buf = vec![0u16; (len as usize) + 1];
                    unsafe { GetWindowTextW(edit, buf.as_mut_ptr(), len + 1) };
                    buf.truncate(len as usize);
                    RENAME_DLG_STATE.with(|state| {
                        state.borrow_mut().1 = Some(String::from_utf16_lossy(&buf));
                    });
                }
                unsafe { EndDialog(hwnd, 1) };
                1
            } else if ctl_id == 2 {
                // IDCANCEL
                RENAME_DLG_STATE.with(|state| {
                    state.borrow_mut().1 = None;
                });
                unsafe { EndDialog(hwnd, 0) };
                1
            } else {
                0
            }
        }
        _ => 0,
    }
}

/// Construit un DLGTEMPLATE en mémoire pour une boîte de renommage simple.
fn build_rename_dlg_template() -> Vec<u8> {
    let mut t: Vec<u8> = Vec::new();

    // Helper closures
    let push_u16 = |t: &mut Vec<u8>, v: u16| t.extend_from_slice(&v.to_le_bytes());
    let push_u32 = |t: &mut Vec<u8>, v: u32| t.extend_from_slice(&v.to_le_bytes());
    let push_i16 = |t: &mut Vec<u8>, v: i16| t.extend_from_slice(&v.to_le_bytes());
    let push_wstr = |t: &mut Vec<u8>, s: &str| {
        for c in s.encode_utf16() {
            t.extend_from_slice(&c.to_le_bytes());
        }
        t.extend_from_slice(&[0, 0]); // null terminator
    };
    let align4 = |t: &mut Vec<u8>| {
        while t.len() % 4 != 0 {
            t.push(0);
        }
    };

    // ── DLGTEMPLATE header ─────────────────────────────────────────────────────
    // style: DS_MODALFRAME(0x80) | DS_CENTER(0x800) | WS_POPUP(0x8000_0000) |
    //        WS_CAPTION(0x00C0_0000) | WS_SYSMENU(0x0008_0000)
    push_u32(&mut t, 0x80CC0880);
    push_u32(&mut t, 0); // exStyle
    push_u16(&mut t, 4); // cdit = 4 contrôles
    push_i16(&mut t, 0);
    push_i16(&mut t, 0); // x, y
    push_i16(&mut t, 222);
    push_i16(&mut t, 82); // cx, cy (dialog units)
    push_u16(&mut t, 0); // menu = aucun
    push_u16(&mut t, 0); // window class = défaut
    push_wstr(&mut t, "Renommer le favori"); // titre

    // ── Contrôle 1 : étiquette STATIC ─────────────────────────────────────────
    align4(&mut t);
    push_u32(&mut t, 0x50000000); // WS_CHILD | WS_VISIBLE
    push_u32(&mut t, 0); // exStyle
    push_i16(&mut t, 10);
    push_i16(&mut t, 10); // x, y
    push_i16(&mut t, 202);
    push_i16(&mut t, 10); // cx, cy
    push_u16(&mut t, 0xFFFF); // id = -1 (pas important)
    push_u16(&mut t, 0xFFFF);
    push_u16(&mut t, 0x0082); // class = STATIC
    push_wstr(&mut t, "Nouveau nom :");
    push_u16(&mut t, 0); // extra data

    // ── Contrôle 2 : champ EDIT ───────────────────────────────────────────────
    align4(&mut t);
    // WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL(0x80)
    push_u32(&mut t, 0x50000080);
    push_u32(&mut t, 0x00000200); // WS_EX_CLIENTEDGE
    push_i16(&mut t, 10);
    push_i16(&mut t, 25); // x, y
    push_i16(&mut t, 202);
    push_i16(&mut t, 14); // cx, cy
    push_u16(&mut t, RENAME_EDIT_CTL_ID as u16);
    push_u16(&mut t, 0xFFFF);
    push_u16(&mut t, 0x0081); // class = EDIT
    push_wstr(&mut t, ""); // texte initial (rempli via WM_INITDIALOG)
    push_u16(&mut t, 0);

    // ── Contrôle 3 : bouton OK ────────────────────────────────────────────────
    align4(&mut t);
    push_u32(&mut t, 0x50010001); // WS_CHILD | WS_VISIBLE | WS_TABSTOP(0x10000) | BS_DEFPUSHBUTTON(1)
    push_u32(&mut t, 0);
    push_i16(&mut t, 100);
    push_i16(&mut t, 60); // x, y
    push_i16(&mut t, 50);
    push_i16(&mut t, 14); // cx, cy
    push_u16(&mut t, 1); // id = IDOK = 1
    push_u16(&mut t, 0xFFFF);
    push_u16(&mut t, 0x0080); // class = BUTTON
    push_wstr(&mut t, "OK");
    push_u16(&mut t, 0);

    // ── Contrôle 4 : bouton Annuler ───────────────────────────────────────────
    align4(&mut t);
    push_u32(&mut t, 0x50010000); // WS_CHILD | WS_VISIBLE | WS_TABSTOP
    push_u32(&mut t, 0);
    push_i16(&mut t, 158);
    push_i16(&mut t, 60); // x, y
    push_i16(&mut t, 54);
    push_i16(&mut t, 14); // cx, cy
    push_u16(&mut t, 2); // id = IDCANCEL = 2
    push_u16(&mut t, 0xFFFF);
    push_u16(&mut t, 0x0080); // class = BUTTON
    push_wstr(&mut t, "Annuler");
    push_u16(&mut t, 0);

    t
}

/// Affiche une boîte de dialogue modale demandant un nouveau nom.
/// Retourne `Some(nouveau_nom)` si l'utilisateur valide, `None` s'il annule.
pub fn show_rename_input_dialog(parent: Hwnd, current_name: &str) -> Option<String> {
    let initial: Vec<u16> = current_name
        .encode_utf16()
        .chain(std::iter::once(0))
        .collect();

    RENAME_DLG_STATE.with(|state| {
        *state.borrow_mut() = (initial, None);
    });

    let template = build_rename_dlg_template();

    let instance = unsafe { GetModuleHandleW(null()) };
    let ret = unsafe {
        DialogBoxIndirectParamW(
            instance,
            template.as_ptr().cast(),
            parent,
            Some(rename_dlg_proc),
            0,
        )
    };

    if ret == 1 {
        RENAME_DLG_STATE.with(|state| state.borrow().1.clone())
    } else {
        None
    }
}
