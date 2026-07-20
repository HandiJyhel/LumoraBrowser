using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.System;
using Lumora.Privacy.HttpsEnforcer;
using Lumora.WinUI.Credentials;

namespace Lumora.WinUI;

// Generateur de la page nouvel onglet (HTML/CSS/JS) et du contexte du mode
// d'usage associe. Extrait de MainWindow.Navigation.cs (god file) : ne
// depend que de _uiSettings, aucun changement de comportement, uniquement
// un deplacement de code au sein de la meme classe partielle.
public sealed partial class MainWindow
{
    private string HomePageHtml() =>
        $$"""
        <!doctype html>
        <html lang="fr">
        <head>
        <meta charset="utf-8">
        <title>Accueil Lumora</title>
        <style>
        *{box-sizing:border-box;margin:0;padding:0}
        body{font-family:'Segoe UI',system-ui,sans-serif;background:{{NewTabPageBackgroundCss()}};color:{{(_uiSettings.AccessibilityHighContrast ? "#fff" : NewTabTextColorCss())}};min-height:100vh;display:flex;align-items:flex-start;justify-content:center;padding:{{NewTabBodyPaddingCss()}};font-size:{{(_uiSettings.AccessibilityLargeText ? "17px" : "15px")}};position:relative;overflow-x:hidden}
        body::before{content:"";position:fixed;inset:0;background:{{NewTabBackdropCss()}};pointer-events:none}
        body::after{content:"";position:fixed;inset:-22%;background:{{NewTabLightTraceCss()}};opacity:{{NewTabLightTraceOpacityCss()}};pointer-events:none;mix-blend-mode:screen;transform:translate3d(-6%,0,0) rotate(0.001deg)}
        main{width:min(1320px,100%);display:flex;flex-direction:column;align-items:stretch;gap:38px;position:relative}
        .home-grid{width:100%;display:grid;grid-template-columns:minmax(420px,1.04fr) minmax(380px,.82fr);gap:72px;align-items:center}
        .home-primary{display:flex;flex-direction:column;align-items:stretch;gap:30px;padding-top:18px}
        .mode-side{display:flex;flex-direction:column;gap:16px;justify-self:end;width:min(500px,100%)}
        .brand{display:flex;flex-direction:column;align-items:flex-start;gap:13px}
        .mark{display:flex;align-items:center;gap:14px}
        .logo-icon{width:66px;height:66px;border-radius:15px;object-fit:contain;flex-shrink:0;filter:drop-shadow(0 18px 30px rgba(0,0,0,.32)) drop-shadow(0 0 22px rgba(255,185,53,.18))}
        .logo-name{font-size:42px;font-weight:650;line-height:1;letter-spacing:0;color:{{(_uiSettings.AccessibilityHighContrast ? "#fff" : NewTabLogoTextColorCss())}}}
        .accent-line{width:168px;height:3px;border-radius:999px;background:{{(_uiSettings.AccessibilityHighContrast ? "#ffd500" : NewTabAccentLineCss())}};opacity:.98;box-shadow:0 0 18px {{NewTabAccentGlowCss()}};position:relative;overflow:hidden}
        .accent-line::after{content:"";position:absolute;inset:0;background:linear-gradient(90deg,transparent,rgba(255,255,255,.82),transparent);transform:translateX(-105%)}
        .search{width:100%;height:{{(_uiSettings.AccessibilityLargeText ? "54px" : "50px")}};border-radius:25px;background:{{(_uiSettings.AccessibilityHighContrast ? "#fff" : "#fbf4e8")}};display:flex;align-items:center;gap:12px;padding:0 20px;border:{{(_uiSettings.AccessibilityVisibleFocus ? "2px" : "1px")}} solid {{(_uiSettings.AccessibilityHighContrast ? "#fff" : "rgba(255,248,235,.24)")}};box-shadow:0 14px 38px rgba(0,0,0,.24)}
        .search:focus-within{border-color:{{(_uiSettings.AccessibilityHighContrast ? "#ffd500" : NewTabFocusBorderCss())}};box-shadow:0 14px 38px rgba(0,0,0,.27),0 0 0 3px {{NewTabFocusRingCss()}}}
        .search svg{width:18px;height:18px;color:#796f63;flex-shrink:0}
        .search input{width:100%;height:100%;border:0;outline:0;background:transparent;color:#201f1b;font-size:{{(_uiSettings.AccessibilityLargeText ? "17px" : "15px")}}}
        .search input::placeholder{color:{{(_uiSettings.AccessibilityHighContrast ? "#4a4a4a" : "#81786d")}}}
        .mode-panel{width:100%;display:grid;grid-template-columns:minmax(0,1fr) auto;grid-template-areas:"copy visual" "actions actions";column-gap:18px;row-gap:14px;align-items:start;padding:18px 20px;border:1px solid {{NewTabModeBorderCss()}};border-radius:10px;background:{{NewTabModeSurfaceCss()}};box-shadow:0 14px 32px rgba(0,0,0,.12);position:relative;overflow:hidden}
        .mode-panel::before{content:"";position:absolute;inset:0 auto 0 0;width:5px;background:{{NewTabModeSignatureBarCss()}}}
        .mode-panel::after{content:"";position:absolute;inset:0;background:{{NewTabModePanelPatternCss()}};opacity:.5;pointer-events:none}
        .mode-copy,.mode-actions,.mode-visual{position:relative;z-index:1}
        .mode-copy{grid-area:copy}
        .mode-eyebrow{font-size:12px;color:{{NewTabModeMutedCss()}};margin-bottom:3px}
        .mode-title{font-size:18px;font-weight:650;color:{{NewTabLogoTextColorCss()}}}
        .mode-text{font-size:13px;color:{{NewTabModeMutedCss()}};line-height:1.42;margin-top:4px;max-width:34ch}
        .mode-visual{grid-area:visual;width:54px;height:42px;border:1px solid {{NewTabModeBorderCss()}};border-radius:8px;background:rgba(255,255,255,.04);display:flex;align-items:center;justify-content:center;gap:5px;padding:8px;overflow:hidden;align-self:start}
        .mode-visual span{display:block;border-radius:999px;background:{{NewTabModeVisualCss()}};box-shadow:0 0 16px {{NewTabModeVisualGlowCss()}}}
        .mode-visual span:nth-child(1){width:7px;height:18px}
        .mode-visual span:nth-child(2){width:7px;height:28px}
        .mode-visual span:nth-child(3){width:7px;height:22px}
        .mode-visual span:nth-child(4){width:7px;height:34px}
        .mode-actions{grid-area:actions;display:flex;gap:8px;flex-wrap:wrap;justify-content:flex-start}
        .mode-chip{border:1px solid {{NewTabModeBorderCss()}};border-radius:999px;background:rgba(255,255,255,.05);color:{{NewTabTextColorCss()}};font-size:12px;padding:7px 10px;white-space:nowrap}
        .mode-intro{width:100%;display:grid;grid-template-columns:minmax(0,1fr) auto;gap:16px;align-items:start;padding:14px 16px;border:{{(_uiSettings.AccessibilityVisibleFocus ? "2px" : "1px")}} solid {{NewTabModeBorderCss()}};border-radius:10px;background:rgba(255,255,255,.02);box-shadow:none}
        .mode-intro-title{font-size:15px;font-weight:650;color:{{NewTabLogoTextColorCss()}}}
        .mode-intro-text{font-size:12px;line-height:1.35;color:{{NewTabModeMutedCss()}};margin-top:4px}
        .mode-intro-points{display:flex;gap:6px;flex-wrap:wrap;margin-top:8px}
        .mode-intro-point{border:1px solid {{NewTabModeBorderCss()}};border-radius:999px;background:rgba(255,255,255,.045);color:{{NewTabTextColorCss()}};font-size:12px;padding:6px 9px}
        .mode-context{width:100%;display:grid;grid-template-columns:minmax(0,1.1fr) minmax(220px,.82fr);gap:18px;padding:18px;border:1px solid {{NewTabModeBorderCss()}};border-radius:10px;background:rgba(255,255,255,.032);box-shadow:0 14px 34px rgba(0,0,0,.12)}
        .mode-context-copy{display:flex;flex-direction:column;gap:10px;min-width:0}
        .mode-context-eyebrow{font-size:12px;color:{{NewTabModeMutedCss()}}}
        .mode-context-title{font-size:17px;font-weight:650;color:{{NewTabLogoTextColorCss()}}}
        .mode-context-text{font-size:12px;line-height:1.35;color:{{NewTabModeMutedCss()}}}
        .mode-draft{width:100%;min-height:116px;resize:vertical;border:1px solid {{NewTabModeBorderCss()}};border-radius:8px;background:{{(_uiSettings.AccessibilityHighContrast ? "#000" : "rgba(255,255,255,.06)")}};color:{{(_uiSettings.AccessibilityHighContrast ? "#fff" : NewTabTextColorCss())}};font:inherit;font-size:13px;line-height:1.35;padding:12px;outline:none}
        .mode-draft:focus{border-color:{{(_uiSettings.AccessibilityHighContrast ? "#ffd500" : NewTabFocusBorderCss())}};box-shadow:0 0 0 3px {{NewTabFocusRingCss()}}}
        .mode-context-actions{display:flex;flex-direction:column;gap:10px}
        .mode-context-button{border:1px solid {{NewTabModeBorderCss()}};border-radius:8px;background:rgba(255,255,255,.05);color:{{NewTabTextColorCss()}};font:inherit;font-size:12px;padding:11px 12px;cursor:pointer;text-align:left}
        .mode-context-button.primary{background:{{NewTabAccentSolidCss()}};border-color:{{NewTabAccentSolidCss()}};color:#15130e;font-weight:650;text-align:center}
        .mode-context-button:hover,.mode-context-button:focus,.mode-intro-button:hover,.mode-intro-button:focus{transform:translateY(-1px);box-shadow:0 10px 22px rgba(0,0,0,.18);outline:none}
        .mode-note-status{min-height:16px;font-size:12px;color:{{NewTabModeMutedCss()}}}
        .mode-intro-button{border:1px solid {{NewTabModeBorderCss()}};border-radius:999px;background:rgba(255,255,255,.06);color:{{NewTabTextColorCss()}};font:inherit;font-size:12px;padding:8px 12px;cursor:pointer;white-space:nowrap}
        .mode-focus .mode-panel{box-shadow:0 16px 38px rgba(0,0,0,.2),0 0 0 1px {{NewTabAccentSolidCss()}}22}
        .mode-focus .mode-visual{gap:3px;background:rgba(255,255,255,.03)}
        .mode-focus .mode-visual span{width:4px;border-radius:3px}
        .mode-focus .mode-visual span:nth-child(1){height:30px}
        .mode-focus .mode-visual span:nth-child(2){height:36px}
        .mode-focus .mode-visual span:nth-child(3){height:18px;opacity:.55}
        .mode-focus .mode-visual span:nth-child(4){height:24px;opacity:.7}
        .mode-reading .mode-panel{background:rgba(255,255,255,.07)}
        .mode-reading .mode-visual{flex-direction:column;align-items:stretch;gap:6px;background:rgba(255,248,234,.07)}
        .mode-reading .mode-visual span{width:auto;height:4px}
        .mode-reading .mode-visual span:nth-child(1){width:90%}
        .mode-reading .mode-visual span:nth-child(2){width:100%}
        .mode-reading .mode-visual span:nth-child(3){width:72%}
        .mode-reading .mode-visual span:nth-child(4){width:52%}
        .mode-creative .mode-panel{background:linear-gradient(135deg,{{NewTabModeSurfaceCss()}},{{NewTabAccentSolidCss()}}18)}
        .mode-creative .mode-visual{transform:rotate(-2deg);background:linear-gradient(135deg,rgba(255,255,255,.07),{{NewTabAccent2SolidCss()}}18)}
        .mode-creative .mode-visual span:nth-child(1){height:16px}
        .mode-creative .mode-visual span:nth-child(2){height:32px}
        .mode-creative .mode-visual span:nth-child(3){height:24px}
        .mode-creative .mode-visual span:nth-child(4){height:38px}
        .mode-research .mode-panel{border-style:dashed}
        .mode-research .mode-visual{flex-direction:column;align-items:stretch;gap:5px;border-style:dashed;background:rgba(67,219,209,.045)}
        .mode-research .mode-visual span{height:3px;width:auto;border-radius:2px}
        .mode-research .mode-visual span:nth-child(1){width:100%}
        .mode-research .mode-visual span:nth-child(2){width:76%}
        .mode-research .mode-visual span:nth-child(3){width:92%}
        .mode-research .mode-visual span:nth-child(4){width:58%}
        .mode-night .mode-panel{background:rgba(6,10,18,.68);border-color:rgba(160,190,255,.24)}
        .mode-night .mode-visual{flex-direction:column;align-items:stretch;gap:7px;background:rgba(6,10,18,.5)}
        .mode-night .mode-visual span{height:3px;width:auto;opacity:.68}
        .mode-night .mode-visual span:nth-child(1){width:68%}
        .mode-night .mode-visual span:nth-child(2){width:92%}
        .mode-night .mode-visual span:nth-child(3){width:50%}
        .mode-night .mode-visual span:nth-child(4){width:74%}
        .mode-workbench{width:min(820px,100%);display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px}
        .mode-tool{min-height:78px;border:1px solid {{NewTabModeBorderCss()}};border-radius:8px;background:rgba(255,255,255,.038);color:{{NewTabTextColorCss()}};text-align:left;padding:12px;cursor:pointer;font:inherit;display:flex;flex-direction:column;gap:5px}
        .mode-tool:hover,.mode-tool:focus{transform:translateY(-1px);background:rgba(255,255,255,.07);box-shadow:0 14px 28px rgba(0,0,0,.18);outline:none}
        .mode-tool-title{font-size:13px;font-weight:650;color:{{NewTabLogoTextColorCss()}}}
        .mode-tool-text{font-size:12px;line-height:1.32;color:{{NewTabModeMutedCss()}}}
        .mode-focus main{width:min(1040px,100%);gap:22px}
        .mode-focus .brand{transform:scale(.94);transform-origin:center top}
        .mode-focus .mode-workbench{grid-template-columns:1fr}
        .mode-focus .mode-tool{min-height:64px}
        .mode-focus .shortcuts{display:none}
        .mode-reading main{gap:26px}
        .mode-reading .search{background:#fff8ee}
        .mode-reading .mode-workbench{width:min(720px,100%)}
        .mode-creative main{width:min(1160px,100%)}
        .mode-creative .mode-workbench{width:min(900px,100%)}
        .mode-creative .mode-tool:nth-child(1){background:linear-gradient(135deg,rgba(255,255,255,.07),{{NewTabAccentSolidCss()}}18)}
        .mode-creative .mode-tool:nth-child(2){background:linear-gradient(135deg,rgba(255,255,255,.06),{{NewTabAccent2SolidCss()}}1f)}
        .mode-research main{width:min(1160px,100%)}
        .mode-research .mode-panel,.mode-research .mode-workbench{width:100%}
        .mode-research .mode-tool{border-style:dashed}
        .mode-night{filter:brightness(.9)}
        .mode-night .search{background:#e8e0d3;box-shadow:0 10px 28px rgba(0,0,0,.22)}
        .mode-night .mode-tool{background:rgba(8,12,22,.58);border-color:rgba(160,190,255,.23)}
        .mode-balanced main{width:min(1320px,100%)}
        .balanced-home{width:100%;min-height:72vh;display:grid;grid-template-columns:minmax(460px,1.08fr) minmax(360px,.82fr);gap:78px;align-items:center}
        .balanced-lead{display:flex;flex-direction:column;align-items:stretch;gap:28px;min-width:0}
        .balanced-brand{display:flex;align-items:center;gap:16px}
        .balanced-logo{width:64px;height:64px;border-radius:15px;object-fit:contain;flex-shrink:0;filter:drop-shadow(0 18px 30px rgba(0,0,0,.32)) drop-shadow(0 0 20px {{NewTabAccentGlowCss()}})}
        .balanced-title{font-size:46px;font-weight:650;line-height:1;letter-spacing:0;color:{{NewTabLogoTextColorCss()}}}
        .balanced-greeting{font-size:13px;color:{{NewTabModeMutedCss()}}}
        .balanced-dock{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px;align-content:start}
        .balanced-mode-panel{grid-column:1 / -1}
        .balanced-wide{grid-column:1 / -1}
        .balanced-card{border:1px solid {{NewTabModeBorderCss()}};border-radius:10px;background:linear-gradient(135deg,rgba(255,255,255,.04),{{NewTabAccent2SolidCss()}}10);padding:16px;box-shadow:0 14px 32px rgba(0,0,0,.12)}
        .balanced-card-title{font-size:15px;font-weight:650;color:{{NewTabLogoTextColorCss()}};margin-bottom:10px}
        .balanced-actions{display:grid;gap:8px}
        .balanced-action{border:1px solid {{NewTabModeBorderCss()}};border-radius:8px;background:rgba(255,255,255,.036);color:{{NewTabTextColorCss()}};font:inherit;font-size:13px;padding:12px;text-align:left;cursor:pointer}
        .balanced-action strong{display:block;font-size:13px;color:{{NewTabLogoTextColorCss()}};margin-bottom:3px}
        .balanced-action span{display:block;font-size:12px;line-height:1.28;color:{{NewTabModeMutedCss()}}}
        .balanced-action:hover,.balanced-action:focus{transform:translateY(-1px);background:rgba(255,255,255,.07);box-shadow:0 12px 26px rgba(0,0,0,.18);outline:none}
        .mode-neutral{align-items:center}
        .mode-neutral::after{display:none}
        .mode-neutral main{width:min(860px,100%);gap:0}
        .neutral-home{width:100%;min-height:72vh;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:22px}
        .neutral-brand{display:flex;align-items:center;justify-content:center;gap:10px;opacity:.9}
        .neutral-logo{width:46px;height:46px;border-radius:12px;object-fit:contain;filter:drop-shadow(0 12px 24px rgba(0,0,0,.3))}
        .neutral-name{font-size:20px;font-weight:600;color:{{NewTabLogoTextColorCss()}}}
        .neutral-clock{font-size:{{(_uiSettings.AccessibilityLargeText ? "68px" : "58px")}};font-weight:520;letter-spacing:0;color:{{NewTabLogoTextColorCss()}};line-height:1}
        .neutral-search{width:min(660px,100%);box-shadow:0 16px 42px rgba(0,0,0,.22)}
        .mode-neutral .shortcuts{justify-content:center;max-width:660px}
        .mode-neutral .shortcut-card{background:rgba(255,255,255,.03)}
        .personalize-invite{width:100%;display:grid;grid-template-columns:minmax(0,1fr) auto;gap:16px;align-items:center;padding:16px;border:1px solid {{NewTabModeBorderCss()}};border-radius:10px;background:{{NewTabPersonalizationInviteSurfaceCss()}};box-shadow:0 16px 38px rgba(0,0,0,.16)}
        .invite-eyebrow{font-size:12px;color:{{NewTabModeMutedCss()}};margin-bottom:3px}
        .invite-title{font-size:19px;font-weight:650;color:{{NewTabLogoTextColorCss()}}}
        .invite-text{font-size:13px;color:{{NewTabModeMutedCss()}};line-height:1.38;margin-top:4px;max-width:520px}
        .invite-actions{display:flex;gap:8px;flex-wrap:wrap;justify-content:flex-end}
        .invite-button{border:1px solid {{NewTabModeBorderCss()}};border-radius:999px;background:rgba(255,255,255,.06);color:{{NewTabTextColorCss()}};font:inherit;font-size:12px;padding:8px 12px;cursor:pointer;white-space:nowrap}
        .invite-button.primary{background:{{NewTabAccentSolidCss()}};border-color:{{NewTabAccentSolidCss()}};color:#15130e;font-weight:650}
        .invite-button:hover,.invite-button:focus{transform:translateY(-1px);box-shadow:0 10px 22px rgba(0,0,0,.18)}
        {{NewTabResponsiveCss()}}
        .shortcuts{display:flex;gap:12px;flex-wrap:wrap;justify-content:flex-start;max-width:760px}
        .shortcut-card{position:relative;width:92px;min-height:88px;border-radius:8px;padding:8px 6px 7px;display:flex;flex-direction:column;align-items:center;gap:8px;color:#eee2d4;text-decoration:none;font-size:12px;border:1px solid transparent}
        .shortcut-card:hover,.shortcut-card:focus-within{background:rgba(255,255,255,.06);border-color:{{NewTabShortcutBorderCss()}}}
        .shortcut-link{display:flex;flex-direction:column;align-items:center;gap:8px;color:inherit;text-decoration:none;width:100%;min-width:0}
        .shortcut-title{width:100%;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;text-align:center}
        .shortcut-dot{width:46px;height:46px;border-radius:8px;background:#31342d;border:1px solid #48483c;display:flex;align-items:center;justify-content:center;color:#fff1df;font-size:17px;font-weight:600}
        .shortcut-card:hover .shortcut-dot{background:#373a32;border-color:#6d8e82;color:#fff}
        .shortcut-actions{position:absolute;top:3px;right:3px;display:flex;gap:2px;opacity:0;pointer-events:none}
        .shortcut-card:hover .shortcut-actions,.shortcut-card:focus-within .shortcut-actions{opacity:1;pointer-events:auto}
        .shortcut-action{width:24px;height:24px;border:0;border-radius:8px;background:rgba(0,0,0,.42);color:#fff;cursor:pointer;font-size:13px;line-height:1}
        .shortcut-action:hover{background:rgba(225,120,24,.9)}
        .add-shortcut{border:1px dashed #5b675f;background:rgba(255,255,255,.035);cursor:pointer}
        .add-shortcut .shortcut-dot{background:transparent;border-style:dashed;color:#cfc5ba}
        .add-shortcut:hover .shortcut-dot{border-color:{{NewTabAccentSolidCss()}};color:#fff}
        .minimal .accent-line,.minimal .hint{display:none}
        .minimal main{gap:22px}
        .calm .shortcut-card:hover,.calm .shortcut-card:focus-within{background:rgba(255,255,255,.045)}
        .hint{font-size:12px;color:#a7a096;margin-top:2px}
        {{NewTabTransitionCss()}}
        {{NewTabMotionCss()}}
        </style>
        </head>
        <body class="{{NewTabMarkup.HtmlAttribute(NewTabStyleClass() + " " + NewTabMotionClass() + " " + NewTabUsageModeClass())}}">
        <main>
          {{NewTabHomeContentHtml()}}
        </main>
        <script>
        function go(event){
          event.preventDefault();
          const input=document.getElementById('q');
          const value=(input.value||'').trim();
          if(!value)return;
          const web=/^https?:\/\//i.test(value);
          const host=/^[\w.-]+\.[a-z]{2,}(\/.*)?$/i.test(value) || /^localhost(:\d+)?(\/.*)?$/i.test(value);
          const search='{{NewTabMarkup.JsString(SearchUrlTemplate())}}'.replace('%s', encodeURIComponent(value));
          location.href=web?value:(host?'https://'+value:search);
        }
        function novaMessage(payload){
          try{window.chrome.webview.postMessage(JSON.stringify(payload));}catch(_){}
        }
        function addShortcut(){
          novaMessage({t:'newtab_add_shortcut'});
        }
        function editShortcut(index,title,url){
          novaMessage({t:'newtab_edit_shortcut',i:index,title:title,url:url});
        }
        function deleteShortcut(index,title){
          if(confirm('Supprimer le raccourci "'+title+'" ?')){
            novaMessage({t:'newtab_delete_shortcut',i:index});
          }
        }
        function personalizeLumora(){
          novaMessage({t:'newtab_personalize'});
        }
        function openLumoraModules(){
          novaMessage({t:'newtab_modules'});
        }
        function modeAction(action){
          novaMessage({t:'newtab_mode_action',action:action});
        }
        function dismissModeIntro(mode){
          novaMessage({t:'newtab_mode_intro_dismiss',mode:mode});
        }
        function saveModeQuickNote(mode,title){
          const draft=document.getElementById('modeDraft');
          const status=document.getElementById('modeNoteStatus');
          const content=(draft?.value||'').trim();
          if(!content){
            if(status)status.textContent='Note vide.';
            draft?.focus();
            return;
          }
          novaMessage({t:'newtab_mode_quick_note',mode:mode,title:title,content:content});
          if(status)status.textContent='Gardé dans Lumie.';
        }
        function clearSearch(){
          const q=document.getElementById('q');
          if(!q)return;
          q.value='';
          q.setAttribute('value','');
        }
        clearSearch();
        window.addEventListener('pageshow',clearSearch);
        document.addEventListener('DOMContentLoaded',clearSearch);
        </script>
        </body>
        </html>
        """;

    private string NewTabHomeContentHtml()
    {
        if (NewTabUsageMode() == "neutral")
        {
            return $$"""
              <section class="neutral-home" aria-label="Accueil neutre Lumora">
                <div class="neutral-brand" aria-label="Lumora">
                  <img class="neutral-logo" src="{{LumoraLogoDataUri()}}" alt="" aria-hidden="true">
                  <span class="neutral-name">Lumora</span>
                </div>
                <time class="neutral-clock" datetime="{{DateTime.Now:HH\:mm}}" aria-label="Heure locale">{{DateTime.Now:HH:mm}}</time>
                {{NewTabSearchFormHtml("neutral-search")}}
                {{NewTabShortcutsHtml()}}
              </section>
            """;
        }

        if (NewTabUsageMode() == "balanced")
        {
            return $$"""
              <section class="balanced-home" aria-label="Accueil quotidien Lumora">
                <div class="balanced-lead">
                  <div class="balanced-brand">
                    <img class="balanced-logo" src="{{LumoraLogoDataUri()}}" alt="" aria-hidden="true">
                    <div>
                      <div class="balanced-title">{{NewTabMarkup.HtmlText(_uiSettings.NewTabTitle)}}</div>
                      <div class="balanced-greeting">{{NewTabMarkup.HtmlText(NewTabGreeting())}}</div>
                    </div>
                  </div>
                  {{NewTabSearchFormHtml()}}
                  {{NewTabShortcutsHtml()}}
                </div>
                <div class="balanced-dock">
                  <section class="mode-panel balanced-mode-panel" aria-label="Mode Lumora">
                    <div class="mode-copy">
                      <div class="mode-eyebrow">Navigation quotidienne</div>
                      <div class="mode-title">{{NewTabMarkup.HtmlText(NewTabUsageModeTitle())}}</div>
                      <div class="mode-text">{{NewTabMarkup.HtmlText(NewTabUsageModeText())}}</div>
                    </div>
                    <div class="mode-visual" aria-hidden="true"><span></span><span></span><span></span><span></span></div>
                    <div class="mode-actions">
                      {{NewTabUsageModeActionsHtml()}}
                    </div>
                  </section>
                  <section class="balanced-card" aria-label="Actions Lumora">
                    <div class="balanced-card-title">Aujourd'hui</div>
                    <div class="balanced-actions">
                      <button class="balanced-action" type="button" onclick="modeAction('bookmarks')"><strong>Favoris</strong><span>Retrouver les pages gardées localement.</span></button>
                      <button class="balanced-action" type="button" onclick="modeAction('history')"><strong>Historique</strong><span>Reprendre une navigation récente.</span></button>
                      <button class="balanced-action" type="button" onclick="modeAction('modules')"><strong>Modules</strong><span>Ajuster les outils visibles.</span></button>
                    </div>
                  </section>
                  {{NewTabPersonalizationInviteHtml()}}
                </div>
              </section>
            """;
        }

        return $$"""
          <section class="home-grid" aria-label="Accueil Lumora">
            <div class="home-primary">
              <div class="brand">
                <div class="mark">
                  <img class="logo-icon" src="{{LumoraLogoDataUri()}}" alt="" aria-hidden="true">
                  <div class="logo-name">{{NewTabMarkup.HtmlText(_uiSettings.NewTabTitle)}}</div>
                </div>
                <div class="accent-line"></div>
              </div>
              {{NewTabSearchFormHtml()}}
              {{NewTabShortcutsHtml()}}
              <p class="hint">{{Version}}</p>
            </div>
            <div class="mode-side">
              <section class="mode-panel" aria-label="Mode Lumora">
                <div class="mode-copy">
                  <div class="mode-eyebrow">{{NewTabMarkup.HtmlText(NewTabGreeting())}}</div>
                  <div class="mode-title">{{NewTabMarkup.HtmlText(NewTabUsageModeTitle())}}</div>
                  <div class="mode-text">{{NewTabMarkup.HtmlText(NewTabUsageModeText())}}</div>
                </div>
                <div class="mode-visual" aria-hidden="true"><span></span><span></span><span></span><span></span></div>
                <div class="mode-actions">
                  {{NewTabUsageModeActionsHtml()}}
                </div>
              </section>
              {{NewTabModeIntroHtml()}}
              {{NewTabModeWorkbenchHtml()}}
              {{NewTabPersonalizationInviteHtml()}}
            </div>
          </section>
        """;
    }

    private string NewTabSearchFormHtml(string extraClass = "")
    {
        var searchClass = string.IsNullOrWhiteSpace(extraClass) ? "search" : $"search {extraClass}";
        return $$"""
              <form class="{{NewTabMarkup.HtmlAttribute(searchClass)}}" onsubmit="go(event)">
                <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M9.5 3a6.5 6.5 0 0 1 5.16 10.45l4.44 4.45-1.2 1.2-4.45-4.44A6.5 6.5 0 1 1 9.5 3m0 1.7a4.8 4.8 0 1 0 0 9.6 4.8 4.8 0 0 0 0-9.6"/></svg>
                <input id="q" value="" {{(_uiSettings.NewTabFocusSearchOnOpen ? "autofocus" : "")}} autocomplete="new-password" autocapitalize="off" autocorrect="off" spellcheck="false" inputmode="search" placeholder="Rechercher ou saisir une URL">
              </form>
        """;
    }

    private string LumoraLogoDataUri()
    {
        try
        {
            foreach (var path in new[]
                     {
                         Path.Combine(AppContext.BaseDirectory, "Assets", "LumoraApp.png"),
                         Path.Combine(Environment.CurrentDirectory, "Lumora.WinUI", "Assets", "LumoraApp.png"),
                         Path.Combine(Environment.CurrentDirectory, "Assets", "LumoraApp.png")
                     })
            {
                if (File.Exists(path))
                {
                    return "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(path));
                }
            }
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Lumora logo data uri skipped: {error.GetType().Name}");
        }

        return string.Empty;
    }

    private string NewTabStyleClass() =>
        (_uiSettings.NewTabStyle ?? "signature").ToLowerInvariant() switch
        {
            "calm" => "calm",
            "minimal" => "minimal",
            _ => "signature"
        };

    private string NewTabMotionClass()
    {
        if (_uiSettings.AccessibilityReduceMotion || _uiSettings.AccessibilityHighContrast)
        {
            return "motion-static";
        }

        return (_uiSettings.PersonalizationMotionStyle ?? "luminous").ToLowerInvariant() switch
        {
            "subtle" => "motion-subtle",
            "dynamic" => "motion-dynamic",
            _ => "motion-luminous"
        };
    }

    private string NewTabUsageMode() =>
        (_uiSettings.UsageMode ?? "neutral").ToLowerInvariant() switch
        {
            "neutral" => "neutral",
            "balanced" => "balanced",
            "focus" => "focus",
            "reading" => "reading",
            "creative" => "creative",
            "research" => "research",
            "night" => "night",
            _ => "neutral"
        };

    private string NewTabUsageModeClass() => "mode-" + NewTabUsageMode();

    private string NewTabUsageModeTitle() => NewTabUsageMode() switch
    {
        "neutral" => "Mode Neutre",
        "focus" => "Mode Focus",
        "reading" => "Mode Lecture",
        "creative" => "Mode Création",
        "research" => "Mode Recherche",
        "night" => "Mode Nuit",
        _ => "Mode Équilibre"
    };

    private string NewTabUsageModeText() => NewTabUsageMode() switch
    {
        "neutral" => "L'accueil de base : l'heure, une recherche et seulement les outils essentiels du navigateur.",
        "focus" => "Une entrée compacte pour reprendre vite, limiter le bruit visuel et garder les outils essentiels à portée.",
        "reading" => "Un accueil calme pour lire, annoter, reprendre les pages longues et garder la navigation douce.",
        "creative" => "Un espace plus expressif pour ouvrir des idées, lancer des recherches et garder les notes proches.",
        "research" => "Un point de départ orienté collecte : recherche, onglets, historique local et sources à comparer.",
        "night" => "Un rythme plus doux pour naviguer tard, avec moins d'éclat et une présence visuelle plus posée.",
        _ => "Un équilibre entre confort, rapidité et repères personnels pour la navigation quotidienne."
    };

    private string NewTabGreeting()
    {
        var hour = DateTime.Now.Hour;
        var moment = hour switch
        {
            >= 5 and < 12 => "Bonjour",
            >= 12 and < 18 => "Bon après-midi",
            >= 18 and < 23 => "Bonsoir",
            _ => "Navigation nocturne"
        };

        return $"{moment} · {DateTime.Now:HH:mm}";
    }

    private string NewTabUsageModeActionsHtml()
    {
        var actions = NewTabUsageMode() switch
        {
            "balanced" => new[] { "Favoris", "Historique", "Modules" },
            "focus" => new[] { "Reprendre", "Épingler", "Ctrl+K" },
            "reading" => new[] { "Lire", "Annoter", "Calmer" },
            "creative" => new[] { "Idées", "Notes", "Explorer" },
            "research" => new[] { "Comparer", "Sources", "Historique" },
            "night" => new[] { "Douceur", "Focus", "Lecture" },
            _ => Array.Empty<string>()
        };

        var html = new StringBuilder();
        foreach (var action in actions)
        {
            html.Append("<span class=\"mode-chip\">")
                .Append(NewTabMarkup.HtmlText(action))
                .Append("</span>");
        }

        return html.ToString();
    }

    private string NewTabModeIntroHtml()
    {
        var mode = NewTabUsageMode();
        if (mode == "neutral" || mode == "balanced")
        {
            return string.Empty;
        }

        if (string.Equals(_uiSettings.LastIntroducedUsageMode, mode, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var points = NewTabModeIntroPoints(mode);
        var pointHtml = string.Join("", points.Select(point =>
            $"<span class=\"mode-intro-point\">{NewTabMarkup.HtmlText(point)}</span>"));

        return $$"""
          <section class="mode-intro" aria-label="Présentation du mode actif">
            <div>
              <div class="mode-intro-title">{{NewTabMarkup.HtmlText(NewTabUsageModeTitle())}} activé</div>
              <div class="mode-intro-text">{{NewTabMarkup.HtmlText(NewTabModeIntroText(mode))}}</div>
              <div class="mode-intro-points">{{pointHtml}}</div>
            </div>
            <button class="mode-intro-button" type="button" onclick="dismissModeIntro('{{NewTabMarkup.JsString(mode)}}')">Compris</button>
          </section>
        """;
    }

    private static string NewTabModeIntroText(string mode) =>
        mode switch
        {
            "neutral" => "Lumora se met en retrait : heure, recherche et navigation essentielle, sans compagnon affiché.",
            "balanced" => "Lumora garde les repères quotidiens visibles sans imposer un contexte spécialisé.",
            "focus" => "Lumora réduit le bruit, rapproche les commandes rapides et garde une seule priorité visible.",
            "reading" => "Lumora adoucit l'accueil, rapproche lecture, annotations et voix locale.",
            "creative" => "Lumora ouvre un espace d'idées : post-it local, notes et relance créative.",
            "research" => "Lumora privilégie la collecte locale : sources, historique, favoris et comparaison.",
            "night" => "Lumora baisse la présence visuelle et garde les actions utiles pour naviguer tard.",
            _ => "Lumora reste polyvalent : repères personnels, recherche et navigation quotidienne."
        };

    private static string[] NewTabModeIntroPoints(string mode) =>
        mode switch
        {
            "neutral" => ["Heure locale", "Recherche", "Outils essentiels"],
            "balanced" => ["Favoris", "Historique local", "Modules"],
            "focus" => ["Accueil compact", "Objectif immédiat", "Commandes rapides"],
            "reading" => ["Lecture calme", "Annotations", "Voix locale"],
            "creative" => ["Post-it local", "Notes proches", "Idées à relancer"],
            "research" => ["Sources", "Historique local", "Comparaison"],
            "night" => ["Moins d'éclat", "Lecture douce", "Actions minimales"],
            _ => ["Navigation normale", "Repères personnels", "Confort"]
        };

    private string NewTabModeWorkbenchHtml()
    {
        var mode = NewTabUsageMode();
        if (mode == "neutral")
        {
            return string.Empty;
        }

        var context = ResolveNewTabModeContext(mode);
        var actionsHtml = new StringBuilder();

        actionsHtml.AppendLine($"""
              <button class="mode-context-button primary" type="button" onclick="saveModeQuickNote('{NewTabMarkup.JsString(mode)}','{NewTabMarkup.JsString(context.NoteTitle)}')">{NewTabMarkup.HtmlText(context.SaveLabel)}</button>
        """);

        foreach (var (action, label) in context.Actions)
        {
            actionsHtml.AppendLine($"""
              <button class="mode-context-button" type="button" onclick="modeAction('{NewTabMarkup.JsString(action)}')">{NewTabMarkup.HtmlText(label)}</button>
            """);
        }

        return $$"""
          <section class="mode-context" aria-label="{{NewTabMarkup.HtmlAttribute(context.AriaLabel)}}">
            <div class="mode-context-copy">
              <div class="mode-context-eyebrow">{{NewTabMarkup.HtmlText(context.Eyebrow)}}</div>
              <div class="mode-context-title">{{NewTabMarkup.HtmlText(context.Title)}}</div>
              <div class="mode-context-text">{{NewTabMarkup.HtmlText(context.Text)}}</div>
              <textarea id="modeDraft" class="mode-draft" aria-label="{{NewTabMarkup.HtmlAttribute(context.DraftLabel)}}" placeholder="{{NewTabMarkup.HtmlAttribute(context.Placeholder)}}">{{NewTabMarkup.HtmlText(CompanionMemory(mode))}}</textarea>
              <div id="modeNoteStatus" class="mode-note-status" aria-live="polite"></div>
            </div>
            <div class="mode-context-actions">
              {{actionsHtml}}            </div>
          </section>
        """;
    }

    private static NewTabModeContext ResolveNewTabModeContext(string mode) =>
        mode switch
        {
            "focus" => new NewTabModeContext(
                "Priorité locale",
                "Objectif de maintenant",
                "Notez la seule chose à terminer. La note reste dans le profil Lumora.",
                "Ex. Finir la page de réglages, vérifier un bug, répondre à un message important...",
                "Enregistrer l'objectif",
                "Objectif Focus",
                "Saisir l'objectif du mode Focus",
                "Outil Focus",
                [("command_palette", "Ouvrir Ctrl+K"), ("fullscreen", "Plein écran")]),
            "reading" => new NewTabModeContext(
                "Lecture active",
                "Marque-page de lecture",
                "Gardez une phrase, une idée ou une page à reprendre sans quitter l'accueil.",
                "Ex. À relire : section sécurité locale, vérifier les annotations, reprendre ce passage...",
                "Garder la note",
                "Note de lecture",
                "Saisir une note de lecture",
                "Outil Lecture",
                [("reader", "Mode lecture"), ("read_aloud", "Voix locale")]),
            "creative" => new NewTabModeContext(
                "Post-it local",
                "Capture d'idée",
                "Un vrai post-it de création : une idée maintenant, enregistrée dans les Notes Lumora.",
                "Ex. Ajouter une animation au changement de mode, tester un panneau plus vivant...",
                "Coller le post-it",
                "Post-it Creation",
                "Saisir une idée créative",
                "Outil Creation",
                [("notes", "Ouvrir Notes"), ("search_assist", "Relancer l'idée"), ("add_shortcut", "Ajouter un repère")]),
            "research" => new NewTabModeContext(
                "Collecte locale",
                "Source ou piste à vérifier",
                "Notez une source, une hypothèse ou une comparaison. Lumora garde la trace localement.",
                "Ex. Comparer WebView2/CEF, vérifier une doc, garder une URL ou une question...",
                "Ajouter la piste",
                "Piste de recherche",
                "Saisir une piste de recherche",
                "Outil Recherche",
                [("history", "Historique local"), ("bookmarks", "Sources gardées"), ("command_palette", "Comparer vite")]),
            "night" => new NewTabModeContext(
                "Navigation calme",
                "Rappel pour plus tard",
                "Posez une note courte pour demain et gardez l'interface légère maintenant.",
                "Ex. Reprendre cette recherche demain, fermer les onglets inutiles, lire hors écran...",
                "Garder pour demain",
                "Rappel Nuit",
                "Saisir un rappel du mode Nuit",
                "Outil Nuit",
                [("reader", "Lecture douce"), ("read_aloud", "Écoute locale"), ("fullscreen", "Réduire l'éclat")]),
            _ => new NewTabModeContext(
                "Repère rapide",
                "Note de navigation",
                "Gardez une note simple sans transformer l'accueil en tableau de bord.",
                "Ex. Page à consulter, idée rapide, rappel lié à la navigation...",
                "Enregistrer la note",
                "Note rapide Lumora",
                "Saisir une note rapide",
                "Outil Equilibre",
                [("personalize", "Mon Lumora"), ("modules", "Modules")])
        };

    private bool ShouldShowNewTabPersonalizationInvite() =>
        NewTabUsageMode() != "neutral" &&
        !_uiSettings.PinnedModuleIds.Any() &&
        !_uiSettings.NewTabShortcutsVisible &&
        _uiSettings.NewTabShortcuts.Count == 0;

    private string NewTabPersonalizationInviteHtml()
    {
        if (!ShouldShowNewTabPersonalizationInvite())
        {
            return string.Empty;
        }

        return """
          <section class="personalize-invite" aria-label="Personnalisation Lumora">
            <div>
              <div class="invite-eyebrow">Profil épuré</div>
              <div class="invite-title">Construisez votre Lumora</div>
              <div class="invite-text">Aucun module ni raccourci n'est imposé. Choisissez votre mode, vos outils visibles et l'accueil qui vous met le plus à l'aise.</div>
            </div>
            <div class="invite-actions">
              <button class="invite-button primary" type="button" onclick="personalizeLumora()">Personnaliser</button>
              <button class="invite-button" type="button" onclick="openLumoraModules()">Modules</button>
            </div>
          </section>
        """;
    }

    private static string NewTabResponsiveCss() =>
        "@media(max-width:860px){main{width:min(760px,100%)}.home-grid,.balanced-home,.balanced-dock{grid-template-columns:1fr;gap:20px}.home-primary{padding-top:0}.mode-side{width:100%;justify-self:stretch}.brand{align-items:center}.mark,.balanced-brand{justify-content:center}.balanced-lead{text-align:center}.accent-line{align-self:center}.shortcuts{justify-content:center}.mode-panel,.mode-intro,.mode-context,.personalize-invite{grid-template-columns:1fr;grid-template-areas:none}.mode-copy,.mode-actions,.mode-visual{grid-area:auto}.mode-visual{width:100%;height:34px;justify-content:flex-start}.mode-actions,.invite-actions{justify-content:flex-start}.mode-context-actions{flex-direction:row;flex-wrap:wrap}.mode-context-button{flex:1 1 150px}.mode-workbench{grid-template-columns:1fr}.mode-tool{min-height:64px}.balanced-mode-panel,.balanced-wide{grid-column:auto}}";

    private string NewTabTransitionCss() =>
        _uiSettings.AccessibilityReduceMotion
            ? ".search,.mode-panel,.mode-intro,.mode-context,.mode-context-button,.balanced-action,.personalize-invite,.invite-button,.shortcut-card,.shortcut-dot,.shortcut-actions{transition:none}"
            : ".search,.mode-panel,.mode-chip,.mode-intro,.mode-context,.mode-context-button,.balanced-action,.personalize-invite,.invite-button,.shortcut-card,.shortcut-dot,.shortcut-actions{transition:background .12s ease,border-color .12s ease,box-shadow .12s ease,color .12s ease,opacity .12s ease,transform .12s ease}";

    private string NewTabMotionCss()
    {
        if (NewTabMotionClass() == "motion-static")
        {
            return """
            .motion-static::after{display:none}
            .motion-static .brand,.motion-static .balanced-brand,.motion-static .neutral-brand,.motion-static .neutral-clock,.motion-static .search,.motion-static .shortcut-card,.motion-static .logo-icon,.motion-static .balanced-logo,.motion-static .neutral-logo,.motion-static .accent-line::after,.motion-static .mode-visual span,.motion-static .mode-panel::after,.motion-static .mode-intro,.motion-static .mode-context{animation:none}
            """;
        }

        var palette = NewTabPalette();
        return """
        @keyframes lumoraRise{from{opacity:0;transform:translateY(12px)}to{opacity:1;transform:translateY(0)}}
        @keyframes lumoraShortcutIn{from{opacity:0;transform:translateY(10px) scale(.98)}to{opacity:1;transform:translateY(0) scale(1)}}
        @keyframes lumoraTrace{from{transform:translate3d(-8%,0,0) rotate(0.001deg)}to{transform:translate3d(8%,0,0) rotate(0.001deg)}}
        @keyframes lumoraBreathe{0%,100%{filter:drop-shadow(0 18px 30px rgba(0,0,0,.32)) drop-shadow(0 0 18px {Accent}22);transform:translateY(0)}50%{filter:drop-shadow(0 22px 34px rgba(0,0,0,.36)) drop-shadow(0 0 30px {Accent}42);transform:translateY(-2px)}}
        @keyframes lumoraSweep{0%,36%{transform:translateX(-110%)}68%,100%{transform:translateX(110%)}}
        @keyframes lumoraDynamicLift{0%{opacity:0;transform:translateY(18px) scale(.985)}100%{opacity:1;transform:translateY(0) scale(1)}}
        @keyframes lumoraFocusBeat{0%,100%{opacity:.48}48%,58%{opacity:1}}
        @keyframes lumoraReadFlow{0%,100%{transform:translateX(0);opacity:.62}50%{transform:translateX(5px);opacity:1}}
        @keyframes lumoraCreativeSpark{0%,100%{transform:translateY(0) scaleY(1)}45%{transform:translateY(-3px) scaleY(1.16)}}
        @keyframes lumoraResearchScan{0%{transform:translateX(-70%);opacity:.14}45%,55%{opacity:.42}100%{transform:translateX(70%);opacity:.14}}
        @keyframes lumoraNightDrift{0%,100%{opacity:.42}50%{opacity:.8}}
        .motion-subtle .brand,.motion-subtle .balanced-brand,.motion-subtle .neutral-brand{animation:lumoraRise .34s ease-out both}
        .motion-subtle .neutral-clock{animation:lumoraRise .36s .03s ease-out both}
        .motion-subtle .search{animation:lumoraRise .38s .05s ease-out both}
        .motion-subtle .shortcut-card{animation:lumoraShortcutIn .28s ease-out both;animation-delay:calc(var(--d,0ms) + 90ms)}
        .motion-luminous::after{animation:lumoraTrace 18s ease-in-out infinite alternate}
        .motion-luminous .brand,.motion-luminous .balanced-brand,.motion-luminous .neutral-brand{animation:lumoraRise .44s cubic-bezier(.2,.8,.2,1) both}
        .motion-luminous .neutral-clock{animation:lumoraRise .46s .05s cubic-bezier(.2,.8,.2,1) both}
        .motion-luminous .search{animation:lumoraRise .46s .08s cubic-bezier(.2,.8,.2,1) both}
        .motion-luminous .shortcut-card{animation:lumoraShortcutIn .34s cubic-bezier(.2,.8,.2,1) both;animation-delay:calc(var(--d,0ms) + 130ms)}
        .motion-luminous .logo-icon,.motion-luminous .balanced-logo{animation:lumoraBreathe 5.8s ease-in-out infinite}
        .motion-luminous .accent-line::after{animation:lumoraSweep 3.8s ease-in-out infinite}
        .motion-dynamic::after{animation:lumoraTrace 10s ease-in-out infinite alternate;opacity:{DynamicTraceOpacity}}
        .motion-dynamic .brand,.motion-dynamic .balanced-brand{animation:lumoraDynamicLift .5s cubic-bezier(.16,1,.3,1) both}
        .motion-dynamic .neutral-brand,.motion-dynamic .neutral-clock{animation:lumoraRise .42s ease-out both}
        .motion-dynamic .search{animation:lumoraDynamicLift .54s .08s cubic-bezier(.16,1,.3,1) both}
        .motion-dynamic .shortcut-card{animation:lumoraShortcutIn .38s cubic-bezier(.16,1,.3,1) both;animation-delay:calc(var(--d,0ms) + 150ms)}
        .motion-dynamic .logo-icon,.motion-dynamic .balanced-logo{animation:lumoraBreathe 3.9s ease-in-out infinite}
        .motion-dynamic .accent-line::after{animation:lumoraSweep 2.6s ease-in-out infinite}
        .motion-dynamic .shortcut-card:hover{transform:translateY(-2px)}
        .mode-focus.motion-subtle .mode-visual span,.mode-focus.motion-luminous .mode-visual span,.mode-focus.motion-dynamic .mode-visual span{animation:lumoraFocusBeat 1.8s steps(2,end) infinite}
        .mode-focus .mode-visual span:nth-child(2){animation-delay:.16s}
        .mode-focus .mode-visual span:nth-child(3){animation-delay:.32s}
        .mode-reading.motion-subtle .mode-visual span,.mode-reading.motion-luminous .mode-visual span,.mode-reading.motion-dynamic .mode-visual span{animation:lumoraReadFlow 5.8s ease-in-out infinite}
        .mode-reading .mode-visual span:nth-child(2){animation-delay:.35s}
        .mode-reading .mode-visual span:nth-child(3){animation-delay:.7s}
        .mode-creative.motion-luminous .mode-visual span,.mode-creative.motion-dynamic .mode-visual span{animation:lumoraCreativeSpark 2.1s ease-in-out infinite}
        .mode-creative .mode-visual span:nth-child(2){animation-delay:.18s}
        .mode-creative .mode-visual span:nth-child(4){animation-delay:.34s}
        .mode-research.motion-luminous .mode-panel::after,.mode-research.motion-dynamic .mode-panel::after{animation:lumoraResearchScan 4.2s ease-in-out infinite}
        .mode-night.motion-subtle .mode-visual span,.mode-night.motion-luminous .mode-visual span,.mode-night.motion-dynamic .mode-visual span{animation:lumoraNightDrift 6.4s ease-in-out infinite}
        """
        .Replace("{Accent}", palette.Accent, StringComparison.Ordinal)
        .Replace("{DynamicTraceOpacity}", NewTabDynamicTraceOpacityCss(), StringComparison.Ordinal);
    }

    private (string Accent, string Accent2, string Ink, string Surface, string Text, string LogoText) NewTabPalette()
    {
        if (_uiSettings.AccessibilityHighContrast)
        {
            return ("#ffd500", "#00ffe2", "#000000", "#ffffff", "#ffffff", "#ffffff");
        }

        return (_uiSettings.AccentPalette ?? "lumora").ToLowerInvariant() switch
        {
            "ocean" => ("#5cbcff", "#89e2d6", "#14242b", "#f2fbff", "#f3fbff", "#f3fbff"),
            "forest" => ("#80cc7c", "#56d0c2", "#1a241c", "#f4fbef", "#f5faef", "#f5faef"),
            "ember" => ("#eb7e4a", "#f6ce68", "#29201c", "#fff6ee", "#fff5eb", "#fff5eb"),
            _ => ("#ffb935", "#43dbd1", "#0d1822", "#f8fbf8", "#fff8ea", "#fff8ea")
        };
    }

    private string NewTabPageBackgroundCss()
    {
        if (_uiSettings.AccessibilityHighContrast) return "#000";
        var palette = NewTabPalette();
        return palette.Ink;
    }

    private string NewTabTextColorCss() => NewTabPalette().Text;

    private string NewTabLogoTextColorCss() => NewTabPalette().LogoText;

    private string NewTabModeSurfaceCss()
    {
        if (_uiSettings.AccessibilityHighContrast) return "#000";
        return NewTabUsageMode() switch
        {
            "neutral" => "rgba(255,255,255,.035)",
            "focus" => "rgba(255,255,255,.055)",
            "reading" => "rgba(255,248,234,.065)",
            "creative" => "rgba(255,255,255,.06)",
            "research" => "rgba(67,219,209,.07)",
            "night" => "rgba(8,12,22,.72)",
            _ => "rgba(255,255,255,.052)"
        };
    }

    private string NewTabModeSignatureBarCss()
    {
        if (_uiSettings.AccessibilityHighContrast) return "#ffd500";
        var palette = NewTabPalette();
        return NewTabUsageMode() switch
        {
            "neutral" => $"linear-gradient(180deg,{palette.Accent2},{palette.Accent})",
            "focus" => $"linear-gradient(180deg,{palette.Accent},#ffffff)",
            "reading" => "linear-gradient(180deg,#f8d49b,#fff5de)",
            "creative" => $"linear-gradient(180deg,{palette.Accent2},{palette.Accent},#ff7f35)",
            "research" => $"repeating-linear-gradient(180deg,{palette.Accent2} 0 8px,transparent 8px 13px)",
            "night" => "linear-gradient(180deg,#a7c3ff,#536aa3)",
            _ => $"linear-gradient(180deg,{palette.Accent},{palette.Accent2})"
        };
    }

    private string NewTabModePanelPatternCss()
    {
        if (_uiSettings.AccessibilityHighContrast) return "none";
        var palette = NewTabPalette();
        return NewTabUsageMode() switch
        {
            "neutral" => "linear-gradient(90deg,rgba(255,255,255,.035),transparent 38%)",
            "focus" => "linear-gradient(90deg,rgba(255,255,255,.08),transparent 44%)",
            "reading" => "repeating-linear-gradient(180deg,transparent 0 18px,rgba(255,248,234,.055) 18px 19px)",
            "creative" => $"linear-gradient(135deg,{palette.Accent2}18 0 20%,transparent 20% 42%,{palette.Accent}12 42% 58%,transparent 58%),linear-gradient(90deg,transparent,{palette.Accent}10)",
            "research" => $"linear-gradient(90deg,{palette.Accent2}16 1px,transparent 1px),linear-gradient(180deg,{palette.Accent2}10 1px,transparent 1px)",
            "night" => "linear-gradient(180deg,rgba(160,190,255,.08),transparent 48%)",
            _ => "linear-gradient(90deg,rgba(255,255,255,.05),transparent 42%)"
        };
    }

    private string NewTabModeVisualCss()
    {
        var palette = NewTabPalette();
        return NewTabUsageMode() switch
        {
            "neutral" => palette.Accent,
            "reading" => "#f8d49b",
            "night" => "#a7c3ff",
            _ => palette.Accent2
        };
    }

    private string NewTabModeVisualGlowCss() => NewTabModeVisualCss() + "55";

    private string NewTabPersonalizationInviteSurfaceCss()
    {
        if (_uiSettings.AccessibilityHighContrast) return "#000";
        var palette = NewTabPalette();
        return $"linear-gradient(135deg,rgba(255,255,255,.06),{palette.Accent2}14)";
    }

    private string NewTabModeBorderCss()
    {
        if (_uiSettings.AccessibilityHighContrast) return "#fff";
        var palette = NewTabPalette();
        return NewTabUsageMode() switch
        {
            "neutral" => "rgba(255,255,255,.10)",
            "focus" => palette.Accent + "44",
            "creative" => palette.Accent2 + "48",
            "research" => palette.Accent2 + "58",
            "night" => "rgba(160,190,255,.24)",
            _ => "rgba(255,255,255,.14)"
        };
    }

    private string NewTabModeMutedCss() =>
        _uiSettings.AccessibilityHighContrast ? "#fff" : "rgba(255,248,234,.72)";

    private string NewTabBodyPaddingCss() =>
        NewTabUsageMode() == "neutral" ? "0 32px" : (NewTabStyleClass() == "minimal" ? "14vh 32px 32px" : "11vh 32px 32px");

    private string NewTabBackdropCss()
    {
        if (_uiSettings.AccessibilityHighContrast) return "none";
        var palette = NewTabPalette();
        var mode = NewTabUsageMode();
        if (mode == "neutral")
        {
            return $"linear-gradient(180deg,{palette.Ink} 0%,#0b1116 100%)";
        }
        if (mode == "focus")
        {
            return $"linear-gradient(90deg,{palette.Accent}18,transparent 30%),linear-gradient(180deg,{palette.Ink} 0%,#0b1118 100%)";
        }
        if (mode == "reading")
        {
            return $"repeating-linear-gradient(180deg,transparent 0 46px,{palette.Accent}0f 46px 47px),linear-gradient(180deg,#19201d 0%,#171715 100%)";
        }
        if (mode == "creative")
        {
            return $"linear-gradient(135deg,{palette.Accent2}16 0 16%,transparent 16% 42%,{palette.Accent}14 42% 60%,transparent 60%),linear-gradient(180deg,{palette.Ink} 0%,#11131c 100%)";
        }
        if (mode == "research")
        {
            return $"linear-gradient(90deg,{palette.Accent2}10 1px,transparent 1px),linear-gradient(180deg,{palette.Accent2}0d 1px,transparent 1px),linear-gradient(180deg,{palette.Ink} 0%,#0c1920 100%)";
        }
        if (mode == "night")
        {
            return "linear-gradient(90deg,rgba(160,190,255,.12),transparent 32%),linear-gradient(180deg,#080d16 0%,#04070c 100%)";
        }

        return NewTabStyleClass() switch
        {
            "calm" => $"linear-gradient(180deg,{palette.Ink} 0%,#171a1a 100%)",
            "minimal" => $"linear-gradient(180deg,{palette.Ink} 0%,#101820 100%)",
            _ => $"radial-gradient(circle at 48% 26%,{palette.Accent}33,transparent 23%),radial-gradient(circle at 72% 18%,{palette.Accent2}24,transparent 26%),radial-gradient(circle at 18% 72%,#ff7f3520,transparent 28%),linear-gradient(180deg,{palette.Ink} 0%,#101820 100%)"
        };
    }

    private string NewTabLightTraceCss()
    {
        if (_uiSettings.AccessibilityHighContrast || _uiSettings.AccessibilityReduceMotion)
        {
            return "none";
        }

        var palette = NewTabPalette();
        if (NewTabUsageMode() == "neutral")
        {
            return "none";
        }

        return $"linear-gradient(112deg,transparent 0 38%,{palette.Accent}18 46%,{palette.Accent2}24 52%,transparent 63%)";
    }

    private string NewTabLightTraceOpacityCss() =>
        NewTabMotionClass() switch
        {
            "motion-subtle" => ".18",
            "motion-dynamic" => ".42",
            "motion-static" => "0",
            _ => ".28"
        };

    private string NewTabDynamicTraceOpacityCss() => ".46";

    private string NewTabAccentLineCss()
    {
        var palette = NewTabPalette();
        return $"linear-gradient(90deg,{palette.Accent},{palette.Accent2})";
    }

    private string NewTabAccentGlowCss() => NewTabPalette().Accent + "55";

    private string NewTabFocusBorderCss() => NewTabPalette().Accent;

    private string NewTabFocusRingCss() => NewTabPalette().Accent + "33";

    private string NewTabShortcutBorderCss() => NewTabPalette().Accent + "42";

    private string NewTabAccentSolidCss() => NewTabPalette().Accent;

    private string NewTabAccent2SolidCss() => NewTabPalette().Accent2;

    private string NewTabShortcutsHtml()
    {
        var shortcuts = _uiSettings.NewTabShortcuts
            .Where(shortcut => !string.IsNullOrWhiteSpace(shortcut.Title) && !string.IsNullOrWhiteSpace(shortcut.Url))
            .Take(12)
            .ToList();

        // "Afficher les raccourcis" ne masque que la LISTE des raccourcis deja
        // enregistres. Le bouton "+" reste toujours disponible tant qu'il reste de
        // la place (<12) : un utilisateur ne doit jamais se retrouver bloque sans
        // aucun moyen d'ajouter un raccourci depuis la page (ni parce que le reglage
        // est desactive, ni parce que sa liste est actuellement vide).
        var showExisting = _uiSettings.NewTabShortcutsVisible && shortcuts.Count > 0;
        var canAddMore = shortcuts.Count < 12;
        if (!showExisting && !canAddMore)
            return string.Empty;

        var html = new StringBuilder();
        html.AppendLine("""<div class="shortcuts" aria-label="Raccourcis Lumora">""");
        if (showExisting)
        {
            for (var i = 0; i < shortcuts.Count; i++)
            {
                var shortcut = shortcuts[i];
                var title = NewTabMarkup.HtmlText(shortcut.Title);
                var url = NewTabMarkup.HtmlAttribute(NewTabMarkup.NormalizeShortcutUrl(shortcut.Url));
                var initial = NewTabMarkup.HtmlText(NewTabMarkup.ShortcutInitial(shortcut.Title));
                var jsTitle = NewTabMarkup.JsString(shortcut.Title);
                var jsUrl = NewTabMarkup.JsString(NewTabMarkup.NormalizeShortcutUrl(shortcut.Url));
                html.AppendLine($"""
                <div class="shortcut-card" style="--d:{i * 42}ms">
                  <a class="shortcut-link" href="{url}" title="{url}">
                    <span class="shortcut-dot">{initial}</span>
                    <span class="shortcut-title">{title}</span>
                  </a>
                  <div class="shortcut-actions">
                    <button class="shortcut-action" type="button" title="Modifier" onclick="editShortcut({i},'{jsTitle}','{jsUrl}')">✎</button>
                    <button class="shortcut-action" type="button" title="Supprimer" onclick="deleteShortcut({i},'{jsTitle}')">×</button>
                  </div>
                </div>
                """);
            }
        }
        if (canAddMore)
        {
            html.AppendLine($"""
            <button class="shortcut-card add-shortcut" type="button" onclick="addShortcut()" title="Ajouter un raccourci" style="--d:{shortcuts.Count * 42}ms">
              <span class="shortcut-dot">+</span>
              <span class="shortcut-title">Ajouter</span>
            </button>
            """);
        }
        html.Append("</div>");
        return html.ToString();
    }

    private string SearchUrlTemplate() => _uiSettings.SearchEngine switch
    {
        "duckduckgo" => "https://duckduckgo.com/?q=%s",
        "brave" => "https://search.brave.com/search?q=%s",
        "bing" => "https://www.bing.com/search?q=%s",
        _ => "https://www.google.com/search?q=%s"
    };

    private sealed record NewTabModeContext(
        string Eyebrow,
        string Title,
        string Text,
        string Placeholder,
        string SaveLabel,
        string NoteTitle,
        string DraftLabel,
        string AriaLabel,
        IReadOnlyList<(string Action, string Label)> Actions);
}
