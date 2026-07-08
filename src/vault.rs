use std::ffi::c_void;
use std::path::Path;
use std::time::{SystemTime, UNIX_EPOCH};

const VAULT_MAGIC: &[u8; 8] = b"PBVAULT1";
const VAULT_VERSION: u32 = 1;
const PAYLOAD_MAGIC: &[u8; 12] = b"PBVDATA1\0\0\0\0";
const VAULT_MARKER: &[u8] = b"pulse-browser-transparent-local-vault";
const CRYPTPROTECT_UI_FORBIDDEN: u32 = 0x1;

#[derive(Clone, PartialEq, Eq)]
pub struct StoredCredential {
    pub origin: String,
    pub username: String,
    secret: String,
    pub created_at_epoch_seconds: u64,
    pub updated_at_epoch_seconds: u64,
}

impl StoredCredential {
    #[allow(dead_code)]
    pub fn new(origin: &str, username: &str, secret: &str) -> Self {
        let now = unix_now();
        Self {
            origin: normalize_origin(origin),
            username: username.trim().to_string(),
            secret: secret.to_string(),
            created_at_epoch_seconds: now,
            updated_at_epoch_seconds: now,
        }
    }

    pub fn secret(&self) -> &str {
        &self.secret
    }

    fn with_timestamps(
        origin: String,
        username: String,
        secret: String,
        created_at_epoch_seconds: u64,
        updated_at_epoch_seconds: u64,
    ) -> Self {
        Self {
            origin,
            username,
            secret,
            created_at_epoch_seconds,
            updated_at_epoch_seconds,
        }
    }
}

#[derive(Clone, Default, PartialEq, Eq)]
pub struct LocalVault {
    credentials: Vec<StoredCredential>,
}

impl LocalVault {
    pub fn empty() -> Self {
        Self {
            credentials: Vec::new(),
        }
    }

    pub fn credentials(&self) -> &[StoredCredential] {
        &self.credentials
    }

    pub fn credentials_for_origin(&self, origin: &str) -> Vec<StoredCredential> {
        let origin = normalize_origin(origin);
        self.credentials
            .iter()
            .filter(|credential| credential.origin == origin)
            .cloned()
            .collect()
    }

    pub fn remove_credential(&mut self, origin: &str, username: &str) {
        let origin = normalize_origin(origin);
        let username = username.trim().to_string();
        self.credentials
            .retain(|c| !(c.origin == origin && c.username == username));
    }

    pub fn upsert_credential(&mut self, credential: StoredCredential) {
        let mut credential = credential;
        credential.origin = normalize_origin(&credential.origin);
        credential.username = credential.username.trim().to_string();

        if let Some(existing) = self.credentials.iter_mut().find(|existing| {
            existing.origin == credential.origin && existing.username == credential.username
        }) {
            existing.secret = credential.secret;
            existing.updated_at_epoch_seconds = unix_now();
            return;
        }

        self.credentials.push(credential);
    }
}

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

pub fn ensure_transparent_vault(path: &Path) -> Result<(), String> {
    if path.exists() {
        let loaded = load_vault_internal(path)?;
        if loaded.was_legacy_marker {
            save_vault(path, &loaded.vault)?;
        }
        return Ok(());
    }

    save_vault(path, &LocalVault::empty())
}

pub fn load_vault(path: &Path) -> Result<LocalVault, String> {
    Ok(load_vault_internal(path)?.vault)
}

pub fn save_vault(path: &Path, vault: &LocalVault) -> Result<(), String> {
    if let Some(parent) = path.parent() {
        std::fs::create_dir_all(parent)
            .map_err(|error| format!("coffre local: creation du dossier impossible: {error}"))?;
    }

    let payload = encode_vault_payload(vault)?;
    let encrypted_payload = protect_for_current_windows_user(&payload)?;
    let file_bytes = build_vault_file(&encrypted_payload);
    std::fs::write(path, file_bytes)
        .map_err(|error| format!("coffre local: ecriture impossible: {error}"))?;
    Ok(())
}

#[allow(dead_code)]
pub fn upsert_credential(path: &Path, credential: StoredCredential) -> Result<(), String> {
    let mut vault = load_vault(path)?;
    vault.upsert_credential(credential);
    save_vault(path, &vault)
}

#[allow(dead_code)]
pub fn remove_credential(path: &Path, origin: &str, username: &str) -> Result<(), String> {
    let mut vault = load_vault(path)?;
    vault.remove_credential(origin, username);
    save_vault(path, &vault)
}

#[allow(dead_code)]
pub fn credentials_for_origin(path: &Path, origin: &str) -> Result<Vec<StoredCredential>, String> {
    Ok(load_vault(path)?.credentials_for_origin(origin))
}

struct LoadedVault {
    vault: LocalVault,
    was_legacy_marker: bool,
}

fn load_vault_internal(path: &Path) -> Result<LoadedVault, String> {
    let encrypted_payload = read_vault_payload(path)?;
    let payload = unprotect_for_current_windows_user(&encrypted_payload)?;

    if payload == VAULT_MARKER {
        return Ok(LoadedVault {
            vault: LocalVault::empty(),
            was_legacy_marker: true,
        });
    }

    Ok(LoadedVault {
        vault: decode_vault_payload(&payload)?,
        was_legacy_marker: false,
    })
}

fn read_vault_payload(path: &Path) -> Result<Vec<u8>, String> {
    let bytes = std::fs::read(path)
        .map_err(|error| format!("coffre local: lecture impossible: {error}"))?;
    parse_vault_file(&bytes)
}

fn parse_vault_file(bytes: &[u8]) -> Result<Vec<u8>, String> {
    if bytes.len() < 16 || &bytes[..8] != VAULT_MAGIC {
        return Err("coffre local: format Pulse Browser invalide".to_string());
    }

    let version = u32::from_le_bytes(bytes[8..12].try_into().unwrap_or_default());
    if version != VAULT_VERSION {
        return Err(format!("coffre local: version {version} non supportee"));
    }

    let payload_len = u32::from_le_bytes(bytes[12..16].try_into().unwrap_or_default()) as usize;
    let payload_end = 16_usize
        .checked_add(payload_len)
        .ok_or_else(|| "coffre local: taille de payload invalide".to_string())?;
    if bytes.len() != payload_end {
        return Err("coffre local: taille de payload incoherente".to_string());
    }

    Ok(bytes[16..payload_end].to_vec())
}

fn build_vault_file(encrypted_payload: &[u8]) -> Vec<u8> {
    let mut bytes = Vec::with_capacity(16 + encrypted_payload.len());
    bytes.extend_from_slice(VAULT_MAGIC);
    bytes.extend_from_slice(&VAULT_VERSION.to_le_bytes());
    bytes.extend_from_slice(&(encrypted_payload.len() as u32).to_le_bytes());
    bytes.extend_from_slice(encrypted_payload);
    bytes
}

fn encode_vault_payload(vault: &LocalVault) -> Result<Vec<u8>, String> {
    let mut bytes = Vec::new();
    bytes.extend_from_slice(PAYLOAD_MAGIC);
    write_u32(&mut bytes, vault.credentials.len() as u32);

    for credential in &vault.credentials {
        write_string(&mut bytes, &credential.origin)?;
        write_string(&mut bytes, &credential.username)?;
        write_string(&mut bytes, credential.secret())?;
        write_u64(&mut bytes, credential.created_at_epoch_seconds);
        write_u64(&mut bytes, credential.updated_at_epoch_seconds);
    }

    Ok(bytes)
}

fn decode_vault_payload(bytes: &[u8]) -> Result<LocalVault, String> {
    let mut cursor = 0;
    read_exact(bytes, &mut cursor, PAYLOAD_MAGIC.len())
        .filter(|magic| magic == PAYLOAD_MAGIC)
        .ok_or_else(|| "coffre local: payload invalide".to_string())?;

    let count = read_u32(bytes, &mut cursor)? as usize;
    let mut credentials = Vec::with_capacity(count);

    for _ in 0..count {
        let origin = read_string(bytes, &mut cursor)?;
        let username = read_string(bytes, &mut cursor)?;
        let secret = read_string(bytes, &mut cursor)?;
        let created_at_epoch_seconds = read_u64(bytes, &mut cursor)?;
        let updated_at_epoch_seconds = read_u64(bytes, &mut cursor)?;

        credentials.push(StoredCredential::with_timestamps(
            normalize_origin(&origin),
            username,
            secret,
            created_at_epoch_seconds,
            updated_at_epoch_seconds,
        ));
    }

    if cursor != bytes.len() {
        return Err("coffre local: donnees supplementaires inattendues".to_string());
    }

    Ok(LocalVault { credentials })
}

fn protect_for_current_windows_user(input: &[u8]) -> Result<Vec<u8>, String> {
    let mut input = input.to_vec();
    let mut data_in = DataBlob {
        cb_data: input.len() as u32,
        pb_data: input.as_mut_ptr(),
    };
    let mut data_out = DataBlob {
        cb_data: 0,
        pb_data: std::ptr::null_mut(),
    };
    let description = wide("Pulse Browser local vault");

    let ok = unsafe {
        CryptProtectData(
            &mut data_in,
            description.as_ptr(),
            std::ptr::null_mut(),
            std::ptr::null_mut(),
            std::ptr::null_mut(),
            CRYPTPROTECT_UI_FORBIDDEN,
            &mut data_out,
        )
    };

    if ok == 0 || data_out.pb_data.is_null() {
        return Err("coffre local: chiffrement Windows DPAPI impossible".to_string());
    }

    copy_and_free_blob(data_out)
}

fn unprotect_for_current_windows_user(input: &[u8]) -> Result<Vec<u8>, String> {
    let mut input = input.to_vec();
    let mut data_in = DataBlob {
        cb_data: input.len() as u32,
        pb_data: input.as_mut_ptr(),
    };
    let mut data_out = DataBlob {
        cb_data: 0,
        pb_data: std::ptr::null_mut(),
    };

    let ok = unsafe {
        CryptUnprotectData(
            &mut data_in,
            std::ptr::null_mut(),
            std::ptr::null_mut(),
            std::ptr::null_mut(),
            std::ptr::null_mut(),
            CRYPTPROTECT_UI_FORBIDDEN,
            &mut data_out,
        )
    };

    if ok == 0 || data_out.pb_data.is_null() {
        return Err("coffre local: dechiffrement Windows DPAPI impossible".to_string());
    }

    copy_and_free_blob(data_out)
}

fn copy_and_free_blob(blob: DataBlob) -> Result<Vec<u8>, String> {
    if blob.pb_data.is_null() {
        return Ok(Vec::new());
    }

    let bytes = unsafe { std::slice::from_raw_parts(blob.pb_data, blob.cb_data as usize) }.to_vec();
    unsafe {
        LocalFree(blob.pb_data.cast());
    }

    Ok(bytes)
}

fn write_string(bytes: &mut Vec<u8>, value: &str) -> Result<(), String> {
    let len =
        u32::try_from(value.len()).map_err(|_| "coffre local: chaine trop longue".to_string())?;
    write_u32(bytes, len);
    bytes.extend_from_slice(value.as_bytes());
    Ok(())
}

fn read_string(bytes: &[u8], cursor: &mut usize) -> Result<String, String> {
    let len = read_u32(bytes, cursor)? as usize;
    let value = read_exact(bytes, cursor, len)
        .ok_or_else(|| "coffre local: chaine tronquee".to_string())?;
    String::from_utf8(value.to_vec()).map_err(|_| "coffre local: chaine UTF-8 invalide".to_string())
}

fn write_u32(bytes: &mut Vec<u8>, value: u32) {
    bytes.extend_from_slice(&value.to_le_bytes());
}

fn read_u32(bytes: &[u8], cursor: &mut usize) -> Result<u32, String> {
    let value =
        read_exact(bytes, cursor, 4).ok_or_else(|| "coffre local: u32 tronque".to_string())?;
    Ok(u32::from_le_bytes(value.try_into().unwrap_or_default()))
}

fn write_u64(bytes: &mut Vec<u8>, value: u64) {
    bytes.extend_from_slice(&value.to_le_bytes());
}

fn read_u64(bytes: &[u8], cursor: &mut usize) -> Result<u64, String> {
    let value =
        read_exact(bytes, cursor, 8).ok_or_else(|| "coffre local: u64 tronque".to_string())?;
    Ok(u64::from_le_bytes(value.try_into().unwrap_or_default()))
}

fn read_exact<'a>(bytes: &'a [u8], cursor: &mut usize, len: usize) -> Option<&'a [u8]> {
    let end = cursor.checked_add(len)?;
    let slice = bytes.get(*cursor..end)?;
    *cursor = end;
    Some(slice)
}

fn normalize_origin(origin: &str) -> String {
    let origin = origin.trim().to_ascii_lowercase();
    origin.trim_end_matches('/').to_string()
}

fn unix_now() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|duration| duration.as_secs())
        .unwrap_or_default()
}

fn wide(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(std::iter::once(0)).collect()
}

#[cfg(test)]
mod tests {
    use super::{
        LocalVault, PAYLOAD_MAGIC, StoredCredential, VAULT_MAGIC, VAULT_VERSION, build_vault_file,
        decode_vault_payload, encode_vault_payload, parse_vault_file,
    };

    #[test]
    fn vault_file_has_magic_version_and_payload_size() {
        let bytes = build_vault_file(b"encrypted");

        assert_eq!(&bytes[..8], VAULT_MAGIC);
        assert_eq!(
            u32::from_le_bytes(bytes[8..12].try_into().unwrap()),
            VAULT_VERSION
        );
        assert_eq!(u32::from_le_bytes(bytes[12..16].try_into().unwrap()), 9);
        assert_eq!(&bytes[16..], b"encrypted");
    }

    #[test]
    fn parse_vault_file_rejects_payload_size_mismatch() {
        let mut bytes = build_vault_file(b"encrypted");
        bytes.push(0);

        assert!(parse_vault_file(&bytes).is_err());
    }

    #[test]
    fn encodes_and_decodes_empty_payload() {
        let payload = encode_vault_payload(&LocalVault::empty()).unwrap();

        assert_eq!(&payload[..PAYLOAD_MAGIC.len()], PAYLOAD_MAGIC);
        assert_eq!(
            decode_vault_payload(&payload).unwrap().credentials().len(),
            0
        );
    }

    #[test]
    fn encodes_and_decodes_credential_without_losing_secret() {
        let mut vault = LocalVault::empty();
        vault.upsert_credential(StoredCredential::with_timestamps(
            "https://youtube.com".to_string(),
            "user@example.com".to_string(),
            "secret-password".to_string(),
            10,
            20,
        ));

        let decoded = decode_vault_payload(&encode_vault_payload(&vault).unwrap()).unwrap();
        let credentials = decoded.credentials_for_origin("https://youtube.com/");

        assert_eq!(credentials.len(), 1);
        assert_eq!(credentials[0].username, "user@example.com");
        assert_eq!(credentials[0].secret(), "secret-password");
    }

    #[test]
    fn upsert_replaces_existing_origin_and_username() {
        let mut vault = LocalVault::empty();
        vault.upsert_credential(StoredCredential::with_timestamps(
            "https://youtube.com".to_string(),
            "user@example.com".to_string(),
            "old".to_string(),
            10,
            20,
        ));
        vault.upsert_credential(StoredCredential::with_timestamps(
            "https://youtube.com/".to_string(),
            "user@example.com".to_string(),
            "new".to_string(),
            30,
            40,
        ));

        let credentials = vault.credentials_for_origin("https://youtube.com");

        assert_eq!(credentials.len(), 1);
        assert_eq!(credentials[0].secret(), "new");
    }
}
