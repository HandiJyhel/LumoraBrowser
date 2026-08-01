namespace Lumora.WinUI;

// Page d'accueil de la fenetre Incognito. Extrait de LumoraIncognitoWindow.xaml.cs
// (etait une methode statique pure, WelcomeHtml, mais coincee dans une classe
// WinUI non compilable dans Lumora.Tests) pour la rendre testable. Aucun
// changement de comportement, uniquement un deplacement de code.
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
        body{font-family:'Segoe UI',system-ui,sans-serif;background:radial-gradient(circle at 50% 28%,#8e7bd633,transparent 25%),linear-gradient(180deg,#17131f,#1c1628);color:#fff8ea;
             display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0}
        .card{max-width:560px;padding:0 32px;text-align:center}
        h1{font-size:26px;font-weight:600;margin:0 0 14px}
        p{opacity:.78;line-height:1.55;margin:0 0 10px}
        .badge{font-size:40px;margin-bottom:18px;color:#8e7bd6}
        .claim{font-weight:600;opacity:1}
        .search{display:flex;align-items:center;gap:10px;background:#241c33;border:1px solid #4d4660;border-radius:999px;padding:10px 18px;margin:0 auto 24px;max-width:420px}
        .search:focus-within{border-color:#8e7bd6;box-shadow:0 0 0 3px rgba(142,123,214,.25)}
        .search svg{width:18px;height:18px;color:#8e7bd6;flex:none}
        .search input{flex:1;min-width:0;border:0;background:transparent;color:#fff8ea;font:inherit;font-size:15px;outline:0}
        .search input::placeholder{color:#c1b6d9;opacity:.75}
        </style></head><body><div class="card">
        <div class="badge">&#128373;&#65039;</div>
        <h1>Incognito</h1>
        <form class="search" onsubmit="go(event)">
          <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M9.5 3a6.5 6.5 0 0 1 5.16 10.45l4.44 4.45-1.2 1.2-4.45-4.44A6.5 6.5 0 1 1 9.5 3m0 1.7a4.8 4.8 0 1 0 0 9.6 4.8 4.8 0 0 0 0-9.6"/></svg>
          <input id="q" autofocus autocomplete="new-password" autocapitalize="off" autocorrect="off" spellcheck="false" inputmode="search" aria-label="Rechercher avec DuckDuckGo (Incognito)" placeholder="Rechercher avec DuckDuckGo (Incognito)">
        </form>
        <p class="claim">Cette session ne sera jamais sauvegardée : à la fermeture de cette
        fenêtre, rien n'est écrit dans l'historique, le coffre, les favoris ou le disque
        - cookies, cache et stockage restent dans un dossier temporaire supprimé à la
        fermeture.</p>
        <p>Ça protège votre historique local, pas votre trafic réseau : sans Tor,
        votre fournisseur d'accès et les sites visités voient toujours votre adresse
        IP réelle, comme en navigation normale.</p>
        <p class="claim">{{(torEnabled
            ? "Tor est actif : votre adresse IP réelle est masquée, elle n'est visible ni du site visité ni d'un relais Tor unique. En échange, la navigation est plus lente."
            : "Votre adresse IP réelle N'est PAS masquée : le site visité et votre réseau la voient normalement. Le bouton Tor (en haut à droite) la masque en faisant transiter votre trafic par le réseau Tor, au prix d'une navigation plus lente - désactivé par défaut car tout le monde n'a pas besoin de cette protection réseau en plus de la confidentialité locale.")}}</p>
        <p>Les fichiers que vous téléchargez volontairement sont, eux, conservés sur le disque.</p>
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
