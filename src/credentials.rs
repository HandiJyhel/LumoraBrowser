#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CapturedLogin {
    pub origin: String,
    pub username: String,
    pub password: String,
}

pub fn capture_urlencoded_login(
    request_url: &str,
    first_party_url: &str,
    body: &[u8],
) -> Option<CapturedLogin> {
    let request_origin = origin_from_url(request_url)?;
    let first_party_origin = origin_from_url(first_party_url)?;
    if request_origin != first_party_origin {
        return None;
    }

    let body = std::str::from_utf8(body).ok()?;
    if !looks_like_form_body(body) {
        return None;
    }

    let pairs = parse_urlencoded_pairs(body);
    let username = find_username(&pairs)?;
    let password = find_password(&pairs)?;
    if username.trim().is_empty() || password.is_empty() {
        return None;
    }

    Some(CapturedLogin {
        origin: request_origin,
        username: username.trim().to_string(),
        password,
    })
}

pub fn origin_from_url(url: &str) -> Option<String> {
    let trimmed = url.trim();
    let scheme_end = trimmed.find("://")?;
    let scheme = trimmed[..scheme_end].to_ascii_lowercase();
    if scheme != "http" && scheme != "https" {
        return None;
    }

    let rest = &trimmed[scheme_end + 3..];
    let authority_end = rest
        .find(|character| ['/', '?', '#'].contains(&character))
        .unwrap_or(rest.len());
    let authority = rest[..authority_end].trim().to_ascii_lowercase();
    if authority.is_empty() || authority.contains('@') {
        return None;
    }

    Some(format!("{scheme}://{}", authority.trim_end_matches('.')))
}

pub fn build_autofill_script(username: &str, password: &str) -> String {
    format!(
        r#"(function() {{
  const pulseUsername = {username};
  const pulsePassword = {password};
  const usable = (input) => input && !input.disabled && !input.readOnly && input.offsetParent !== null;
  const setValue = (input, value) => {{
    input.focus();
    input.value = value;
    input.dispatchEvent(new Event('input', {{ bubbles: true }}));
    input.dispatchEvent(new Event('change', {{ bubbles: true }}));
  }};
  const scoreUsername = (input) => {{
    const text = `${{input.type || ''}} ${{input.name || ''}} ${{input.id || ''}} ${{input.autocomplete || ''}} ${{input.placeholder || ''}}`.toLowerCase();
    if (text.includes('email') || text.includes('mail')) return 4;
    if (text.includes('login') || text.includes('username') || text.includes('user')) return 3;
    if (text.includes('identifier') || text.includes('account')) return 2;
    return input.type === 'text' ? 1 : 0;
  }};
  const passwordInput = Array.from(document.querySelectorAll('input[type="password"]')).find(usable);
  if (!passwordInput || passwordInput.value) return;
  const scope = passwordInput.form || passwordInput.closest('form') || document;
  const candidates = Array.from(scope.querySelectorAll('input')).filter((input) => {{
    const type = (input.type || 'text').toLowerCase();
    return usable(input) && ['email', 'text', 'search', 'tel', 'url', ''].includes(type);
  }});
  const usernameInput = candidates
    .map((input) => [scoreUsername(input), input])
    .filter(([score]) => score > 0)
    .sort((a, b) => b[0] - a[0])[0]?.[1] || candidates[candidates.length - 1];
  if (usernameInput && !usernameInput.value) setValue(usernameInput, pulseUsername);
  if (!passwordInput.value) setValue(passwordInput, pulsePassword);
}})();"#,
        username = js_string_literal(username),
        password = js_string_literal(password)
    )
}

fn looks_like_form_body(body: &str) -> bool {
    body.contains('=') && body.len() <= 64 * 1024
}

fn parse_urlencoded_pairs(body: &str) -> Vec<(String, String)> {
    body.split('&')
        .filter_map(|pair| {
            let (name, value) = pair.split_once('=')?;
            Some((decode_form_component(name), decode_form_component(value)))
        })
        .collect()
}

fn find_username(pairs: &[(String, String)]) -> Option<String> {
    let preferred = pairs.iter().find(|(name, _)| {
        let name = name.to_ascii_lowercase();
        contains_any(&name, &["email", "mail", "login", "username", "identifier"])
            || name == "user"
            || name == "id"
    });

    preferred
        .or_else(|| {
            pairs.iter().find(|(name, value)| {
                let name = name.to_ascii_lowercase();
                !looks_like_password_field(&name) && value.contains('@')
            })
        })
        .map(|(_, value)| value.clone())
}

fn find_password(pairs: &[(String, String)]) -> Option<String> {
    pairs
        .iter()
        .find(|(name, value)| {
            looks_like_password_field(&name.to_ascii_lowercase()) && !value.is_empty()
        })
        .map(|(_, value)| value.clone())
}

fn looks_like_password_field(name: &str) -> bool {
    contains_any(name, &["password", "passwd", "pwd", "pass"])
}

fn contains_any(value: &str, needles: &[&str]) -> bool {
    needles.iter().any(|needle| value.contains(needle))
}

fn decode_form_component(value: &str) -> String {
    let bytes = value.as_bytes();
    let mut decoded = Vec::with_capacity(bytes.len());
    let mut index = 0;

    while index < bytes.len() {
        match bytes[index] {
            b'+' => {
                decoded.push(b' ');
                index += 1;
            }
            b'%' if index + 2 < bytes.len() => {
                if let Some(byte) = hex_pair(bytes[index + 1], bytes[index + 2]) {
                    decoded.push(byte);
                    index += 3;
                } else {
                    decoded.push(bytes[index]);
                    index += 1;
                }
            }
            byte => {
                decoded.push(byte);
                index += 1;
            }
        }
    }

    String::from_utf8_lossy(&decoded).into_owned()
}

fn hex_pair(high: u8, low: u8) -> Option<u8> {
    Some(hex_digit(high)? * 16 + hex_digit(low)?)
}

fn hex_digit(value: u8) -> Option<u8> {
    match value {
        b'0'..=b'9' => Some(value - b'0'),
        b'a'..=b'f' => Some(value - b'a' + 10),
        b'A'..=b'F' => Some(value - b'A' + 10),
        _ => None,
    }
}

fn js_string_literal(value: &str) -> String {
    let mut output = String::from("\"");
    for character in value.chars() {
        match character {
            '\\' => output.push_str("\\\\"),
            '"' => output.push_str("\\\""),
            '\n' => output.push_str("\\n"),
            '\r' => output.push_str("\\r"),
            '\t' => output.push_str("\\t"),
            character if character.is_control() => {
                output.push_str(&format!("\\u{:04x}", character as u32));
            }
            character => output.push(character),
        }
    }
    output.push('"');
    output
}

#[cfg(test)]
mod tests {
    use super::{build_autofill_script, capture_urlencoded_login, origin_from_url};

    #[test]
    fn extracts_origin_from_http_url_without_path() {
        assert_eq!(
            origin_from_url("https://Example.com/login?next=/"),
            Some("https://example.com".to_string())
        );
    }

    #[test]
    fn rejects_non_web_origins() {
        assert_eq!(origin_from_url("file:///tmp/index.html"), None);
    }

    #[test]
    fn captures_urlencoded_login_for_same_origin() {
        let login = capture_urlencoded_login(
            "https://example.com/session",
            "https://example.com/login",
            b"email=user%40example.com&password=s3cret",
        )
        .unwrap();

        assert_eq!(login.origin, "https://example.com");
        assert_eq!(login.username, "user@example.com");
        assert_eq!(login.password, "s3cret");
    }

    #[test]
    fn rejects_cross_origin_login_capture() {
        assert!(
            capture_urlencoded_login(
                "https://accounts.example.com/session",
                "https://news.example.net/login",
                b"email=user%40example.com&password=s3cret",
            )
            .is_none()
        );
    }

    #[test]
    fn autofill_script_escapes_secret_values() {
        let script = build_autofill_script("u\"ser", "pa\\ss\nword");

        assert!(script.contains("u\\\"ser"));
        assert!(script.contains("pa\\\\ss\\nword"));
        assert!(!script.contains("console."));
    }
}
