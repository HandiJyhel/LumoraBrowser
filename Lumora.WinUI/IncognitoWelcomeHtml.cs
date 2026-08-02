namespace Lumora.WinUI;

// Page d'accueil de la fenetre Incognito. Extrait de LumoraIncognitoWindow.xaml.cs
// (etait une methode statique pure, WelcomeHtml, mais coincee dans une classe
// WinUI non compilable dans Lumora.Tests) pour la rendre testable. Aucun
// changement de comportement, uniquement un deplacement de code.
//
// Identite visuelle refaite le 2026-08-03 (demande explicite : "ameliorer le
// truc de bienvenue", "sortir du lot") - texte inchange mot pour mot (les 4
// phrases d'origine restent identiques), seule la mise en forme change :
// fond "aurore" anime (memes teintes que LumoraWindowIdentityBrush dans
// LumoraIncognitoWindow.xaml, violet -> turquoise), logo SVG maison a la
// place de l'emoji, pastille d'etat Tor qui reprend exactement le texte deja
// affiche dans la barre d'outils ("IP masquee : oui/non"), et les 4 phrases
// presentees en lignes iconographiees plutot qu'en paragraphes centres.
// `prefers-reduced-motion` coupe toutes les animations (aurore + entree).
internal static class IncognitoWelcomeHtml
{
    // Moteur fixe (voir IncognitoSearchEngine dans LumoraIncognitoWindow.xaml.cs,
    // meme raisonnement/date) : la barre de recherche de cette page utilise
    // TOUJOURS DuckDuckGo, jamais le moteur global (_uiSettings.SearchEngine,
    // potentiellement Google) - demande explicite utilisateur du 2026-08-01,
    // koherent avec l'adresse Incognito qui force deja ce meme moteur.
    private const string SearchEngineHost = "duckduckgo.com";

    public static string Build(bool torEnabled)
    {
        // Langue de l'interface systeme (ex. "fr"), meme calcul que
        // AddressNormalizer.SearchUrl pour que cette recherche et celle de la
        // barre d'adresse renvoient la meme region de resultats DuckDuckGo.
        var lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        return $$"""
        <!doctype html><html lang="fr"><head><meta charset="utf-8">
        <title>Incognito</title>
        <style>
        :root{
          --bg:#1a1626; --panel:#241c33; --stroke:#4d4660;
          --text:#f6efff; --muted:#c1b6d9;
          --violet:#b9a7e8; --teal:#7be4db;
          --success:#3dd688; --danger:#e5484d;
        }
        *{box-sizing:border-box}
        html,body{height:100%}
        body{font-family:'Segoe UI',system-ui,sans-serif;background:var(--bg);color:var(--text);
             display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0;
             overflow-x:hidden;position:relative}
        .aurora{position:fixed;inset:0;overflow:hidden;z-index:0;pointer-events:none}
        .aurora span{position:absolute;width:60vmax;height:60vmax;border-radius:50%;
                     filter:blur(90px);mix-blend-mode:screen}
        .aurora span:nth-child(1){background:var(--violet);opacity:.30;top:-25%;left:-15%;
                                   animation:drift1 34s ease-in-out infinite alternate}
        .aurora span:nth-child(2){background:var(--teal);opacity:.24;bottom:-30%;right:-20%;
                                   animation:drift2 41s ease-in-out infinite alternate}
        .aurora span:nth-child(3){background:var(--violet);opacity:.14;top:20%;right:10%;
                                   animation:drift3 47s ease-in-out infinite alternate}
        @keyframes drift1{from{transform:translate(0,0) scale(1)}to{transform:translate(9%,7%) scale(1.18)} }
        @keyframes drift2{from{transform:translate(0,0) scale(1)}to{transform:translate(-7%,-9%) scale(1.12)} }
        @keyframes drift3{from{transform:translate(0,0) scale(1)}to{transform:translate(-6%,8%) scale(1.2)} }
        .stage{position:relative;z-index:1;max-width:600px;padding:0 32px;text-align:center}
        .stage>*{animation:rise .5s ease both}
        .stage>*:nth-child(2){animation-delay:.05s}
        .stage>*:nth-child(3){animation-delay:.1s}
        .stage>*:nth-child(4){animation-delay:.16s}
        @keyframes rise{from{opacity:0;transform:translateY(10px)}to{opacity:1;transform:translateY(0)} }
        .mark{margin:0 auto 14px;width:52px;height:52px}
        h1{font-size:28px;font-weight:700;letter-spacing:-.01em;margin:0 0 14px}
        .status-pill{display:inline-flex;align-items:center;gap:8px;padding:6px 14px;
                      border-radius:999px;background:rgba(36,28,51,.7);border:1px solid var(--stroke);
                      font-size:12.5px;font-weight:600;margin:0 0 20px;color:var(--text)}
        .status-pill .dot{width:8px;height:8px;border-radius:50%;flex:none}
        .status-pill.on .dot{background:var(--success);box-shadow:0 0 8px rgba(61,214,136,.7)}
        .status-pill.off .dot{background:var(--danger)}
        .search{display:flex;align-items:center;gap:10px;background:rgba(36,28,51,.7);
                border:1px solid var(--stroke);border-radius:999px;padding:12px 20px;
                margin:0 auto 26px;max-width:440px;backdrop-filter:blur(6px);
                transition:border-color .15s,box-shadow .15s}
        .search:focus-within{border-color:var(--violet);
                              box-shadow:0 0 0 4px rgba(185,167,232,.18),0 0 26px rgba(123,228,219,.14)}
        .search svg{width:18px;height:18px;color:var(--violet);flex:none}
        .search input{flex:1;min-width:0;border:0;background:transparent;color:var(--text);
                       font:inherit;font-size:15px;outline:0}
        .search input::placeholder{color:var(--muted);opacity:.75}
        .facts{display:flex;flex-direction:column;gap:10px;text-align:left}
        .fact{display:flex;gap:12px;align-items:flex-start;padding:13px 16px;
              background:rgba(36,28,51,.5);border:1px solid rgba(77,70,96,.55);
              border-radius:14px;backdrop-filter:blur(6px)}
        .fact svg{flex:none;width:17px;height:17px;margin-top:2px;color:var(--violet)}
        .fact.masked svg{color:var(--success)}
        .fact.unmasked svg{color:var(--danger)}
        .fact p{margin:0;font-size:13.5px;line-height:1.55;opacity:.82}
        .fact.claim p{opacity:1;font-weight:600;color:#fff8ea}
        @media (prefers-reduced-motion: reduce){
          .aurora span{animation:none!important}
          .stage>*{animation:none!important}
        }
        </style></head><body>
        <div class="aurora" aria-hidden="true"><span></span><span></span><span></span></div>
        <div class="stage">
        <svg class="mark" viewBox="0 0 48 48" fill="none" aria-hidden="true">
          <defs><linearGradient id="markGrad" x1="0" y1="0" x2="48" y2="48">
            <stop stop-color="#b9a7e8"/><stop offset="1" stop-color="#7be4db"/>
          </linearGradient></defs>
          <path d="M24 9c-10 0-18.3 5.7-22 13 3.7 7.3 12 13 22 13s18.3-5.7 22-13c-3.7-7.3-12-13-22-13Z"
                stroke="url(#markGrad)" stroke-width="2.6" stroke-linejoin="round"/>
          <circle cx="15.5" cy="22" r="3.6" fill="url(#markGrad)"/>
          <circle cx="32.5" cy="22" r="3.6" fill="url(#markGrad)"/>
        </svg>
        <h1>Incognito</h1>
        <span class="status-pill {{(torEnabled ? "on" : "off")}}">
          <span class="dot" aria-hidden="true"></span>
          IP masquée : {{(torEnabled ? "oui" : "non")}}
        </span>
        <form class="search" onsubmit="go(event)">
          <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M9.5 3a6.5 6.5 0 0 1 5.16 10.45l4.44 4.45-1.2 1.2-4.45-4.44A6.5 6.5 0 1 1 9.5 3m0 1.7a4.8 4.8 0 1 0 0 9.6 4.8 4.8 0 0 0 0-9.6"/></svg>
          <input id="q" autofocus autocomplete="new-password" autocapitalize="off" autocorrect="off" spellcheck="false" inputmode="search" aria-label="Rechercher avec DuckDuckGo (Incognito)" placeholder="Rechercher avec DuckDuckGo (Incognito)">
        </form>
        <div class="facts">
          <div class="fact claim">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M12 2 4 5v6c0 5.2 3.4 9.9 8 11 4.6-1.1 8-5.8 8-11V5l-8-3Zm0 2.2 6 2.25V11c0 4.1-2.6 8-6 8.9V4.2Z"/></svg>
            <p>Cette session ne sera jamais sauvegardée : à la fermeture de cette fenêtre, rien n'est écrit dans l'historique, le coffre, les favoris ou le disque - cookies, cache et stockage restent dans un dossier temporaire supprimé à la fermeture.</p>
          </div>
          <div class="fact">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10 10-4.5 10-10S17.5 2 12 2Zm7.9 9h-3.95a15.6 15.6 0 0 0-1.13-5.4A8.02 8.02 0 0 1 19.9 11ZM12 4.06c.9 1.2 1.94 3.3 2.13 6.94H9.87c.19-3.64 1.23-5.74 2.13-6.94ZM9.18 5.6A15.6 15.6 0 0 0 8.05 11H4.1a8.02 8.02 0 0 1 5.08-5.4ZM4.1 13h3.95c.12 2.1.55 3.98 1.13 5.4A8.02 8.02 0 0 1 4.1 13Zm7.9 6.94c-.9-1.2-1.94-3.3-2.13-6.94h4.26c-.19 3.64-1.23 5.74-2.13 6.94Zm2.82-1.54c.58-1.42 1.01-3.3 1.13-5.4h3.95a8.02 8.02 0 0 1-5.08 5.4Z"/></svg>
            <p>Ça protège votre historique local, pas votre trafic réseau : sans Tor, votre fournisseur d'accès et les sites visités voient toujours votre adresse IP réelle, comme en navigation normale.</p>
          </div>
          <div class="fact claim {{(torEnabled ? "masked" : "unmasked")}}">
            {{(torEnabled
                ? "<svg viewBox=\"0 0 24 24\" aria-hidden=\"true\"><path fill=\"currentColor\" d=\"M12 1 3 5v6c0 5.5 3.8 10.7 9 12 5.2-1.3 9-6.5 9-12V5l-9-4Zm-1.1 14.3-3.2-3.2 1.4-1.4 1.8 1.8 4.8-4.8 1.4 1.4-6.2 6.2Z\"/></svg>"
                : "<svg viewBox=\"0 0 24 24\" aria-hidden=\"true\"><path fill=\"currentColor\" d=\"M12 5c-5.5 0-10 4.3-11.6 7 1.6 2.7 6.1 7 11.6 7s10-4.3 11.6-7C22 9.3 17.5 5 12 5Zm0 11.5A4.5 4.5 0 1 1 12 7.5a4.5 4.5 0 0 1 0 9Zm0-7.2a2.7 2.7 0 1 0 0 5.4 2.7 2.7 0 0 0 0-5.4Z\"/></svg>")}}
            <p>{{(torEnabled
                ? "Tor est actif : votre adresse IP réelle est masquée, elle n'est visible ni du site visité ni d'un relais Tor unique. En échange, la navigation est plus lente."
                : "Votre adresse IP réelle N'est PAS masquée : le site visité et votre réseau la voient normalement. Le bouton Tor (en haut à droite) la masque en faisant transiter votre trafic par le réseau Tor, au prix d'une navigation plus lente - désactivé par défaut car tout le monde n'a pas besoin de cette protection réseau en plus de la confidentialité locale.")}}</p>
          </div>
          <div class="fact">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M12 3a1 1 0 0 1 1 1v9.6l3-3 1.4 1.4-5.4 5.4-5.4-5.4L7.9 10.6l3 3V4a1 1 0 0 1 1-1ZM5 19h14v2H5v-2Z"/></svg>
            <p>Les fichiers que vous téléchargez volontairement sont, eux, conservés sur le disque.</p>
          </div>
        </div>
        </div>
        <script>
        function go(event){
          event.preventDefault();
          const input=document.getElementById('q');
          const value=(input.value||'').trim();
          if(!value)return;
          const web=/^https?:\/\//i.test(value);
          const host=/^[\w.-]+\.[a-z]{2,}(\/.*)?$/i.test(value) || /^localhost(:\d+)?(\/.*)?$/i.test(value);
          const search='https://{{SearchEngineHost}}/?q='+encodeURIComponent(value)+'&kl={{lang}}-{{lang}}';
          location.href = web ? value : (host ? 'https://'+value : search);
        }
        </script>
        </body></html>
        """;
    }
}
