use std::ffi::c_void;
use std::path::Path;
use sha2::{Digest, Sha256};

const LOCK_FILE_MAGIC: &[u8; 10] = b"PBVLOCK1\0\0";
const SALT_LEN: usize = 32;
const ITER: u32 = 100_000;

#[repr(C)]
struct DataBlob {
    cb_data: u32,
    pb_data: *mut u8,
}

#[link(name = "crypt32")]
unsafe extern "system" {
    fn CryptProtectData(
        data_in: *mut DataBlob,
        data_description: *const u16,
        optional_entropy: *mut DataBlob,
        reserved: *mut c_void,
        prompt_struct: *mut c_void,
        flags: u32,
        data_out: *mut DataBlob,
    ) -> i32;
    fn CryptUnprotectData(
        data_in: *mut DataBlob,
        data_description: *mut *mut u16,
        optional_entropy: *mut DataBlob,
        reserved: *mut c_void,
        prompt_struct: *mut c_void,
        flags: u32,
        data_out: *mut DataBlob,
    ) -> i32;
}

#[link(name = "kernel32")]
unsafe extern "system" {
    fn LocalFree(memory: *mut c_void) -> *mut c_void;
}

const CRYPTPROTECT_UI_FORBIDDEN: u32 = 0x1;

// Dérive une clé de 32 octets par PBKDF2-SHA256
fn pbkdf2_sha256(password: &[u8], salt: &[u8], iterations: u32) -> [u8; 32] {
    // PBKDF2 avec HMAC-SHA256, 1 bloc (32 octets)
    // PRF = HMAC-SHA256(password, data)
    fn hmac_sha256(key: &[u8], data: &[u8]) -> [u8; 32] {
        let block = 64usize;
        let mut k = [0u8; 64];
        if key.len() > block {
            let h = Sha256::digest(key);
            k[..32].copy_from_slice(&h);
        } else {
            k[..key.len()].copy_from_slice(key);
        }
        let mut ipad = [0x36u8; 64];
        let mut opad = [0x5cu8; 64];
        for i in 0..64 {
            ipad[i] ^= k[i];
            opad[i] ^= k[i];
        }
        let mut inner = Sha256::new();
        inner.update(ipad);
        inner.update(data);
        let inner_hash = inner.finalize();
        let mut outer = Sha256::new();
        outer.update(opad);
        outer.update(inner_hash);
        outer.finalize().into()
    }

    // PRF pour PBKDF2 bloc 1 : salt || 0x00000001
    let mut block_input = Vec::with_capacity(salt.len() + 4);
    block_input.extend_from_slice(salt);
    block_input.extend_from_slice(&1u32.to_be_bytes());

    let mut u = hmac_sha256(password, &block_input);
    let mut result = u;
    for _ in 1..iterations {
        u = hmac_sha256(password, &u);
        for i in 0..32 {
            result[i] ^= u[i];
        }
    }
    result
}

fn random_bytes(n: usize) -> Vec<u8> {
    let mut buf = vec![0u8; n];
    // CryptGenRandom via GetProcessHeap handle (prov = NULL pour Advapi32 old API)
    // On utilise une approche plus simple : remplir via l'entropie système Windows
    // En pratique pour Rust on peut utiliser getrandom si disponible, mais on reste sans dépendance
    // Utilisation de RtlGenRandom (SystemFunction036) via advapi32
    unsafe { fill_random(&mut buf) };
    buf
}

unsafe fn fill_random(buf: &mut [u8]) {
    unsafe extern "system" {
        #[link_name = "SystemFunction036"]
        fn rtl_gen_random(buf: *mut u8, len: u32) -> u8;
    }
    unsafe { rtl_gen_random(buf.as_mut_ptr(), buf.len() as u32); }
}

#[link(name = "advapi32")]
unsafe extern "system" {}

fn dpapi_protect(data: &[u8]) -> Result<Vec<u8>, String> {
    let mut input = data.to_vec();
    let mut blob_in = DataBlob { cb_data: input.len() as u32, pb_data: input.as_mut_ptr() };
    let mut blob_out = DataBlob { cb_data: 0, pb_data: std::ptr::null_mut() };
    let desc = wide("Pulse Browser vault lock");
    let ok = unsafe {
        CryptProtectData(&mut blob_in, desc.as_ptr(), std::ptr::null_mut(), std::ptr::null_mut(), std::ptr::null_mut(), CRYPTPROTECT_UI_FORBIDDEN, &mut blob_out)
    };
    if ok == 0 || blob_out.pb_data.is_null() {
        return Err("vault_lock: chiffrement DPAPI impossible".to_string());
    }
    let bytes = unsafe { std::slice::from_raw_parts(blob_out.pb_data, blob_out.cb_data as usize) }.to_vec();
    unsafe { LocalFree(blob_out.pb_data.cast()); }
    Ok(bytes)
}

fn dpapi_unprotect(data: &[u8]) -> Result<Vec<u8>, String> {
    let mut input = data.to_vec();
    let mut blob_in = DataBlob { cb_data: input.len() as u32, pb_data: input.as_mut_ptr() };
    let mut blob_out = DataBlob { cb_data: 0, pb_data: std::ptr::null_mut() };
    let ok = unsafe {
        CryptUnprotectData(&mut blob_in, std::ptr::null_mut(), std::ptr::null_mut(), std::ptr::null_mut(), std::ptr::null_mut(), CRYPTPROTECT_UI_FORBIDDEN, &mut blob_out)
    };
    if ok == 0 || blob_out.pb_data.is_null() {
        return Err("vault_lock: dechiffrement DPAPI impossible".to_string());
    }
    let bytes = unsafe { std::slice::from_raw_parts(blob_out.pb_data, blob_out.cb_data as usize) }.to_vec();
    unsafe { LocalFree(blob_out.pb_data.cast()); }
    Ok(bytes)
}

fn wide(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

// ── Fichier de verrou ─────────────────────────────────────────────────────────
// Format : LOCK_FILE_MAGIC(10) | salt(32) | hash(32) = 74 octets bruts, chiffrés DPAPI

fn build_lock_file(salt: &[u8; SALT_LEN], hash: &[u8; 32]) -> Vec<u8> {
    let mut v = Vec::with_capacity(10 + SALT_LEN + 32);
    v.extend_from_slice(LOCK_FILE_MAGIC);
    v.extend_from_slice(salt);
    v.extend_from_slice(hash);
    v
}

fn parse_lock_file(data: &[u8]) -> Option<([u8; SALT_LEN], [u8; 32])> {
    if data.len() != 10 + SALT_LEN + 32 { return None; }
    if &data[..10] != LOCK_FILE_MAGIC { return None; }
    let mut salt = [0u8; SALT_LEN];
    let mut hash = [0u8; 32];
    salt.copy_from_slice(&data[10..10 + SALT_LEN]);
    hash.copy_from_slice(&data[10 + SALT_LEN..]);
    Some((salt, hash))
}

pub fn has_master_password(lock_path: &Path) -> bool {
    lock_path.exists()
}

pub fn set_master_password(lock_path: &Path, password: &str) -> Result<(), String> {
    let salt: [u8; SALT_LEN] = random_bytes(SALT_LEN).try_into()
        .map_err(|_| "vault_lock: génération sel impossible".to_string())?;
    let hash = pbkdf2_sha256(password.as_bytes(), &salt, ITER);
    let raw = build_lock_file(&salt, &hash);
    let encrypted = dpapi_protect(&raw)?;

    if let Some(parent) = lock_path.parent() {
        std::fs::create_dir_all(parent)
            .map_err(|e| format!("vault_lock: creation dossier: {e}"))?;
    }
    std::fs::write(lock_path, &encrypted)
        .map_err(|e| format!("vault_lock: ecriture: {e}"))?;
    Ok(())
}

pub fn verify_master_password(lock_path: &Path, password: &str) -> Result<bool, String> {
    if !lock_path.exists() { return Ok(true); }
    let encrypted = std::fs::read(lock_path)
        .map_err(|e| format!("vault_lock: lecture: {e}"))?;
    let raw = dpapi_unprotect(&encrypted)?;
    let (salt, stored_hash) = parse_lock_file(&raw)
        .ok_or_else(|| "vault_lock: format invalide".to_string())?;
    let computed = pbkdf2_sha256(password.as_bytes(), &salt, ITER);
    Ok(computed == stored_hash)
}

pub fn clear_master_password(lock_path: &Path) -> Result<(), String> {
    if lock_path.exists() {
        std::fs::remove_file(lock_path)
            .map_err(|e| format!("vault_lock: suppression: {e}"))?;
    }
    Ok(())
}
