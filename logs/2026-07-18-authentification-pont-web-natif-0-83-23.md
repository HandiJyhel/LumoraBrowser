# 2026-07-18 - Authentification du pont web<->natif (0.83.23-dev)

Suite a un audit complet du projet demande par l'utilisateur (multi-agents,
axes securite/vie privee, coffre, architecture, tests/build), le point
bloquant le plus prioritaire a ete traite en premier : le pont
`BrowserCore_WebMessageReceived`, partage par tous les onglets y compris
les sites web arbitraires, ne verifiait l'origine d'aucun message.

## Failles corrigees

1. **Usurpation du carnet de passkeys.** Le script injecte par
   `RegisterPasskeyMonitorAsync` envoie `{t:'passkey_created', o:
   location.origin}` a chaque creation/utilisation reelle de passkey.
   Mais rien n'empechait une page malveillante d'appeler directement
   `window.chrome.webview.postMessage(...)` avec un champ `o` mensonger,
   sans passer par ce script - le carnet de passkeys local aurait alors
   enregistre une origine forgee. Corrige en derivant l'origine de
   `CoreWebView2WebMessageReceivedEventArgs.Source` (attestee par
   WebView2, jamais lue dans le payload JSON controle par la page).

2. **Falsification des messages `newtab_*`.** Les messages qui pilotent
   l'accueil interne de Lumora (ajout/suppression de raccourcis, memoire
   du compagnon Lumie, ouverture des panneaux Modules/Personnalisation)
   n'etaient rejetes pour aucun onglet : un site web ouvert dans un onglet
   normal pouvait en theorie les emettre lui-meme et, par exemple,
   supprimer silencieusement un raccourci de l'utilisateur ou injecter une
   fausse note dans la memoire du compagnon. Corrige en verifiant que
   l'onglet emetteur (`TabForCore(sender as CoreWebView2)`) a bien pour
   adresse logique `lumora://accueil`, la page interne de Lumora.

## Perimetre volontairement laisse de cote

Les autres types de messages (`nova.loginDiagnostic`, `nova.payment.form`,
`nova.fullscreenExit`, `lumora.annotation`) sont concus par nature pour
venir de n'importe quel site visite (detection de formulaire de paiement,
sortie de plein ecran, diagnostic de connexion, annotations de lecture) et
ne persistent aucune donnee sensible sans une action explicite de
l'utilisateur : le portefeuille exige un deverrouillage manuel avant tout
remplissage, le diagnostic de connexion reste en memoire non persistee.
Pas de changement necessaire pour cette premiere passe.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 517/517
  tests reussis (aucun test dedie a ce pont pour l'instant - couverture a
  ajouter separement, deja identifie dans l'audit).
- Build WinUI MSBuild x64 (Debug) : 0 avertissement, 0 erreur.

**Version :** `0.83.23-dev`.
