# 2026-07-18 - Navigation anonyme (Tor), architecture (0.83.20-dev)

Poursuite du masquage d'IP entame avec l'anti-fuite WebRTC (0.83.18-dev).
Increment scope volontairement a l'architecture seule : fenetre dediee,
gestion reelle du processus Tor, wiring du proxy WebView2 - mais sans
cabler le telechargement du moteur, qui reste une etape separee necessitant
un Go explicite avant toute action reseau.

- Version passee a `0.83.20-dev`.
- Nouvelle entree de menu "Navigation anonyme (Tor)" (les deux copies du
  menu, barre haute et barre basse), a cote de "Nouvelle fenetre privee".
- Nouveau `MainWindow.TorBrowsing.cs` : ouverture de la fenetre, meme garde
  que la navigation privee (pas d'ouverture avant profil/session actifs).
- Nouvelle fenetre `LumoraTorWindow` (xaml + code-behind), independante de
  `MainWindow`, chrome minimal + protections reseau habituelles.
- Nouveau `Lumora.WinUI/Tor/TorProcessManager.cs` : gestion reelle (pas un
  stub) du cycle de vie d'un `tor.exe` s'il existe deja dans
  `<profil>/tor/tor.exe` - demarrage avec `--SocksPort`/`--DataDirectory`,
  suivi du bootstrap via les lignes de log, arret propre.
- Ecran d'etat honnete tant que le moteur n'est pas installe : explication,
  statut, bouton "Installer le moteur Tor" qui indique clairement que le
  telechargement n'est pas encore cable (aucune fausse promesse, aucune
  action reseau).
- Une fois `Connected`, creation d'un environnement WebView2 dedie (dossier
  de donnees separe -> processus Chromium distinct) avec
  `--proxy-server=socks5://127.0.0.1:<port>` : seule cette fenetre est
  affectee, jamais le reste de la navigation.
- Mise a jour du test de garde-fou d'alignement de version vers
  `0.83.20-dev` (`Lumora.Tests/UsageModeVisualIdentityTests.cs`,
  `AGENTS.md`, `scripts/build-clean-test-artifact.ps1`,
  `scripts/build-installer.ps1`).
- Documentation ajoutee : `docs/NAVIGATION_ANONYME_TOR_0_83_20.md`.
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora en dehors de la verification.

**Verification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 517/517
  tests reussis.
- Build WinUI MSBuild x64 (Debug) : 0 avertissement, 0 erreur.
- Verification en conditions reelles : ouverture du menu, clic "Navigation
  anonyme (Tor)" - fenetre dediee ouverte, statut "Moteur Tor non installe."
  confirme visuellement, clic sur "Installer le moteur Tor" affiche le
  message honnete attendu, aucune exception dans la trace runtime.

**Version :** `0.83.20-dev`.
