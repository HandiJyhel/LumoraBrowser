# 2026-07-18 - Anti-fuite WebRTC (0.83.18-dev)

Suite a la discussion sur les VPN (l'idee d'un VPN "maison" a ete ecartee :
poser un reseau de relais soi-meme revient a lancer une entreprise
d'infrastructure - couts serveurs, gestion des abus, credibilite du no-log -
un projet a part, pas une fonctionnalite de navigateur), la piste retenue
pour cette version a ete la plus concrete et la plus a portee : bloquer la
fuite WebRTC qui revele l'IP reelle meme derriere un vrai VPN si le
navigateur ne s'en protege pas.

- Version passee a `0.83.18-dev`.
- Ajout du reglage `WebRtcLeakProtectionEnabled` dans `UiSettings`, actif par
  defaut (protection, pas confort).
- Ajout du `ToggleSwitch` "Bloquer les fuites d'IP WebRTC" dans
  Reglages > Confidentialite (section "WebRTC").
- `WebView2Bootstrap.ConfigureOnce` ajoute desormais
  `--force-webrtc-ip-handling-policy=disable_non_proxied_udp` a
  `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` quand le reglage est actif.
- Reordonnancement du constructeur de `MainWindow` : `UiSettings.Load` se
  fait maintenant AVANT `WebView2Bootstrap.ConfigureOnce`, sinon le reglage
  ne serait jamais connu a temps pour influencer le demarrage du moteur.
- Un changement de ce reglage necessite un redemarrage de Lumora pour
  s'appliquer (drapeau Chromium fige au demarrage) : message explicite
  affiche a l'utilisateur.
- Mise a jour du test de garde-fou d'alignement de version
  (`Lumora.Tests/UsageModeVisualIdentityTests.cs`) vers `0.83.18-dev`, ainsi
  que `AGENTS.md`, `scripts/build-clean-test-artifact.ps1` et
  `scripts/build-installer.ps1`.
- Documentation ajoutee : `docs/WEBRTC_ANTI_FUITE_0_83_18.md`.
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora en dehors de la verification.

**Verification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 517/517
  tests reussis.
- Build WinUI MSBuild x64 (Debug) : 0 avertissement, 0 erreur.
- Verification en conditions reelles : inspection directe (WMI) du processus
  navigateur reel de WebView2 lance par Lumora - ligne de commande confirmee
  contenant `--force-webrtc-ip-handling-policy=disable_non_proxied_udp`.

**Version :** `0.83.18-dev`.
