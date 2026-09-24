using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;

namespace Lumora.WinUI;

// ── Vrai lien cliqué → nouvel onglet toujours autorisé ───────────────────────
// Retour utilisateur (2026-09-24) : cliquer un lien qui s'ouvre dans un nouvel
// onglet vers un autre site demandait d'« autoriser la popup ». Le bloqueur ne
// savait pas distinguer un vrai lien d'un clic détourné par script (les deux
// arrivent comme IsUserInitiated). Signal ajouté : au clic (événement de
// confiance uniquement), la page signale l'adresse du <a href> cliqué ; si la
// fenêtre demandée juste après a exactement cette adresse, c'est le lien
// lui-même, pas un script publicitaire qui ouvre autre chose (voir
// PopupPolicy, isClickedLinkTarget). Partagé par MainWindow (clé = ID
// d'onglet) et les fenêtres d'application web (une seule clé).
//
// Sens du signal : la page POUSSE au moment du clic. Interroger la page depuis
// NewWindowRequested (ExecuteScriptAsync) risquerait un interblocage, le
// moteur de rendu attendant lui-même la réponse à sa demande de fenêtre.
// L'ordre d'arrivée des deux événements n'étant pas garanti (piège WebView2
// déjà rencontré), NewWindowRequested patiente un court instant si le signal
// n'est pas encore là.
//
// Jeton : un script du site pourrait sinon poster lui-même un faux « clic » sur
// l'adresse de sa pub avant de l'ouvrir. Le jeton n'existe que dans la
// fermeture du script injecté (exécuté avant tout script de la page), avec une
// référence à postMessage capturée au même moment : invisible pour la page.
internal sealed class LinkClickSignal
{
    public const string MessageType = "lumora.linkClick";

    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan WaitBudget = TimeSpan.FromMilliseconds(300);

    private readonly string _token = Guid.NewGuid().ToString("N");
    private readonly Dictionary<int, (string Href, DateTime AtUtc)> _lastClickByKey = new();

    public async Task RegisterAsync(CoreWebView2 core)
    {
        try
        {
            await core.AddScriptToExecuteOnDocumentCreatedAsync($$"""
                (function(){
                    if(window.__lumora_link_click_monitor)return;
                    window.__lumora_link_click_monitor=true;
                    var wv=window.chrome&&window.chrome.webview;
                    if(!wv)return;
                    var post=wv.postMessage.bind(wv);
                    var token='{{_token}}';
                    // Tout ce qui est lu au moment du clic est capture MAINTENANT,
                    // avant tout script de la page : sinon un site pouvait
                    // redefinir Element.prototype.closest, le getter href ou
                    // Function.prototype.call pour faire signaler l'adresse de sa
                    // pub comme "lien reellement clique" (relecture 2026-09-24).
                    // isTrusted est une propriete propre non falsifiable.
                    var uncurry=function(f){return Function.prototype.call.bind(f);};
                    var desc=Object.getOwnPropertyDescriptor;
                    var closest=uncurry(Element.prototype.closest);
                    var getTarget=uncurry(desc(Event.prototype,'target').get);
                    var getHref=uncurry(desc(HTMLAnchorElement.prototype,'href').get);
                    function note(e){
                        try{
                            if(!e.isTrusted)return;
                            var t=getTarget(e);
                            var a=closest(t,'a[href]');
                            if(!a)return;
                            var href=getHref(a);
                            if(href)post({t:'lumora.linkClick',k:token,href:href});
                        }catch(_){}
                    }
                    document.addEventListener('click',note,true);
                    document.addEventListener('auxclick',note,true);
                })();
                """);
        }
        catch { }
    }

    public void Record(int key, JsonObject message)
    {
        if (message["k"]?.GetValue<string>() != _token)
        {
            return;
        }

        var href = message["href"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(href))
        {
            _lastClickByKey[key] = (href, DateTime.UtcNow);
        }
    }

    public void Forget(int key) => _lastClickByKey.Remove(key);

    public async Task<bool> IsClickedLinkTargetAsync(int key, string? popupUri)
    {
        if (string.IsNullOrWhiteSpace(popupUri))
        {
            return false;
        }

        var waited = TimeSpan.Zero;
        var step = TimeSpan.FromMilliseconds(25);
        while (true)
        {
            if (_lastClickByKey.TryGetValue(key, out var click) &&
                DateTime.UtcNow - click.AtUtc <= MaxAge &&
                PopupPolicy.IsSameLinkAddress(click.Href, popupUri))
            {
                // Consommé : un seul onglet par clic sur un lien.
                _lastClickByKey.Remove(key);
                return true;
            }

            if (waited >= WaitBudget)
            {
                return false;
            }

            await Task.Delay(step);
            waited += step;
        }
    }
}
