/// Wizard d'importation de favoris : affiché au premier lancement (favoris vides)
/// et accessible depuis le menu Favoris > Importer depuis un fichier.
use crate::bookmarks;
use crate::bookmarks_import;
use crate::store;

// ── Rapport d'import fichier ──────────────────────────────────────────────────

pub struct FileImportReport {
    pub added: usize,
    pub folders: usize,
}

// ── Page HTML du wizard ───────────────────────────────────────────────────────

pub fn wizard_page_data_url() -> String {
    let sources = bookmarks_import::discover_import_sources();
    let source_items = sources
        .iter()
        .enumerate()
        .map(|(i, src)| {
            if src.read_error.is_some() {
                format!(
                    r#"<li class="source disabled"><span class="icon">🌐</span>{}</li>"#,
                    html_escape(&src.label())
                )
            } else {
                format!(
                    r#"<li class="source"><a href="pulse://import/browser/{i}" class="btn-source">
                        <span class="icon">🌐</span>{}</a></li>"#,
                    html_escape(&src.label())
                )
            }
        })
        .collect::<Vec<_>>()
        .join("\n");

    let sources_section = if sources.is_empty() {
        r#"<p class="no-sources">Aucun navigateur détecté sur cet ordinateur.</p>"#.to_string()
    } else {
        format!(r#"<ul class="source-list">{source_items}</ul>"#)
    };

    let html = format!(
        r#"<!DOCTYPE html>
<html lang="fr">
<head>
<meta charset="utf-8">
<title>Bienvenue dans Pulse Browser</title>
<style>
  * {{ box-sizing: border-box; margin: 0; padding: 0; }}
  body {{
    font-family: -apple-system, 'Segoe UI', system-ui, sans-serif;
    background: #1a1a2e;
    color: #e0e0e0;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    min-height: 100vh;
    padding: 32px;
  }}
  .card {{
    background: #16213e;
    border: 1px solid #0f3460;
    border-radius: 16px;
    padding: 40px 48px;
    max-width: 600px;
    width: 100%;
    text-align: center;
    box-shadow: 0 8px 32px rgba(0,0,0,0.4);
  }}
  .logo {{ font-size: 48px; margin-bottom: 16px; }}
  h1 {{ font-size: 28px; font-weight: 700; color: #e94560; margin-bottom: 8px; }}
  .subtitle {{ color: #888; margin-bottom: 32px; font-size: 15px; }}
  h2 {{ font-size: 16px; font-weight: 600; color: #a0a0c0; margin: 24px 0 12px; text-align: left; }}
  .source-list {{ list-style: none; text-align: left; }}
  .source {{ margin-bottom: 8px; }}
  .btn-source {{
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 12px 16px;
    background: #0f3460;
    border-radius: 10px;
    color: #e0e0e0;
    text-decoration: none;
    font-size: 14px;
    transition: background 0.2s;
  }}
  .btn-source:hover {{ background: #e94560; color: #fff; }}
  .source.disabled .btn-source {{ opacity: 0.4; pointer-events: none; }}
  .divider {{ border: none; border-top: 1px solid #0f3460; margin: 24px 0; }}
  .actions {{ display: flex; gap: 12px; justify-content: center; flex-wrap: wrap; }}
  .btn {{
    padding: 12px 24px;
    border-radius: 10px;
    font-size: 14px;
    font-weight: 600;
    text-decoration: none;
    cursor: pointer;
    transition: opacity 0.2s;
  }}
  .btn-primary {{ background: #e94560; color: #fff; }}
  .btn-secondary {{ background: #0f3460; color: #e0e0e0; }}
  .btn:hover {{ opacity: 0.85; }}
  .no-sources {{ color: #666; font-size: 14px; text-align: left; }}
</style>
</head>
<body>
<div class="card">
  <div class="logo">⚡</div>
  <h1>Bienvenue dans Pulse Browser</h1>
  <p class="subtitle">Pour commencer, importe tes favoris ou démarre sans.</p>

  <h2>Importer depuis un navigateur</h2>
  {sources_section}

  <hr class="divider">

  <div class="actions">
    <a href="pulse://import/file" class="btn btn-primary">📂 Importer depuis un fichier HTML</a>
    <a href="pulse://import/skip" class="btn btn-secondary">Commencer sans favoris</a>
  </div>
</div>
</body>
</html>"#
    );

    let encoded = data_url_encode(&html);
    format!("data:text/html;charset=utf-8;base64,{encoded}")
}

// ── Import depuis fichier HTML Netscape ───────────────────────────────────────

/// Parse un fichier Netscape Bookmark File (exporté par Chrome, Edge, Firefox, Safari)
/// et importe les favoris dans le store.
pub fn import_from_html_file(path: &str) -> Result<FileImportReport, String> {
    let content =
        std::fs::read_to_string(path).map_err(|e| format!("Impossible de lire le fichier: {e}"))?;

    let items = parse_netscape_html(&content);
    if items.is_empty() {
        return Err("Aucun favori trouvé dans ce fichier.".to_string());
    }

    let store = store::open_bookmarks()?;
    let mut added = 0usize;
    let mut folders = 0usize;

    import_nodes_into_store(
        &store,
        &items,
        bookmarks::OTHER_ROOT_ID,
        &mut added,
        &mut folders,
    )?;

    Ok(FileImportReport { added, folders })
}

// ── Parsing Netscape HTML ─────────────────────────────────────────────────────

#[derive(Debug)]
enum NetscapeNode {
    Url {
        title: String,
        url: String,
    },
    Folder {
        title: String,
        children: Vec<NetscapeNode>,
    },
}

fn parse_netscape_html(html: &str) -> Vec<NetscapeNode> {
    let mut nodes = Vec::new();
    let mut stack: Vec<(String, Vec<NetscapeNode>)> = Vec::new();
    let mut current_folder_title = String::new();

    for line in html.lines() {
        let line = line.trim();

        // Dossier ouvert
        if let Some(title) = extract_h3(line) {
            stack.push((current_folder_title.clone(), std::mem::take(&mut nodes)));
            current_folder_title = title;
            nodes = Vec::new();
            continue;
        }

        // Fin de liste — ferme le dossier courant
        if line.eq_ignore_ascii_case("</dl>") || line.eq_ignore_ascii_case("</dl><p>") {
            if let Some((parent_title, mut parent_nodes)) = stack.pop() {
                let folder = NetscapeNode::Folder {
                    title: current_folder_title.clone(),
                    children: std::mem::take(&mut nodes),
                };
                parent_nodes.push(folder);
                nodes = parent_nodes;
                current_folder_title = parent_title;
            }
            continue;
        }

        // Lien
        if let Some((url, title)) = extract_link(line) {
            if crate::browser_data::is_web_url(&url) {
                nodes.push(NetscapeNode::Url { title, url });
            }
        }
    }

    nodes
}

fn extract_h3(line: &str) -> Option<String> {
    let lower = line.to_lowercase();
    if !lower.contains("<h3") {
        return None;
    }
    let start = line.find('>')? + 1;
    let end = line[start..].find('<').map(|i| i + start)?;
    Some(line[start..end].trim().to_string())
}

fn extract_link(line: &str) -> Option<(String, String)> {
    let lower = line.to_lowercase();
    if !lower.contains("<a ") && !lower.contains("<a\t") {
        return None;
    }
    let href_start = lower.find("href=\"").map(|i| i + 6)?;
    let href_end = line[href_start..].find('"').map(|i| i + href_start)?;
    let url = line[href_start..href_end].to_string();

    let title_start = line.find('>')? + 1;
    let title_end = line[title_start..].find('<').map(|i| i + title_start)?;
    let title = line[title_start..title_end].trim().to_string();

    Some((url, title))
}

fn import_nodes_into_store(
    store: &bookmarks::BookmarkStore,
    nodes: &[NetscapeNode],
    parent_id: &str,
    added: &mut usize,
    folders: &mut usize,
) -> Result<(), String> {
    for node in nodes {
        match node {
            NetscapeNode::Url { title, url } => {
                store.add_url(parent_id, url, title)?;
                *added += 1;
            }
            NetscapeNode::Folder { title, children } => {
                let folder_id = store.add_folder(parent_id, title)?;
                *folders += 1;
                import_nodes_into_store(store, children, &folder_id, added, folders)?;
            }
        }
    }
    Ok(())
}

// ── Helpers ───────────────────────────────────────────────────────────────────

fn html_escape(s: &str) -> String {
    s.replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
        .replace('"', "&quot;")
}

fn data_url_encode(html: &str) -> String {
    use std::fmt::Write;
    let bytes = html.as_bytes();
    let mut out = String::with_capacity(bytes.len() * 4 / 3 + 4);
    const TABLE: &[u8] = b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    let mut i = 0;
    while i + 2 < bytes.len() {
        let b0 = bytes[i] as usize;
        let b1 = bytes[i + 1] as usize;
        let b2 = bytes[i + 2] as usize;
        let _ = write!(
            out,
            "{}{}{}{}",
            TABLE[b0 >> 2] as char,
            TABLE[((b0 & 3) << 4) | (b1 >> 4)] as char,
            TABLE[((b1 & 0xf) << 2) | (b2 >> 6)] as char,
            TABLE[b2 & 0x3f] as char
        );
        i += 3;
    }
    match bytes.len() - i {
        1 => {
            let b0 = bytes[i] as usize;
            let _ = write!(
                out,
                "{}{}==",
                TABLE[b0 >> 2] as char,
                TABLE[(b0 & 3) << 4] as char
            );
        }
        2 => {
            let b0 = bytes[i] as usize;
            let b1 = bytes[i + 1] as usize;
            let _ = write!(
                out,
                "{}{}{}=",
                TABLE[b0 >> 2] as char,
                TABLE[((b0 & 3) << 4) | (b1 >> 4)] as char,
                TABLE[(b1 & 0xf) << 2] as char
            );
        }
        _ => {}
    }
    out
}
