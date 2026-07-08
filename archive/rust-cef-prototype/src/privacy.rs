#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CookieDecision {
    Allow,
    BlockThirdParty,
    BlockUnknownContext,
}

pub fn cookie_access_decision(request_url: &str, first_party_url: &str) -> CookieDecision {
    let Some(request_host) = http_host(request_url) else {
        return CookieDecision::Allow;
    };

    let Some(first_party_host) = http_host(first_party_url) else {
        return CookieDecision::BlockUnknownContext;
    };

    if same_site(&request_host, &first_party_host) {
        CookieDecision::Allow
    } else {
        CookieDecision::BlockThirdParty
    }
}

pub fn should_allow_cookie_access(request_url: &str, first_party_url: &str) -> bool {
    cookie_access_decision(request_url, first_party_url) == CookieDecision::Allow
}

pub fn display_host(url: &str) -> String {
    http_host(url).unwrap_or_else(|| "adresse locale ou interne".to_string())
}

fn same_site(left: &str, right: &str) -> bool {
    left == right || registrable_domain(left) == registrable_domain(right)
}

fn registrable_domain(host: &str) -> String {
    let labels: Vec<&str> = host.split('.').filter(|label| !label.is_empty()).collect();
    if labels.len() < 2 || host.parse::<std::net::IpAddr>().is_ok() {
        return host.to_string();
    }

    labels[labels.len().saturating_sub(2)..].join(".")
}

fn http_host(url: &str) -> Option<String> {
    let url = url.trim();
    let lower = url.to_ascii_lowercase();
    if !lower.starts_with("http://") && !lower.starts_with("https://") {
        return None;
    }

    let after_scheme = url.split_once("://")?.1;
    let authority = after_scheme
        .split(['/', '?', '#'])
        .next()
        .unwrap_or_default()
        .trim();

    if authority.is_empty() {
        return None;
    }

    let without_userinfo = authority
        .rsplit_once('@')
        .map_or(authority, |(_, host)| host);
    let host = without_userinfo
        .split_once(':')
        .map_or(without_userinfo, |(host, _)| host)
        .trim_matches(['[', ']'])
        .to_ascii_lowercase();

    (!host.is_empty()).then_some(host)
}

#[cfg(test)]
mod tests {
    use super::{CookieDecision, cookie_access_decision, display_host, should_allow_cookie_access};

    #[test]
    fn allows_first_party_cookies() {
        assert!(should_allow_cookie_access(
            "https://www.youtube.com/feed",
            "https://youtube.com/"
        ));
    }

    #[test]
    fn allows_same_site_subdomains() {
        assert!(should_allow_cookie_access(
            "https://accounts.google.com/login",
            "https://www.google.com/"
        ));
    }

    #[test]
    fn blocks_third_party_cookie_context() {
        assert_eq!(
            cookie_access_decision(
                "https://accounts.google.com/check",
                "https://example-quebec.ca/"
            ),
            CookieDecision::BlockThirdParty
        );
    }

    #[test]
    fn blocks_unknown_first_party_context_for_http_cookies() {
        assert_eq!(
            cookie_access_decision("https://accounts.google.com/check", ""),
            CookieDecision::BlockUnknownContext
        );
    }

    #[test]
    fn keeps_full_url_out_of_display_host() {
        assert_eq!(
            display_host("https://user:secret@example.com/private?token=hidden"),
            "example.com"
        );
    }
}
