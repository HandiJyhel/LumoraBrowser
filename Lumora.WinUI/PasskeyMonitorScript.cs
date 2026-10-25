namespace Lumora.WinUI;

// Script injecté dans chaque page : Lumora ne propose aucune passkey (pas de
// popup Windows Hello), mais la réponse aux appels WebAuthn doit rester
// conforme à la spécification. Résoudre à null n'est PAS un résultat valide
// pour un appel publicKey : les sites qui lisent ensuite le résultat plantaient
// (connexion PlayStation : « Une erreur s'est produite », corrigé en
// 0.94.32.2-dev). Un navigateur sans clé disponible se comporte ainsi :
// - saisie automatique (mediation "conditional") : promesse en attente
//   jusqu'à l'annulation par le site (AbortSignal) ;
// - appel explicite : refus NotAllowedError, comme si l'utilisateur avait
//   fermé la boîte de dialogue.
internal static class PasskeyMonitorScript
{
    internal const string Source = """
(function(){
    if(window.__nova_passkey_monitor)return;
    window.__nova_passkey_monitor=true;

    function noPasskey(options){
        var signal=options&&options.signal;
        if(options&&options.mediation==='conditional'){
            return new Promise(function(_,reject){
                if(!signal)return;
                function abort(){reject(signal.reason||new DOMException('The operation was aborted.','AbortError'));}
                if(signal.aborted){abort();return;}
                signal.addEventListener('abort',abort,{once:true});
            });
        }
        return Promise.reject(new DOMException('The operation either timed out or was not allowed.','NotAllowedError'));
    }

    var _origCreate=navigator.credentials.create.bind(navigator.credentials);
    navigator.credentials.create=function(options){
        if(options&&options.publicKey){
            return noPasskey(options);
        }
        var p=_origCreate(options);
        if(p&&typeof p.then==='function'){
            p.then(function(cred){
                if(cred){
                    try{window.chrome.webview.postMessage(JSON.stringify({t:'passkey_created',o:location.origin}));}catch(_){}
                }
            }).catch(function(){});
        }
        return p;
    };

    var _origGet=navigator.credentials.get.bind(navigator.credentials);
    navigator.credentials.get=function(options){
        if(options&&options.publicKey){
            return noPasskey(options);
        }
        var p=_origGet(options);
        if(p&&typeof p.then==='function'){
            p.then(function(cred){
                if(cred){
                    try{window.chrome.webview.postMessage(JSON.stringify({t:'passkey_used',o:location.origin}));}catch(_){}
                }
            }).catch(function(){});
        }
        return p;
    };
})();
""";
}
