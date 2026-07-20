namespace Lumora.WinUI;

// Page d'accueil de la fenetre Incognito. Extrait de LumoraIncognitoWindow.xaml.cs
// (etait une methode statique pure, WelcomeHtml, mais coincee dans une classe
// WinUI non compilable dans Lumora.Tests) pour la rendre testable. Aucun
// changement de comportement, uniquement un deplacement de code.
internal static class IncognitoWelcomeHtml
{
    public static string Build(bool torEnabled) => $$"""
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
        </style></head><body><div class="card">
        <div class="badge">&#128373;&#65039;</div>
        <h1>Incognito</h1>
        <p class="claim">Cette session ne sera jamais sauvegardee : a la fermeture de cette
        fenetre, rien n'est ecrit dans l'historique, le coffre, les favoris ou le disque
        - cookies, cache et stockage restent dans un dossier temporaire supprime a la
        fermeture.</p>
        <p>Ca protege votre historique local, pas votre trafic reseau : sans Tor,
        votre fournisseur d'acces et les sites visites voient toujours votre adresse
        IP reelle, comme en navigation normale.</p>
        <p class="claim">{{(torEnabled
            ? "Tor est actif : votre adresse IP reelle est masquee, elle n'est visible ni du site visite ni d'un relais Tor unique. En echange, la navigation est plus lente."
            : "Votre adresse IP reelle N'est PAS masquee : le site visite et votre reseau la voient normalement. Le bouton Tor (en haut a droite) la masque en faisant transiter votre trafic par le reseau Tor, au prix d'une navigation plus lente - desactive par defaut car tout le monde n'a pas besoin de cette protection reseau en plus de la confidentialite locale.")}}</p>
        <p>Les fichiers que vous telechargez volontairement sont, eux, conserves sur le disque.</p>
        </div></body></html>
        """;
}
