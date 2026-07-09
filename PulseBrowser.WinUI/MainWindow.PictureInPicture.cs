using Microsoft.UI.Xaml;
using System.Text.Json.Nodes;

namespace PulseBrowser.WinUI;

// Lecteur vidéo flottant : détache la vidéo active de la page dans une petite
// fenêtre système toujours au premier plan (API standard Picture-in-Picture du
// moteur Chromium), sans script en tâche de fond sur chaque page — l'action ne
// s'exécute qu'à la demande explicite de l'utilisateur.
public sealed partial class MainWindow
{
    private async void DetachVideoMenu_Click(object sender, RoutedEventArgs e)
    {
        var core = _browserView?.CoreWebView2;
        if (core is null)
        {
            StatusText.Text = "Moteur web indisponible.";
            return;
        }

        try
        {
            var resultJson = await core.ExecuteScriptAsync(RequestPictureInPictureScript);
            StatusText.Text = ParsePictureInPictureResult(resultJson);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Picture-in-Picture impossible : {ex.Message}";
        }
    }

    private static string ParsePictureInPictureResult(string rawJson)
    {
        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node is JsonValue value && value.TryGetValue<string>(out var nested))
            {
                node = JsonNode.Parse(nested);
            }

            if (node is JsonObject obj && obj["message"] is JsonValue messageValue &&
                messageValue.TryGetValue<string>(out var message))
            {
                return message;
            }
        }
        catch { }

        return "Picture-in-Picture : reponse inattendue de la page.";
    }

    // Cible la vidéo la plus probablement « active » : en lecture en priorité,
    // sinon la plus grande vidéo visible de la page. Ne fonctionne que dans le
    // document principal : une vidéo dans un iframe cross-origin (ex. lecteur
    // intégré tiers) reste inaccessible au script, limite connue documentée.
    // Script asynchrone (WebView2 attend la promesse) : requestPictureInPicture
    // exige un geste utilisateur côté page ; un rejet du moteur remonte ici tel
    // quel plutôt que d'être masqué par un faux succès.
    private const string RequestPictureInPictureScript = """
        (async function(){
            try{
                if(!document.pictureInPictureEnabled){
                    return JSON.stringify({success:false,message:'Picture-in-Picture non pris en charge sur cette page.'});
                }
                var videos=Array.prototype.slice.call(document.querySelectorAll('video'))
                    .filter(function(v){return !v.disablePictureInPicture;});
                if(videos.length===0){
                    return JSON.stringify({success:false,message:'Aucune video trouvee sur cette page.'});
                }
                var playing=videos.filter(function(v){return !v.paused && !v.ended;});
                var target=(playing[0])||videos.sort(function(a,b){
                    return (b.videoWidth*b.videoHeight)-(a.videoWidth*a.videoHeight);
                })[0];
                if(document.pictureInPictureElement){
                    await document.exitPictureInPicture();
                }
                await target.requestPictureInPicture();
                return JSON.stringify({success:true,message:'Video detachee en Picture-in-Picture.'});
            }catch(err){
                var detail=(err&&err.message)?err.message:String(err);
                return JSON.stringify({success:false,message:'Picture-in-Picture refuse : '+detail});
            }
        })();
        """;
}
