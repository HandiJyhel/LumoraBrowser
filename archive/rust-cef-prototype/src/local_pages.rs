use crate::{
    bookmarks::{BookmarkKind, BookmarkNode, OTHER_ROOT_ID, TOOLBAR_ROOT_ID},
    bookmarks_import::BookmarkImportSource,
    browser_data::{FavoriteEntry, HistoryEntry},
    privacy, profile, settings,
};

pub const HOME_ADDRESS: &str = "pulse://accueil";

pub fn home_page_data_url(version: &str) -> String {
    let html = format!(
        r#"<!doctype html>
<html lang="fr">
<head>
<meta charset="utf-8">
<title>Pulse Browser</title>
<style>
:root {{ color-scheme: light dark; font-family: "Segoe UI", Arial, sans-serif; }}
body {{ margin: 0; min-height: 100vh; display: grid; place-items: center; background: #f6f8fb; color: #18202c; }}
main {{ width: min(720px, calc(100vw - 48px)); }}
h1 {{ font-size: 34px; margin: 0 0 10px; font-weight: 680; }}
p {{ font-size: 16px; line-height: 1.55; margin: 0 0 18px; color: #4c5666; }}
.panel {{ border: 1px solid #d9e0ea; border-radius: 8px; padding: 18px 20px; background: white; }}
.row {{ display: flex; gap: 10px; flex-wrap: wrap; margin-top: 16px; }}
.pill {{ border: 1px solid #c8d2df; border-radius: 999px; padding: 7px 11px; font-size: 13px; color: #344256; }}
@media (prefers-color-scheme: dark) {{
  body {{ background: #151922; color: #edf2f7; }}
  p {{ color: #aeb8c7; }}
  .panel {{ background: #1d2430; border-color: #303a49; }}
  .pill {{ border-color: #435064; color: #d7deea; }}
}}
</style>
</head>
<body>
<main>
<h1>Pulse Browser</h1>
<p>Navigation locale, simple et securisee. Entre une adresse dans la barre pour commencer.</p>
<div class="panel">
<p>Version {version}. Les donnees de navigation de ce profil restent sur cet ordinateur.</p>
<div class="row">
<span class="pill">Chromium via CEF</span>
<span class="pill">Profil local</span>
<span class="pill">Cookies tiers limites</span>
</div>
</div>
</main>
</body>
</html>"#
    );
    html_data_url(&html)
}

pub fn history_page_data_url(entries: &[HistoryEntry]) -> String {
    let content = if entries.is_empty() {
        "<p>Aucune visite web locale pour le moment.</p>".to_string()
    } else {
        let mut list = String::from("<ul>");
        for entry in entries {
            list.push_str(&format!(
                "<li><a href=\"{url}\">{title}</a><span>{host}</span></li>",
                url = escape_html(&entry.url),
                title = escape_html(display_title(&entry.title, &entry.url).as_str()),
                host = escape_html(&privacy::display_host(&entry.url))
            ));
        }
        list.push_str("</ul>");
        list
    };

    simple_page_data_url(
        "Historique local",
        "Les dernieres pages web visitees avec ce profil.",
        &content,
    )
}

#[allow(dead_code)]
pub fn favorites_page_data_url(entries: &[FavoriteEntry]) -> String {
    let content = if entries.is_empty() {
        "<p>Aucun favori local pour le moment.</p>".to_string()
    } else {
        let mut list = String::from("<ul>");
        for entry in entries {
            list.push_str(&format!(
                "<li><a href=\"{url}\">{title}</a><span>{host}</span></li>",
                url = escape_html(&entry.url),
                title = escape_html(display_title(&entry.title, &entry.url).as_str()),
                host = escape_html(&privacy::display_host(&entry.url))
            ));
        }
        list.push_str("</ul>");
        list
    };

    simple_page_data_url(
        "Favoris locaux",
        "Les pages ajoutees aux favoris dans ce profil.",
        &content,
    )
}

pub fn bookmarks_page_data_url(nodes: &[BookmarkNode]) -> String {
    let json = nodes_to_json(nodes);
    let html = BOOKMARKS_PAGE.replace("{JSON_DATA}", &json);
    html_data_url(&html)
}

fn nodes_to_json(nodes: &[BookmarkNode]) -> String {
    let mut out = format!(
        r#"{{"toolbar_id":"{}","other_id":"{}","nodes":["#,
        TOOLBAR_ROOT_ID, OTHER_ROOT_ID
    );
    for (i, n) in nodes.iter().enumerate() {
        if i > 0 {
            out.push(',');
        }
        out.push_str(&format!(
            r#"{{"id":{},"parent_id":{},"kind":{},"title":{},"url":{},"position":{}}}"#,
            json_str(&n.id),
            json_str(&n.parent_id),
            if n.kind == BookmarkKind::Url {
                "\"url\""
            } else {
                "\"folder\""
            },
            json_str(&n.title),
            json_str(&n.url),
            n.position,
        ));
    }
    out.push_str("]}");
    out
}

fn json_str(s: &str) -> String {
    let mut out = String::with_capacity(s.len() + 2);
    out.push('"');
    for c in s.chars() {
        match c {
            '"' => out.push_str("\\\""),
            '\\' => out.push_str("\\\\"),
            '\n' => out.push_str("\\n"),
            '\r' => out.push_str("\\r"),
            '\t' => out.push_str("\\t"),
            c if (c as u32) < 0x20 => out.push_str(&format!("\\u{:04X}", c as u32)),
            c => out.push(c),
        }
    }
    out.push('"');
    out
}

const BOOKMARKS_PAGE: &str = r####"<!doctype html>
<html lang="fr">
<head>
<meta charset="utf-8">
<title>Favoris - Pulse Browser</title>
<style>
*,*::before,*::after{box-sizing:border-box;margin:0;padding:0;}
html,body{height:100%;overflow:hidden;}
body{
  font-family:'Segoe UI',system-ui,-apple-system,Arial,sans-serif;
  font-size:14px;background:var(--bg);color:var(--text);
  display:flex;flex-direction:column;
}
:root{
  --bg:#ffffff;
  --sb:#f8f9fa;
  --sb-border:#e8eaed;
  --hover:rgba(32,33,36,.06);
  --sel:#e8f0fe;
  --sel-text:#1967d2;
  --text:#202124;
  --muted:#5f6368;
  --border:rgba(0,0,0,.12);
  --acc:#1a73e8;
  --folder:#ea8600;
  --dng:#d93025;
  --menu-bg:#ffffff;
  --menu-shadow:0 1px 3px 1px rgba(0,0,0,.15),0 1px 2px rgba(0,0,0,.3);
  --input-bg:#f1f3f4;
}
@media(prefers-color-scheme:dark){
  :root{
    --bg:#202124;
    --sb:#292a2d;
    --sb-border:#3c4043;
    --hover:rgba(232,234,237,.1);
    --sel:#394457;
    --sel-text:#8ab4f8;
    --text:#e8eaed;
    --muted:#9aa0a6;
    --border:rgba(255,255,255,.14);
    --acc:#8ab4f8;
    --folder:#f9ab00;
    --dng:#f28b82;
    --menu-bg:#3c4043;
    --menu-shadow:0 1px 3px 1px rgba(0,0,0,.3),0 1px 2px rgba(0,0,0,.5);
    --input-bg:#3c4043;
  }
}
/* Topbar */
.topbar{
  display:flex;align-items:center;height:64px;
  padding:0 20px;gap:12px;flex-shrink:0;
  border-bottom:1px solid var(--border);
}
.topbar-title{
  font-size:20px;font-weight:400;
  white-space:nowrap;flex-shrink:0;letter-spacing:-.01em;
}
.search-wrap{flex:1;max-width:600px;position:relative;}
.search-icon{
  position:absolute;left:14px;top:50%;transform:translateY(-50%);
  color:var(--muted);pointer-events:none;
}
.search-input{
  width:100%;height:44px;padding:0 16px 0 44px;
  border:none;border-radius:22px;
  background:var(--input-bg);color:var(--text);
  font-size:14px;font-family:inherit;outline:none;
  transition:box-shadow .15s,background .15s;
}
.search-input::placeholder{color:var(--muted);}
.search-input:focus{background:var(--bg);box-shadow:0 0 0 2px var(--acc);}
.spacer{flex:1;}
.btn-ghost{
  display:inline-flex;align-items:center;gap:6px;
  padding:0 14px;height:36px;
  border:none;border-radius:4px;background:transparent;
  color:var(--acc);font-size:13px;font-weight:500;
  font-family:inherit;cursor:pointer;white-space:nowrap;
}
.btn-ghost:hover{background:var(--hover);}
.btn-ghost.danger{color:var(--dng);}
.topbar-divider{width:1px;height:24px;background:var(--border);flex-shrink:0;}
/* Layout */
.layout{display:flex;flex:1;overflow:hidden;min-height:0;}
/* Sidebar */
.sidebar{
  width:256px;flex-shrink:0;
  background:var(--sb);
  border-right:1px solid var(--sb-border);
  overflow-y:auto;padding:8px 0;
}
.sb-group{font-size:11px;font-weight:700;text-transform:uppercase;
  letter-spacing:.05em;color:var(--muted);
  padding:10px 20px 4px;user-select:none;}
.sb-row{
  display:flex;align-items:center;height:36px;
  padding:0 16px;gap:10px;cursor:pointer;user-select:none;
}
.sb-row:hover{background:var(--hover);}
.sb-row.active{background:var(--sel);color:var(--sel-text);}
.sb-row.active .sb-folder-ico{color:var(--sel-text);}
.sb-folder-ico{flex-shrink:0;color:var(--folder);display:flex;align-items:center;}
.sb-name{flex:1;font-size:13px;font-weight:500;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
.sb-name.unnamed{color:var(--muted);font-style:italic;font-weight:400;}
.sb-count{font-size:12px;color:var(--muted);flex-shrink:0;}
.sb-indent{padding-left:36px;}
.sb-hr{height:1px;background:var(--border);margin:8px 0;}
/* Pane */
.pane{flex:1;min-width:0;overflow-y:auto;display:flex;flex-direction:column;}
.pane-hdr{
  display:flex;align-items:center;height:48px;
  padding:0 16px;gap:6px;flex-shrink:0;
  border-bottom:1px solid var(--border);
  position:sticky;top:0;background:var(--bg);z-index:10;
}
.bc{display:flex;align-items:center;flex:1;overflow:hidden;gap:2px;min-width:0;}
.bc-part{font-size:14px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:220px;}
.bc-link{color:var(--acc);cursor:pointer;}
.bc-link:hover{text-decoration:underline;}
.bc-cur{color:var(--text);font-weight:500;}
.bc-sep{color:var(--muted);padding:0 2px;flex-shrink:0;}
.pane-cnt{font-size:12px;color:var(--muted);white-space:nowrap;flex-shrink:0;}
/* Item list */
.item-list{padding:4px 8px 24px;}
.item{
  display:flex;align-items:center;
  height:52px;padding:0 8px 0 12px;
  border-radius:8px;gap:14px;cursor:default;
  position:relative;
}
.item:hover{background:var(--hover);}
.item-ico{
  flex-shrink:0;width:24px;height:24px;
  display:flex;align-items:center;justify-content:center;
}
.item-ico img{display:block;border-radius:4px;width:20px;height:20px;object-fit:cover;}
.item-body{flex:1;min-width:0;}
.item-name{
  font-size:13px;font-weight:500;
  white-space:nowrap;overflow:hidden;text-overflow:ellipsis;
}
.item-name.unnamed{color:var(--muted);font-style:italic;font-weight:400;}
.item-url{
  font-size:12px;color:var(--muted);margin-top:1px;
  white-space:nowrap;overflow:hidden;text-overflow:ellipsis;
}
.item-menu{flex-shrink:0;opacity:0;transition:opacity .12s;position:relative;}
.item:hover .item-menu{opacity:1;}
.menu-btn{
  display:flex;align-items:center;justify-content:center;
  width:32px;height:32px;border:none;border-radius:50%;
  background:transparent;cursor:pointer;color:var(--muted);
  font-size:18px;font-weight:700;line-height:1;
  font-family:inherit;padding:0;
}
.menu-btn:hover{background:rgba(128,128,128,.15);}
.ctx{
  display:none;position:absolute;right:0;top:34px;
  background:var(--menu-bg);border-radius:4px;
  box-shadow:var(--menu-shadow);
  min-width:192px;z-index:100;padding:4px 0;
}
.item-menu.open .ctx{display:block;}
.ctx-row{
  display:flex;align-items:center;height:40px;
  padding:0 16px;font-size:13px;color:var(--text);
  cursor:pointer;white-space:nowrap;
}
.ctx-row:hover{background:var(--hover);}
.ctx-row.danger{color:var(--dng);}
.ctx-sep{height:1px;background:var(--border);margin:4px 0;}
/* Empty state */
.empty{
  display:flex;flex-direction:column;align-items:center;
  justify-content:center;gap:16px;
  padding:64px 24px;text-align:center;color:var(--muted);flex:1;
}
.empty p{font-size:14px;}
</style>
</head>
<body>
<div class="topbar">
  <span class="topbar-title">Favoris</span>
  <div class="search-wrap">
    <svg class="search-icon" width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
      <path d="M15.5 14h-.79l-.28-.27A6.471 6.471 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"/>
    </svg>
    <input id="q" class="search-input" type="search"
      placeholder="Rechercher dans les favoris..."
      autocomplete="off" spellcheck="false">
  </div>
  <div class="spacer"></div>
  <button class="btn-ghost" id="btn-folder">
    <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
      <path d="M20 6h-8l-2-2H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm0 12H4V6h5.17l2 2H20v10zm-8-4h2v2h2v-2h2v-2h-2v-2h-2v2h-2z"/>
    </svg>
    Nouveau dossier
  </button>
  <div class="topbar-divider"></div>
  <button class="btn-ghost danger" id="btn-clear">Tout supprimer</button>
</div>
<div class="layout">
  <nav class="sidebar" id="sidebar"></nav>
  <div class="pane" id="pane"></div>
</div>
<div id="fmenu" style="display:none;position:fixed;z-index:500;background:var(--menu-bg);border-radius:4px;box-shadow:var(--menu-shadow);min-width:200px;padding:4px 0;"></div>
<script>
var DATA = {JSON_DATA};
var TID = DATA.toolbar_id, OID = DATA.other_id;
var curFolder = TID, searchQ = '', favc = 0;

function kids(pid) {
  return DATA.nodes
    .filter(function(n){ return n.parent_id === pid; })
    .sort(function(a,b){ return a.position - b.position; });
}
function byId(id) { return DATA.nodes.find(function(n){ return n.id === id; }); }
function kcount(id) { return kids(id).length; }
function host(url) {
  try { return new URL(url).hostname.replace(/^www\./, ''); } catch(e) { return ''; }
}
function nodeName(n) {
  return n.kind === 'url'
    ? (n.title || host(n.url) || 'Sans titre')
    : (n.title || '');
}

var COLORS = ['#1a73e8','#0f9d58','#f9ab00','#d93025','#9334e6','#e52592','#00897b','#558b2f','#0288d1'];
function hue(s) {
  var h = 0;
  for (var i = 0; i < s.length; i++) { h = (Math.imul(31, h) + s.charCodeAt(i)) | 0; }
  return COLORS[Math.abs(h) % COLORS.length];
}

function faviconEl(node) {
  var NS = 'http://www.w3.org/2000/svg';
  if (node.kind === 'folder') {
    var wrap = document.createElement('span');
    wrap.style.cssText = 'display:flex;align-items:center;justify-content:center;color:var(--folder)';
    var svg = document.createElementNS(NS, 'svg');
    svg.setAttribute('width', '20'); svg.setAttribute('height', '20'); svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('fill', 'currentColor');
    var p = document.createElementNS(NS, 'path');
    p.setAttribute('d', 'M10 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z');
    svg.appendChild(p); wrap.appendChild(svg); return wrap;
  }
  var h = host(node.url) || '?';
  var letter = h.charAt(0).toUpperCase();
  var color = hue(h);
  var img = document.createElement('img');
  img.width = 20; img.height = 20; img.style.borderRadius = '4px';
  img.src = 'chrome://favicon/size/32@1x/' + node.url;
  img.addEventListener('error', function() {
    var svg = document.createElementNS(NS, 'svg');
    svg.setAttribute('width', '20'); svg.setAttribute('height', '20'); svg.setAttribute('viewBox', '0 0 20 20');
    var c = document.createElementNS(NS, 'circle');
    c.setAttribute('cx', '10'); c.setAttribute('cy', '10'); c.setAttribute('r', '10'); c.setAttribute('fill', color);
    var t = document.createElementNS(NS, 'text');
    t.setAttribute('x', '10'); t.setAttribute('y', '14');
    t.setAttribute('text-anchor', 'middle'); t.setAttribute('dominant-baseline', 'middle');
    t.setAttribute('font-family', "system-ui,'Segoe UI',sans-serif");
    t.setAttribute('font-size', '11'); t.setAttribute('font-weight', '700'); t.setAttribute('fill', '#fff');
    t.textContent = letter;
    svg.appendChild(c); svg.appendChild(t);
    if (img.parentNode) img.parentNode.replaceChild(svg, img);
  });
  return img;
}

function renderSidebar() {
  var sb = document.getElementById('sidebar'); sb.innerHTML = '';
  var NS = 'http://www.w3.org/2000/svg';
  [['Barre des favoris', TID], ['Autres favoris', OID]].forEach(function(pair, gi) {
    if (gi > 0) { var hr = document.createElement('div'); hr.className = 'sb-hr'; sb.appendChild(hr); }
    var grp = document.createElement('div'); grp.className = 'sb-group'; grp.textContent = pair[0]; sb.appendChild(grp);
    sb.appendChild(makeSbRow(pair[1], pair[0], false, null, false));
    kids(pair[1]).filter(function(n){ return n.kind === 'folder'; }).forEach(function(f) {
      sb.appendChild(makeSbRow(f.id, f.title || '(sans nom)', !f.title, kcount(f.id), true));
    });
  });
}

function makeSbRow(id, label, unnamed, count, indent) {
  var NS = 'http://www.w3.org/2000/svg';
  var div = document.createElement('div');
  div.className = 'sb-row' + (curFolder === id ? ' active' : '') + (indent ? ' sb-indent' : '');
  var icoSpan = document.createElement('span'); icoSpan.className = 'sb-folder-ico';
  var svg = document.createElementNS(NS, 'svg');
  svg.setAttribute('width', '16'); svg.setAttribute('height', '16'); svg.setAttribute('viewBox', '0 0 24 24'); svg.setAttribute('fill', 'currentColor');
  var p = document.createElementNS(NS, 'path');
  p.setAttribute('d', 'M10 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z');
  svg.appendChild(p); icoSpan.appendChild(svg);
  var nameSpan = document.createElement('span');
  nameSpan.className = 'sb-name' + (unnamed ? ' unnamed' : '');
  nameSpan.textContent = label;
  div.appendChild(icoSpan); div.appendChild(nameSpan);
  if (count !== null) {
    var cnt = document.createElement('span'); cnt.className = 'sb-count'; cnt.textContent = count; div.appendChild(cnt);
  }
  div.addEventListener('click', function() { curFolder = id; searchQ = ''; document.getElementById('q').value = ''; render(); });
  return div;
}

function buildTrail(id) {
  var parts = []; var cur = id;
  while (cur && cur !== TID && cur !== OID) {
    var n = byId(cur); if (!n) break;
    parts.unshift({ id: n.id, name: n.title || '(sans nom)' }); cur = n.parent_id;
  }
  parts.unshift({ id: cur, name: cur === TID ? 'Barre des favoris' : 'Autres favoris' });
  return parts;
}

function renderPane() {
  var pane = document.getElementById('pane'); pane.innerHTML = '';
  if (searchQ.length > 1) { renderSearch(pane); return; }
  var items = kids(curFolder);
  var trail = buildTrail(curFolder);
  var hdr = document.createElement('div'); hdr.className = 'pane-hdr';
  var bc = document.createElement('div'); bc.className = 'bc';
  trail.forEach(function(seg, i) {
    if (i > 0) { var sep = document.createElement('span'); sep.className = 'bc-sep'; sep.textContent = '>'; bc.appendChild(sep); }
    var sp = document.createElement('span');
    if (i === trail.length - 1) {
      sp.className = 'bc-part bc-cur'; sp.textContent = seg.name;
    } else {
      sp.className = 'bc-part bc-link'; sp.textContent = seg.name;
      sp.addEventListener('click', (function(sid){ return function(){ curFolder = sid; render(); }; })(seg.id));
    }
    bc.appendChild(sp);
  });
  hdr.appendChild(bc);
  var cntSpan = document.createElement('span'); cntSpan.className = 'pane-cnt';
  cntSpan.textContent = items.length + ' element' + (items.length > 1 ? 's' : '');
  hdr.appendChild(cntSpan);
  pane.appendChild(hdr);
  if (!items.length) { pane.appendChild(makeEmpty('Ce dossier est vide.')); return; }
  var list = document.createElement('div'); list.className = 'item-list';
  items.forEach(function(n) { list.appendChild(makeItem(n)); });
  pane.appendChild(list);
}

function renderSearch(pane) {
  var lq = searchQ.toLowerCase();
  var results = DATA.nodes.filter(function(n) {
    return n.kind === 'url' && (
      n.title.toLowerCase().indexOf(lq) >= 0 ||
      n.url.toLowerCase().indexOf(lq) >= 0 ||
      host(n.url).toLowerCase().indexOf(lq) >= 0
    );
  });
  var hdr = document.createElement('div'); hdr.className = 'pane-hdr';
  var bc = document.createElement('div'); bc.className = 'bc';
  var sp = document.createElement('span'); sp.className = 'bc-part bc-cur';
  sp.textContent = results.length + ' resultat' + (results.length > 1 ? 's' : '') + ' - ' + searchQ;
  bc.appendChild(sp); hdr.appendChild(bc); pane.appendChild(hdr);
  if (!results.length) { pane.appendChild(makeEmpty('Aucun favori trouve.')); return; }
  var list = document.createElement('div'); list.className = 'item-list';
  results.forEach(function(n) { list.appendChild(makeItem(n)); });
  pane.appendChild(list);
}

function makeEmpty(msg) {
  var NS = 'http://www.w3.org/2000/svg';
  var d = document.createElement('div'); d.className = 'empty';
  var svg = document.createElementNS(NS, 'svg');
  svg.setAttribute('width', '56'); svg.setAttribute('height', '56'); svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('fill', 'none'); svg.setAttribute('stroke', 'currentColor'); svg.setAttribute('stroke-width', '1.2');
  svg.setAttribute('opacity', '.35');
  var path = document.createElementNS(NS, 'path');
  path.setAttribute('stroke-linecap', 'round'); path.setAttribute('stroke-linejoin', 'round');
  path.setAttribute('d', 'M17.593 3.322c1.1.128 1.907 1.077 1.907 2.185V21L12 17.25 4.5 21V5.507c0-1.108.806-2.057 1.907-2.185a48.507 48.507 0 0 1 11.186 0z');
  svg.appendChild(path);
  var p = document.createElement('p'); p.textContent = msg;
  d.appendChild(svg); d.appendChild(p); return d;
}

function makeItem(node) {
  var div = document.createElement('div'); div.className = 'item';
  var icoDiv = document.createElement('div'); icoDiv.className = 'item-ico';
  icoDiv.appendChild(faviconEl(node));
  var bodyDiv = document.createElement('div'); bodyDiv.className = 'item-body';
  var nameDiv = document.createElement('div');
  nameDiv.className = 'item-name' + (!node.title ? ' unnamed' : '');
  nameDiv.textContent = nodeName(node) || '(sans nom)';
  var urlDiv = document.createElement('div'); urlDiv.className = 'item-url';
  urlDiv.textContent = node.kind === 'url'
    ? (host(node.url) || node.url)
    : (kcount(node.id) + ' element' + (kcount(node.id) > 1 ? 's' : ''));
  bodyDiv.appendChild(nameDiv); bodyDiv.appendChild(urlDiv);
  var menuDiv = document.createElement('div'); menuDiv.className = 'item-menu';
  var btn = document.createElement('button'); btn.className = 'menu-btn'; btn.title = 'Actions';
  btn.textContent = '⋮';
  btn.addEventListener('click', function(e) { e.stopPropagation(); toggleMenu(menuDiv); });
  var ctx = document.createElement('div'); ctx.className = 'ctx';
  var openRow = document.createElement('div'); openRow.className = 'ctx-row';
  openRow.textContent = node.kind === 'url' ? 'Ouvrir' : 'Ouvrir le dossier';
  openRow.addEventListener('click', function() {
    closeMenus();
    if (node.kind === 'url') window.location.href = node.url;
    else { curFolder = node.id; render(); }
  });
  var renRow = document.createElement('div'); renRow.className = 'ctx-row';
  renRow.textContent = 'Renommer';
  renRow.addEventListener('click', function() { doRename(node.id, node.title); });
  var sepRow = document.createElement('div'); sepRow.className = 'ctx-sep';
  var delRow = document.createElement('div'); delRow.className = 'ctx-row danger';
  delRow.textContent = 'Supprimer';
  delRow.addEventListener('click', function() { doDelete(node.id); });
  ctx.appendChild(openRow); ctx.appendChild(renRow); ctx.appendChild(sepRow); ctx.appendChild(delRow);
  menuDiv.appendChild(btn); menuDiv.appendChild(ctx);
  div.appendChild(icoDiv); div.appendChild(bodyDiv); div.appendChild(menuDiv);
  div.addEventListener('dblclick', function() {
    if (node.kind === 'url') window.location.href = node.url;
    else { curFolder = node.id; render(); }
  });
  div.addEventListener('contextmenu', function(e) {
    var rows = [
      { label: node.kind === 'url' ? 'Ouvrir' : 'Ouvrir le dossier', fn: function() {
        if (node.kind === 'url') window.location.href = node.url;
        else { curFolder = node.id; render(); }
      }},
      { label: 'Renommer', fn: function() { doRename(node.id, node.title); }},
      null,
      { label: 'Supprimer', danger: true, fn: function() { doDelete(node.id); }}
    ];
    showFMenu(e, rows);
  });
  return div;
}

function closeMenus() {
  document.querySelectorAll('.item-menu.open').forEach(function(el){ el.classList.remove('open'); });
}
function toggleMenu(el) {
  var was = el.classList.contains('open');
  closeMenus();
  if (!was) el.classList.add('open');
}
document.addEventListener('click', function(e) {
  var t = e.target;
  while (t) { if (t.classList && t.classList.contains('item-menu')) return; t = t.parentElement; }
  closeMenus();
  hideFMenu();
});
document.addEventListener('contextmenu', function(e) { e.preventDefault(); });
function showFMenu(e, rows) {
  e.preventDefault(); e.stopPropagation();
  var fm = document.getElementById('fmenu'); fm.innerHTML = '';
  rows.forEach(function(row) {
    if (!row) {
      var sep = document.createElement('div');
      sep.style.cssText = 'height:1px;background:var(--border);margin:4px 0;';
      fm.appendChild(sep); return;
    }
    var d = document.createElement('div');
    d.style.cssText = 'padding:7px 16px;cursor:pointer;font-size:13px;color:' +
      (row.danger ? 'var(--dng)' : 'var(--text)') + ';white-space:nowrap;line-height:1.4;';
    d.textContent = row.label;
    d.addEventListener('mouseenter', function() { this.style.background = 'var(--hover)'; });
    d.addEventListener('mouseleave', function() { this.style.background = ''; });
    d.addEventListener('mousedown', function(ev) { ev.preventDefault(); ev.stopPropagation(); hideFMenu(); row.fn(); });
    fm.appendChild(d);
  });
  fm.style.display = 'block';
  var x = e.clientX, y = e.clientY;
  fm.style.left = x + 'px'; fm.style.top = y + 'px';
  requestAnimationFrame(function() {
    var r = fm.getBoundingClientRect();
    if (r.right > window.innerWidth) fm.style.left = (x - r.width) + 'px';
    if (r.bottom > window.innerHeight) fm.style.top = (y - r.height) + 'px';
  });
}
function hideFMenu() { document.getElementById('fmenu').style.display = 'none'; }

function doRename(id, cur) {
  closeMenus();
  var n = window.prompt('Nouveau nom :', cur);
  if (n === null) return;
  n = n.trim(); if (n === cur) return;
  window.location.href = 'pulse://bookmarks/rename/' + id + '/' + encodeURIComponent(n);
}
function doDelete(id) {
  closeMenus();
  if (window.confirm('Supprimer ce favori ?'))
    window.location.href = 'pulse://bookmarks/delete/' + id;
}
function newFolder() {
  var n = window.prompt('Nom du nouveau dossier :', '');
  if (n === null) return;
  n = n.trim(); if (!n) return;
  window.location.href = 'pulse://bookmarks/add-folder/' + curFolder + '/' + encodeURIComponent(n);
}
function clearAll() {
  if (window.confirm('Vider tous les favoris ?'))
    window.location.href = 'pulse://bookmarks/clear';
}

document.getElementById('btn-folder').addEventListener('click', newFolder);
document.getElementById('btn-clear').addEventListener('click', clearAll);
document.getElementById('q').addEventListener('input', function() {
  searchQ = this.value.trim(); render();
});

function render() { renderSidebar(); renderPane(); }
render();
</script>
</body>
</html>"####;

pub fn settings_page_data_url(s: &settings::PulseSettings, p: &profile::PulseProfile) -> String {
    let engines_html = settings::SearchEngine::all()
        .iter()
        .map(|e| {
            let checked = if *e == s.search_engine { " checked" } else { "" };
            let active = if *e == s.search_engine { " active" } else { "" };
            format!(
                r#"<label class="opt-row{active}"><input type="radio" name="se" value="{key}"{checked} onchange="save('search_engine',this.value)"><span class="opt-label">{label}</span></label>"#,
                key = e.key(),
                label = e.label(),
            )
        })
        .collect::<Vec<_>>()
        .join("\n");

    let (home_checked, custom_checked) = match &s.startup {
        settings::StartupBehavior::Home => (" checked", ""),
        settings::StartupBehavior::Custom(_) => ("", " checked"),
    };
    let custom_url = escape_html(s.startup.custom_url());
    let custom_display = if matches!(s.startup, settings::StartupBehavior::Custom(_)) {
        "block"
    } else {
        "none"
    };

    let tab_bar_html = [
        settings::TabBarMode::Horizontal,
        settings::TabBarMode::Vertical,
    ]
    .iter()
    .map(|m| {
        let checked = if *m == s.tab_bar { " checked" } else { "" };
        let active = if *m == s.tab_bar { " active" } else { "" };
        format!(
            r#"<label class="opt-row{active}"><input type="radio" name="tb" value="{key}"{checked} onchange="save('tab_bar',this.value)"><span class="opt-label">{label}</span></label>"#,
            key = m.key(),
            label = m.label(),
        )
    })
    .collect::<Vec<_>>()
    .join("\n");

    let html = format!(
        r#"<!doctype html>
<html lang="fr">
<head>
<meta charset="utf-8">
<title>Parametres — Pulse Browser</title>
<style>
*,*::before,*::after{{box-sizing:border-box;margin:0;padding:0;}}
body{{
  font-family:'Segoe UI',system-ui,-apple-system,Arial,sans-serif;
  font-size:14px;background:var(--bg);color:var(--text);
  min-height:100vh;
}}
:root{{
  --bg:#f8f9fa;--panel:#ffffff;--text:#202124;--muted:#5f6368;
  --border:rgba(0,0,0,.12);--acc:#1a73e8;--acc-light:#e8f0fe;
  --dng:#d93025;--dng-light:#fce8e6;--hover:rgba(0,0,0,.04);
  --input-border:rgba(0,0,0,.24);--input-focus:#1a73e8;
}}
@media(prefers-color-scheme:dark){{
  :root{{
    --bg:#202124;--panel:#292a2d;--text:#e8eaed;--muted:#9aa0a6;
    --border:rgba(255,255,255,.14);--acc:#8ab4f8;--acc-light:#394457;
    --dng:#f28b82;--dng-light:#3e1f1f;--hover:rgba(255,255,255,.06);
    --input-border:rgba(255,255,255,.24);--input-focus:#8ab4f8;
  }}
}}
.page{{max-width:680px;margin:0 auto;padding:32px 24px 64px;}}
h1{{font-size:22px;font-weight:400;margin-bottom:28px;color:var(--text);}}
.card{{background:var(--panel);border-radius:8px;margin-bottom:16px;overflow:hidden;}}
.card-hdr{{padding:20px 24px 0;}}
.card-title{{font-size:14px;font-weight:500;color:var(--text);margin-bottom:4px;}}
.card-desc{{font-size:12px;color:var(--muted);margin-bottom:16px;}}
.opt-row{{
  display:flex;align-items:center;gap:12px;
  padding:12px 24px;cursor:pointer;
  border-top:1px solid var(--border);
  transition:background .1s;
}}
.opt-row:hover{{background:var(--hover);}}
.opt-row.active{{background:var(--acc-light);}}
.opt-row input[type=radio]{{accent-color:var(--acc);width:16px;height:16px;cursor:pointer;flex-shrink:0;}}
.opt-label{{font-size:14px;color:var(--text);}}
.custom-url-wrap{{padding:12px 24px 20px;border-top:1px solid var(--border);}}
.custom-url-input{{
  width:100%;padding:10px 14px;
  border:1px solid var(--input-border);border-radius:4px;
  background:var(--panel);color:var(--text);font-size:14px;
  font-family:inherit;outline:none;
  transition:border-color .15s;
}}
.custom-url-input:focus{{border-color:var(--input-focus);box-shadow:0 0 0 2px rgba(26,115,232,.2);}}
.card-body{{padding:0 24px 20px;}}
.data-row{{
  display:flex;align-items:center;justify-content:space-between;
  padding:16px 0;border-bottom:1px solid var(--border);gap:16px;
}}
.data-row:last-child{{border-bottom:none;}}
.data-label{{font-size:14px;color:var(--text);}}
.data-value{{font-size:12px;color:var(--muted);overflow-wrap:anywhere;max-width:360px;text-align:right;}}
.btn{{
  padding:8px 20px;border-radius:4px;border:none;
  font-size:13px;font-weight:500;font-family:inherit;
  cursor:pointer;white-space:nowrap;flex-shrink:0;
  transition:background .15s;
}}
.btn-danger{{background:var(--dng-light);color:var(--dng);}}
.btn-danger:hover{{background:var(--dng);color:#fff;}}
</style>
</head>
<body>
<div class="page">
  <h1>Parametres</h1>

  <div class="card">
    <div class="card-hdr">
      <div class="card-title">Moteur de recherche</div>
      <div class="card-desc">Utilise quand vous tapez une recherche dans la barre d'adresse.</div>
    </div>
    {engines_html}
  </div>

  <div class="card">
    <div class="card-hdr">
      <div class="card-title">Au demarrage</div>
      <div class="card-desc">Page ouverte au lancement de Pulse Browser.</div>
    </div>
    <label class="opt-row{home_active}">
      <input type="radio" name="su" value="home"{home_checked} onchange="save('startup','home')">
      <span class="opt-label">Page d'accueil Pulse Browser</span>
    </label>
    <label class="opt-row{custom_active}" onclick="showCustom()">
      <input type="radio" name="su" value="custom"{custom_checked} onchange="showCustom()">
      <span class="opt-label">URL personnalisee</span>
    </label>
    <div class="custom-url-wrap" id="custom-wrap" style="display:{custom_display}">
      <input type="url" class="custom-url-input" id="custom-url"
        placeholder="https://..."
        value="{custom_url}"
        onchange="saveCustom(this.value)">
    </div>
  </div>

  <div class="card">
    <div class="card-hdr">
      <div class="card-title">Barre d'onglets</div>
      <div class="card-desc">Position de la barre d'onglets.</div>
    </div>
    {tab_bar_html}
  </div>

  <div class="card">
    <div class="card-hdr">
      <div class="card-title">Donnees</div>
    </div>
    <div class="card-body" style="padding-top:8px;">
      <div class="data-row">
        <span class="data-label">Historique de navigation</span>
        <button class="btn btn-danger" onclick="clearHistory()">Effacer l'historique</button>
      </div>
      <div class="data-row">
        <span class="data-label">Profil</span>
        <span class="data-value">{profile_id}</span>
      </div>
      <div class="data-row">
        <span class="data-label">Donnees Chromium</span>
        <span class="data-value">{cef_path}</span>
      </div>
      <div class="data-row">
        <span class="data-label">Favoris</span>
        <span class="data-value">{bm_path}</span>
      </div>
    </div>
  </div>
</div>
<script>
function save(key, val) {{
  window.location.href = 'pulse://settings/set/' + encodeURIComponent(key) + '/' + encodeURIComponent(val);
}}
function saveCustom(url) {{
  url = url.trim();
  if (!url) return;
  save('startup', url);
}}
function showCustom() {{
  document.getElementById('custom-wrap').style.display = 'block';
  document.getElementById('custom-url').focus();
}}
function clearHistory() {{
  if (window.confirm('Effacer tout l\'historique de navigation ?'))
    window.location.href = 'pulse://settings/clear-history';
}}
</script>
</body>
</html>"#,
        home_active = if matches!(s.startup, settings::StartupBehavior::Home) {
            " active"
        } else {
            ""
        },
        custom_active = if matches!(s.startup, settings::StartupBehavior::Custom(_)) {
            " active"
        } else {
            ""
        },
        profile_id = escape_html(&p.id),
        cef_path = escape_html(&p.cef_cache_dir.to_string_lossy()),
        bm_path = escape_html(&p.bookmarks_file.to_string_lossy()),
    );
    html_data_url(&html)
}

pub fn import_page_data_url(sources: &[BookmarkImportSource]) -> String {
    let browsers_html = if sources.is_empty() {
        "<p class=\"no-src\">Aucun navigateur detecte sur cet ordinateur.</p>".to_string()
    } else {
        let mut html = String::new();
        for (i, src) in sources.iter().enumerate() {
            let label = escape_html(&src.label());
            if src.read_error.is_some() {
                html.push_str(&format!(
                    r#"<div class="browser-row disabled"><span class="browser-name">{label}</span><span class="error-note">Inaccessible</span></div>"#
                ));
            } else {
                html.push_str(&format!(
                    r#"<div class="browser-row"><span class="browser-name">{label}</span><div class="browser-btns"><a href="pulse://import/browser/{i}" class="btn-merge">Fusionner</a><a href="pulse://import/replace/{i}" class="btn-replace">Remplacer</a></div></div>"#
                ));
            }
        }
        html
    };

    let html = format!(
        r#"<!doctype html>
<html lang="fr">
<head>
<meta charset="utf-8">
<title>Importer des favoris — Pulse Browser</title>
<style>
:root{{color-scheme:light dark;font-family:"Segoe UI",system-ui,Arial,sans-serif;--bg:#f2f4f8;--panel:#fff;--text:#1a2232;--muted:#697590;--sm:#8a98b8;--bd:#dde4f0;--acc:#1e5bbf;--acc-bg:#e8efff;--dng:#c0392b;--dng-bg:#fdf0f0;--dng-bd:#e0a0a0;--row-bg:#f8faff;--row-bd:#dde4f0;}}
@media(prefers-color-scheme:dark){{:root{{--bg:#0f1420;--panel:#141c2e;--text:#d8e2f4;--muted:#6878a0;--sm:#4e607a;--bd:#202c44;--acc:#5e90e8;--acc-bg:#1e2e50;--dng:#e07070;--dng-bg:#2e1a1a;--dng-bd:#5a2020;--row-bg:#111824;--row-bd:#202c44;}}}}
*,*::before,*::after{{box-sizing:border-box;margin:0;padding:0;}}
body{{background:var(--bg);color:var(--text);min-height:100vh;display:flex;flex-direction:column;align-items:center;justify-content:flex-start;padding:40px 20px;font-size:14px;}}
.card{{background:var(--panel);border:1px solid var(--bd);border-radius:14px;padding:36px 40px;width:min(580px,100%);box-shadow:0 4px 24px rgba(0,0,0,.08);}}
.logo{{font-size:36px;text-align:center;margin-bottom:16px;}}
h1{{font-size:22px;font-weight:700;text-align:center;margin-bottom:6px;}}
.subtitle{{font-size:14px;color:var(--muted);text-align:center;margin-bottom:32px;}}
h2{{font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:var(--muted);margin-bottom:12px;}}
.browser-row{{display:flex;align-items:center;gap:12px;padding:13px 16px;background:var(--row-bg);border:1px solid var(--row-bd);border-radius:10px;margin-bottom:8px;}}
.browser-row.disabled{{opacity:.45;}}
.browser-name{{flex:1;font-size:14px;font-weight:500;}}
.browser-btns{{display:flex;gap:8px;flex-shrink:0;}}
.btn-merge,.btn-replace{{padding:6px 14px;border-radius:7px;font-size:12px;font-weight:600;text-decoration:none;white-space:nowrap;}}
.btn-merge{{background:var(--acc-bg);color:var(--acc);}}
.btn-merge:hover{{background:var(--acc);color:#fff;}}
.btn-replace{{background:var(--dng-bg);color:var(--dng);border:1px solid var(--dng-bd);}}
.btn-replace:hover{{background:var(--dng);color:#fff;}}
.divider{{border:none;border-top:1px solid var(--bd);margin:28px 0;}}
.file-section{{display:flex;flex-direction:column;align-items:flex-start;gap:10px;}}
.btn-file{{display:inline-flex;align-items:center;gap:8px;padding:12px 20px;background:var(--acc-bg);color:var(--acc);border:1px solid var(--acc);border-radius:10px;font-size:14px;font-weight:600;text-decoration:none;}}
.btn-file:hover{{background:var(--acc);color:#fff;}}
.file-note{{font-size:12px;color:var(--sm);line-height:1.5;}}
.no-src{{font-size:13px;color:var(--muted);padding:10px 0;}}
.tooltip{{font-size:11px;color:var(--sm);margin-top:4px;padding:0 2px;}}
</style>
</head>
<body>
<div class="card">
  <div class="logo">⚡</div>
  <h1>Importer des favoris</h1>
  <p class="subtitle">Choisis d'ou tu veux importer tes favoris.</p>
  <h2>Depuis un navigateur installe</h2>
  {browsers_html}
  <p class="tooltip">
    <strong>Fusionner</strong> : ajoute sans supprimer les favoris existants.<br>
    <strong>Remplacer</strong> : remplace tous les favoris existants (une sauvegarde locale est creee).
  </p>
  <hr class="divider">
  <h2>Depuis un fichier HTML</h2>
  <div class="file-section">
    <a href="pulse://import/file" class="btn-file">📂 Choisir un fichier HTML…</a>
    <p class="file-note">Tous les navigateurs peuvent exporter leurs favoris au format HTML.<br>
    Dans Chrome : Favoris → Gestionnaire de favoris → ⋮ → Exporter les favoris.</p>
  </div>
</div>
</body>
</html>"#
    );
    html_data_url(&html)
}

pub fn profile_page_data_url(pulse_profile: &profile::PulseProfile) -> String {
    let rows = [
        ("Profil", pulse_profile.id.clone()),
        (
            "Donnees Chromium",
            pulse_profile.cef_cache_dir.to_string_lossy().into_owned(),
        ),
        (
            "Navigation",
            pulse_profile.navigation_dir.to_string_lossy().into_owned(),
        ),
        (
            "Favoris structures",
            pulse_profile.bookmarks_file.to_string_lossy().into_owned(),
        ),
        (
            "Coffre",
            pulse_profile.vault_file.to_string_lossy().into_owned(),
        ),
    ];
    let mut content = String::from("<dl>");
    for (name, value) in rows {
        content.push_str(&format!(
            "<dt>{}</dt><dd>{}</dd>",
            escape_html(name),
            escape_html(&value)
        ));
    }
    content.push_str("</dl>");

    simple_page_data_url(
        "Donnees du profil",
        "Tous ces chemins restent locaux sur cet ordinateur.",
        &content,
    )
}

pub fn about_page_data_url(version: &str) -> String {
    let content = format!(
        "<p>Version {}</p>\
         <p>Pulse Browser est un navigateur web local-first en developpement, \
         concu pour rester simple, moderne et securise sans envoyer de donnees vers l'exterieur.</p>\
         <table style=\"border-collapse:collapse;margin-top:1em\">\
           <tr><td style=\"padding:.25em 1em .25em 0;opacity:.6\">Langage</td><td>Rust</td></tr>\
           <tr><td style=\"padding:.25em 1em .25em 0;opacity:.6\">Moteur web</td><td>Chromium via CEF</td></tr>\
           <tr><td style=\"padding:.25em 1em .25em 0;opacity:.6\">Developpeur</td><td>H.J.</td></tr>\
         </table>",
        escape_html(version)
    );
    simple_page_data_url(
        "A propos de Pulse Browser",
        "Navigateur local-first en developpement.",
        &content,
    )
}

pub fn error_page_data_url(failed_url: &str, error_code: i32, error_text: &str) -> String {
    let host = privacy::display_host(failed_url);
    let html = format!(
        r#"<!doctype html>
<html lang="fr">
<head>
<meta charset="utf-8">
<title>Page inaccessible</title>
<style>
:root {{ color-scheme: light dark; font-family: "Segoe UI", Arial, sans-serif; }}
body {{ margin: 0; min-height: 100vh; display: grid; place-items: center; background: #fbf7f4; color: #231c18; }}
main {{ width: min(680px, calc(100vw - 48px)); }}
h1 {{ font-size: 30px; margin: 0 0 10px; font-weight: 680; }}
p {{ font-size: 16px; line-height: 1.55; margin: 0 0 16px; color: #5c504a; }}
.panel {{ border: 1px solid #ead9ce; border-radius: 8px; padding: 18px 20px; background: white; }}
code {{ font-family: Consolas, monospace; }}
@media (prefers-color-scheme: dark) {{
  body {{ background: #211b18; color: #f8f1ec; }}
  p {{ color: #d4c5ba; }}
  .panel {{ background: #2b2420; border-color: #4a3c34; }}
}}
</style>
</head>
<body>
<main>
<h1>Page inaccessible</h1>
<p>Pulse Browser n'a pas reussi a charger <strong>{host}</strong>.</p>
<div class="panel">
<p>Code CEF: <code>{error_code}</code></p>
<p>{error_text}</p>
</div>
</main>
</body>
</html>"#,
        host = escape_html(&host),
        error_text = escape_html(error_text)
    );
    html_data_url(&html)
}

fn simple_page_data_url(title: &str, subtitle: &str, content: &str) -> String {
    let html = format!(
        r#"<!doctype html>
<html lang="fr">
<head>
<meta charset="utf-8">
<title>{title}</title>
<style>
:root {{ color-scheme: light dark; font-family: "Segoe UI", Arial, sans-serif; }}
body {{ margin: 0; min-height: 100vh; background: #f6f8fb; color: #18202c; }}
main {{ width: min(860px, calc(100vw - 48px)); margin: 54px auto; }}
h1 {{ font-size: 30px; margin: 0 0 8px; font-weight: 680; }}
p {{ font-size: 15px; line-height: 1.55; margin: 0 0 18px; color: #4c5666; }}
ul {{ list-style: none; margin: 18px 0 0; padding: 0; border: 1px solid #d9e0ea; border-radius: 8px; overflow: hidden; background: white; }}
li {{ display: grid; gap: 4px; padding: 13px 16px; border-bottom: 1px solid #e8edf4; }}
li:last-child {{ border-bottom: 0; }}
a {{ color: #1f5e9d; text-decoration: none; font-weight: 600; overflow-wrap: anywhere; }}
span {{ color: #6a7483; font-size: 13px; }}
dl {{ display: grid; grid-template-columns: 160px 1fr; gap: 10px 16px; border: 1px solid #d9e0ea; border-radius: 8px; padding: 18px; background: white; }}
dt {{ color: #536071; font-weight: 600; }}
dd {{ margin: 0; overflow-wrap: anywhere; }}
.bookmark-section {{ margin-top: 18px; }}
h2 {{ font-size: 18px; margin: 18px 0 8px; }}
.folder {{ font-weight: 700; color: #d18a20; }}
.nested {{ margin: 8px 0 0 18px; border-radius: 6px; }}
@media (prefers-color-scheme: dark) {{
  body {{ background: #151922; color: #edf2f7; }}
  p, span, dt {{ color: #aeb8c7; }}
  ul, dl {{ background: #1d2430; border-color: #303a49; }}
  li {{ border-color: #303a49; }}
  a {{ color: #8fc7ff; }}
}}
</style>
</head>
<body>
<main>
<h1>{title}</h1>
<p>{subtitle}</p>
{content}
</main>
</body>
</html>"#,
        title = escape_html(title),
        subtitle = escape_html(subtitle),
    );
    html_data_url(&html)
}

fn html_data_url(html: &str) -> String {
    format!("data:text/html;charset=utf-8,{}", percent_encode(html))
}

fn display_title(title: &str, url: &str) -> String {
    let title = title.trim();
    if title.is_empty() || title == "Page sans titre" {
        privacy::display_host(url)
    } else {
        title.to_string()
    }
}

fn percent_encode(value: &str) -> String {
    let mut encoded = String::new();
    for byte in value.as_bytes() {
        match *byte {
            b'A'..=b'Z' | b'a'..=b'z' | b'0'..=b'9' | b'-' | b'_' | b'.' | b'~' => {
                encoded.push(*byte as char);
            }
            byte => encoded.push_str(&format!("%{byte:02X}")),
        }
    }
    encoded
}

fn escape_html(value: &str) -> String {
    let mut escaped = String::new();
    for character in value.chars() {
        match character {
            '&' => escaped.push_str("&amp;"),
            '<' => escaped.push_str("&lt;"),
            '>' => escaped.push_str("&gt;"),
            '"' => escaped.push_str("&quot;"),
            '\'' => escaped.push_str("&#39;"),
            character => escaped.push(character),
        }
    }
    escaped
}

#[cfg(test)]
mod tests {
    use super::{
        HOME_ADDRESS, about_page_data_url, bookmarks_page_data_url, error_page_data_url,
        favorites_page_data_url, history_page_data_url, home_page_data_url,
    };
    use crate::bookmarks::{BookmarkKind, BookmarkNode, TOOLBAR_ROOT_ID};

    #[test]
    fn home_address_is_internal() {
        assert_eq!(HOME_ADDRESS, "pulse://accueil");
    }

    #[test]
    fn home_page_is_data_url() {
        assert!(home_page_data_url("0.4.1-dev").starts_with("data:text/html;charset=utf-8,"));
    }

    #[test]
    fn error_page_does_not_embed_raw_html() {
        let page = error_page_data_url("https://example.com/<x>", -105, "<script>");

        assert!(page.contains("%26lt%3Bscript%26gt%3B"));
        assert!(!page.contains("<script>"));
    }

    #[test]
    fn menu_pages_are_data_urls() {
        assert!(history_page_data_url(&[]).starts_with("data:text/html;charset=utf-8,"));
        assert!(favorites_page_data_url(&[]).starts_with("data:text/html;charset=utf-8,"));
        assert!(
            bookmarks_page_data_url(&[BookmarkNode {
                id: "b1".to_string(),
                parent_id: TOOLBAR_ROOT_ID.to_string(),
                kind: BookmarkKind::Url,
                title: "Example".to_string(),
                url: "https://example.com".to_string(),
                position: 0,
            }])
            .starts_with("data:text/html;charset=utf-8,")
        );
        assert!(about_page_data_url("0.4.1-dev").starts_with("data:text/html;charset=utf-8,"));
    }
}
