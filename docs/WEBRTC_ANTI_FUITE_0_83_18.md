# Anti-fuite WebRTC - 0.83.18-dev

## Objectif

A la suite d'une discussion sur les VPN et l'anonymat reel (l'idee d'un VPN
"maison" a ete ecartee : ca reviendrait a operer une infrastructure de
relais avec les couts, la responsabilite juridique et l'exigence de
diversite d'operateurs que ca implique - un projet d'entreprise, pas une
fonctionnalite de navigateur), traiter d'abord la fuite la plus concrete et
la plus a portee : WebRTC peut reveler l'IP locale/publique reelle d'un
utilisateur meme derriere un vrai VPN, si le navigateur ne s'en protege pas.

## Comportement

- Nouveau reglage `WebRtcLeakProtectionEnabled` dans `UiSettings`, actif par
  defaut (protection, pas confort - meme logique que le bloqueur de pub ou
  le forçage HTTPS).
- Nouveau `ToggleSwitch` "Bloquer les fuites d'IP WebRTC" dans
  Reglages > Confidentialite, section "WebRTC".
- Applique via l'argument Chromium
  `--force-webrtc-ip-handling-policy=disable_non_proxied_udp`, ajoute a
  `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` dans `WebView2Bootstrap.ConfigureOnce`.
- Contrainte technique assumee : ce drapeau est fige au demarrage du moteur
  WebView2, donc un changement de reglage ne prend effet qu'au prochain
  redemarrage de Lumora (message explicite affiche a l'utilisateur au
  moment du bascule).
- `UiSettings` est desormais charge AVANT `WebView2Bootstrap.ConfigureOnce`
  dans le constructeur de `MainWindow` (auparavant charge plus tard) : le
  reglage doit etre connu avant que le moteur WebView2 ne demarre pour de
  bon, sans quoi il ne pourrait jamais influencer cette session.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 517/517
  tests reussis (y compris le test de garde-fou d'alignement de version, mis
  a jour vers `0.83.18-dev`).
- Build WinUI MSBuild x64 (Debug) : 0 avertissement, 0 erreur.
- Verification en conditions reelles : lancement isole, navigation, puis
  inspection directe du processus navigateur reel de WebView2
  (`msedgewebview2.exe`, celui portant `--webview-exe-name=Lumora.WinUI.exe`)
  via WMI - ligne de commande confirmee contenant
  `--force-webrtc-ip-handling-policy=disable_non_proxied_udp`.

## Notes

- `dotnet build` seul reste inadapte pour WinUI sur cette machine (tache AppX
  `ExpandPriContent` absente du SDK .NET courant) : build via MSBuild Visual
  Studio x64.
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora en dehors de la verification.
