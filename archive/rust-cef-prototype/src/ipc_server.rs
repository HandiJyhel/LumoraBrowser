use std::ffi::c_void;
use std::path::PathBuf;
use serde::Deserialize;

use crate::{profile, vault, vault_lock, APP_VERSION};

const PIPE_NAME: &str = r"\\.\pipe\PulseBrowserCore";
const PIPE_ACCESS_DUPLEX: u32 = 0x00000003;
const PIPE_TYPE_BYTE: u32 = 0x00000000;
const PIPE_WAIT: u32 = 0x00000000;
const PIPE_UNLIMITED_INSTANCES: u32 = 255;
const ERROR_PIPE_CONNECTED: u32 = 535;

type Handle = *mut c_void;

#[link(name = "kernel32")]
unsafe extern "system" {
    fn CreateNamedPipeW(
        name: *const u16,
        open_mode: u32,
        pipe_mode: u32,
        max_instances: u32,
        out_buf: u32,
        in_buf: u32,
        timeout: u32,
        security: *mut c_void,
    ) -> Handle;
    fn ConnectNamedPipe(pipe: Handle, overlapped: *mut c_void) -> i32;
    fn DisconnectNamedPipe(pipe: Handle) -> i32;
    fn CloseHandle(h: Handle) -> i32;
    fn ReadFile(h: Handle, buf: *mut c_void, n: u32, read: *mut u32, ov: *mut c_void) -> i32;
    fn WriteFile(h: Handle, buf: *const c_void, n: u32, written: *mut u32, ov: *mut c_void) -> i32;
    fn GetLastError() -> u32;
    fn FlushFileBuffers(h: Handle) -> i32;
}

fn invalid_handle() -> Handle {
    usize::MAX as Handle
}

fn to_wide(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

// ── Pipe handle ───────────────────────────────────────────────────────────────

struct Pipe(Handle);

impl Drop for Pipe {
    fn drop(&mut self) {
        unsafe {
            DisconnectNamedPipe(self.0);
            CloseHandle(self.0);
        }
    }
}

fn create_pipe(name: &str) -> Result<Pipe, String> {
    let w = to_wide(name);
    let h = unsafe {
        CreateNamedPipeW(
            w.as_ptr(),
            PIPE_ACCESS_DUPLEX,
            PIPE_TYPE_BYTE | PIPE_WAIT,
            PIPE_UNLIMITED_INSTANCES,
            65536,
            65536,
            0,
            std::ptr::null_mut(),
        )
    };
    if h == invalid_handle() {
        return Err(format!("CreateNamedPipeW echec: {}", unsafe { GetLastError() }));
    }
    Ok(Pipe(h))
}

fn read_byte(pipe: &Pipe) -> Option<u8> {
    let mut buf = [0u8];
    let mut read = 0u32;
    let ok = unsafe { ReadFile(pipe.0, buf.as_mut_ptr() as _, 1, &mut read, std::ptr::null_mut()) };
    if ok == 0 || read == 0 { None } else { Some(buf[0]) }
}

fn read_line(pipe: &Pipe) -> Option<String> {
    let mut line = Vec::new();
    loop {
        match read_byte(pipe) {
            None => {
                if line.is_empty() { return None; }
                break;
            }
            Some(b'\n') => break,
            Some(b'\r') => {}
            Some(b) => line.push(b),
        }
    }
    String::from_utf8(line).ok()
}

fn write_line(pipe: &Pipe, s: &str) {
    let buf = format!("{s}\n");
    let bytes = buf.as_bytes();
    let mut written = 0u32;
    unsafe {
        WriteFile(pipe.0, bytes.as_ptr() as _, bytes.len() as u32, &mut written, std::ptr::null_mut());
        FlushFileBuffers(pipe.0);
    }
}

// ── État serveur ──────────────────────────────────────────────────────────────

struct ServerState {
    vault_file: PathBuf,
    lock_path: PathBuf,
    /// true = coffre verrouillé (mot de passe maître requis mais pas encore saisi)
    vault_locked: bool,
}

impl ServerState {
    fn new(vault_file: PathBuf, lock_path: PathBuf) -> Self {
        let vault_locked = vault_lock::has_master_password(&lock_path);
        Self { vault_file, lock_path, vault_locked }
    }
}

// ── Protocole JSON ────────────────────────────────────────────────────────────

#[derive(Deserialize)]
struct Request {
    id: String,
    method: String,
    origin: Option<String>,
    username: Option<String>,
    password: Option<String>,
}

fn resp_ok(id: &str) -> String {
    format!(r#"{{"id":{id_json},"ok":true}}"#, id_json = serde_json::json!(id))
}

fn resp_ok_value(id: &str, result: serde_json::Value) -> String {
    serde_json::json!({ "id": id, "ok": true, "result": result }).to_string()
}

fn resp_err(id: &str, msg: &str) -> String {
    serde_json::json!({ "id": id, "ok": false, "error": msg }).to_string()
}

fn dispatch(line: &str, state: &mut ServerState) -> (String, bool) {
    let req: Request = match serde_json::from_str(line) {
        Ok(r) => r,
        Err(e) => return (resp_err("?", &e.to_string()), false),
    };

    let id = &req.id;
    let shutdown = req.method == "shutdown";

    let response = match req.method.as_str() {
        "ping" => resp_ok_value(id, serde_json::json!("pong")),

        "get_profile" => resp_ok_value(id, serde_json::json!({
            "version": APP_VERSION,
            "vault_file": state.vault_file.to_string_lossy()
        })),

        // ── Verrouillage ──────────────────────────────────────────────────────

        "lock_status" => {
            let status = if state.vault_locked { "locked" } else { "unlocked" };
            resp_ok_value(id, serde_json::json!(status))
        }

        "unlock_vault" => {
            let password = req.password.as_deref().unwrap_or("");
            match vault_lock::verify_master_password(&state.lock_path, password) {
                Ok(true) => {
                    state.vault_locked = false;
                    resp_ok_value(id, serde_json::json!(true))
                }
                Ok(false) => resp_ok_value(id, serde_json::json!(false)),
                Err(e) => resp_err(id, &e),
            }
        }

        "set_master_password" => {
            let password = req.password.as_deref().unwrap_or("");
            match vault_lock::set_master_password(&state.lock_path, password) {
                Ok(()) => {
                    state.vault_locked = false; // déjà déverrouillé dans cette session
                    resp_ok(id)
                }
                Err(e) => resp_err(id, &e),
            }
        }

        "verify_master_password" => {
            let password = req.password.as_deref().unwrap_or("");
            match vault_lock::verify_master_password(&state.lock_path, password) {
                Ok(ok) => resp_ok_value(id, serde_json::json!(ok)),
                Err(e) => resp_err(id, &e),
            }
        }

        "clear_master_password" => {
            match vault_lock::clear_master_password(&state.lock_path) {
                Ok(()) => {
                    state.vault_locked = false;
                    resp_ok(id)
                }
                Err(e) => resp_err(id, &e),
            }
        }

        // ── Accès coffre (gardé par le verrou) ───────────────────────────────

        "list_credentials" => {
            if state.vault_locked {
                return (resp_err(id, "vault_locked"), false);
            }
            match vault::load_vault(&state.vault_file) {
                Ok(v) => {
                    let creds: Vec<serde_json::Value> = v.credentials().iter().map(|c| serde_json::json!({
                        "origin": c.origin,
                        "username": c.username,
                        "password": c.secret(),
                        "created_at": c.created_at_epoch_seconds,
                        "updated_at": c.updated_at_epoch_seconds,
                    })).collect();
                    resp_ok_value(id, serde_json::json!(creds))
                }
                Err(e) => resp_err(id, &e),
            }
        }

        "delete_credential" => {
            if state.vault_locked {
                return (resp_err(id, "vault_locked"), false);
            }
            let origin = req.origin.as_deref().unwrap_or("");
            let username = req.username.as_deref().unwrap_or("");
            match vault::remove_credential(&state.vault_file, origin, username) {
                Ok(()) => resp_ok(id),
                Err(e) => resp_err(id, &e),
            }
        }

        "upsert_credential" => {
            if state.vault_locked {
                return (resp_err(id, "vault_locked"), false);
            }
            let origin = req.origin.as_deref().unwrap_or("");
            let username = req.username.as_deref().unwrap_or("");
            let password = req.password.as_deref().unwrap_or("");
            match vault::upsert_credential(
                &state.vault_file,
                vault::StoredCredential::new(origin, username, password),
            ) {
                Ok(()) => resp_ok(id),
                Err(e) => resp_err(id, &e),
            }
        }

        "shutdown" => resp_ok(id),

        other => resp_err(id, &format!("methode inconnue: {other}")),
    };

    (response, shutdown)
}

fn serve_client(pipe: &Pipe, state: &mut ServerState) -> bool {
    loop {
        let Some(line) = read_line(pipe) else { return false };
        if line.trim().is_empty() { continue; }
        let (resp, shutdown) = dispatch(line.trim(), state);
        write_line(pipe, &resp);
        if shutdown { return true; }
    }
}

// ── Point d'entrée du mode --serve ───────────────────────────────────────────

pub fn run() -> Result<(), String> {
    let profile = profile::PulseProfile::default()?;
    vault::ensure_transparent_vault(&profile.vault_file).ok();

    let lock_path = profile.vault_file
        .parent()
        .unwrap_or(std::path::Path::new("."))
        .join("vault-master.lock");

    let mut state = ServerState::new(profile.vault_file.clone(), lock_path);

    loop {
        let pipe = create_pipe(PIPE_NAME)?;
        let ok = unsafe { ConnectNamedPipe(pipe.0, std::ptr::null_mut()) };
        if ok == 0 && unsafe { GetLastError() } != ERROR_PIPE_CONNECTED {
            std::thread::sleep(std::time::Duration::from_millis(50));
            continue;
        }
        if serve_client(&pipe, &mut state) {
            return Ok(());
        }
    }
}
