# 2026-07-19 - Telechargement et installation du moteur Tor (0.83.26-dev)

Suite directe de l'etape 1 (0.83.25-dev, integrite de `tor.exe` avant
lancement). Retour utilisateur (capture d'ecran a l'appui) : sur sa machine
reelle, la bascule Tor de la fenetre Incognito affichait juste "Moteur Tor
non installe." sans aucun moyen d'agir - le trou fonctionnel documente dans
`docs/PROCHAINES_ETAPES.md` depuis l'etape 1. Cette version cable le
telechargement + l'installation reelle du moteur, jamais automatique (geste
explicite requis, regle `AGENTS.md`).

- Version passee a `0.83.26-dev`.
- `Lumora.WinUI/Tor/TorTrustedRelease.cs` etendu (pas remplace) : nouvelles
  constantes `Version` (`15.0.18`), `ArchiveFileName`, `ArchiveUrl`
  (`https://dist.torproject.org/torbrowser/15.0.18/tor-expert-bundle-windows-x86_64-15.0.18.tar.gz`)
  et `ArchiveSha256` (deja verifiees a l'etape 1 - aucune nouvelle valeur
  fabriquee, juste exposees comme constantes utilisables par le nouveau
  code de telechargement).
- Nouveau `Lumora.WinUI/Tor/TorEngineProvider.cs` : telecharge l'archive
  officielle vers un dossier de travail temporaire, verifie son SHA256
  contre `TorTrustedRelease.ArchiveSha256`, extrait uniquement l'entree
  `tor/tor.exe` (`System.Formats.Tar.TarReader` + `GZipStream`, deja dans le
  framework .NET 8 - aucune nouvelle dependance ajoutee), reverifie le
  binaire extrait contre `TorTrustedRelease.TrustedFileHashes["tor.exe"]`
  (meme controle que celui qu'utilisera ensuite
  `TorProcessManager.VerifyEngineIntegrityAsync` au lancement - redondant
  par design, jamais un fichier non verifie ne migre vers le profil), puis
  copie le fichier verifie vers `<profil>/tor/tor.exe`. Nettoyage du dossier
  de travail dans un `finally`, meme en cas d'echec.
- Decision de conception (validee avec l'utilisateur avant implementation) :
  hash epingle en dur pour l'archive, pas de verification de signature GPG
  a l'execution. Reutilise le travail deja fait a l'etape 1 (signature GPG
  reelle deja verifiee cette session sur l'archive et son fichier de sommes
  de controle) sans ajouter de dependance OpenPGP. Limite assumee et
  documentee dans `docs/PROCHAINES_ETAPES.md` : une nouvelle version Tor
  demandera de refaire cette verification a la main.
- UI (`LumoraIncognitoWindow`) : nouveau bouton `IncognitoTorInstallButton`
  ("Installer le moteur Tor", cache par defaut) a deux endroits, tous deux
  deja reserves aux echecs lies a Tor :
  - Bascule d'en-tete (`IncognitoTorSwitch_Toggled`) : rendu visible quand
    le moteur n'est pas installe, a cote du `ToggleSwitch` existant.
  - Ecran d'echec au demarrage (`ShowStartupError`, desormais parametree
    par `offerInstall: bool`) : `TextBlock` simple remplace par un
    `StackPanel` (message + bouton + statut), pour les echecs "moteur non
    installe" et "connexion Tor en echec" (qui couvre aussi un refus
    d'integrite de l'etape 1 sur un fichier deja present mais corrompu).
  - Logique d'installation factorisee dans `RunTorEngineInstallAsync`,
    partagee par les deux boutons : desactive le bouton, lance
    `TorEngineProvider.DownloadEngineAsync` avec la progression liee au
    texte de statut, puis en cas de succes rouvre une fenetre Incognito
    neuve avec Tor active (`IncognitoProcessLauncher.Launch(torEnabled: true)`)
    et ferme celle-ci - coherent avec l'architecture existante (le proxy Tor
    est fige au demarrage du process, changer d'etat ferme et rouvre
    toujours une fenetre neuve). En cas d'echec, message d'erreur explicite
    et bouton reactive pour reessayer. Aucun declenchement automatique :
    uniquement sur clic explicite.
- Nouveaux tests `Lumora.Tests/TorEngineProviderTests.cs` : extraction
  ciblee de `tor/tor.exe` depuis une archive tar.gz synthetique construite
  en memoire (`TarWriter`), ignore les entrees parasites (autre fichier,
  meme nom dans un autre dossier), renvoie `false` si l'entree est absente ;
  sanity checks sur les nouvelles constantes `TorTrustedRelease`. Comme pour
  `YtDlpEngineProviderTests`, `DownloadEngineAsync` (reseau reel) est
  volontairement exclu des tests automatises. Ajout au `Compile Include` de
  `Lumora.Tests.csproj`.
- Mise a jour du test de garde-fou d'alignement de version vers
  `0.83.26-dev` (`Lumora.Tests/UsageModeVisualIdentityTests.cs`,
  `AGENTS.md`, `scripts/build-clean-test-artifact.ps1`,
  `scripts/build-installer.ps1`).
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora en dehors de la verification.

**Verification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 528/528
  tests reussis (523 precedents + 5 nouveaux pour `TorEngineProvider`).
- Build WinUI MSBuild x64 (Debug, MSBuild Visual Studio 2022) : 0
  avertissement, 0 erreur.
- Verification en conditions reelles, bout en bout, sur un profil de test
  isole totalement vierge (aucun dossier `tor/`, jamais le profil de
  l'utilisateur), pilotage UIA par PID (fiable independamment de l'affichage
  ecran) :
  1. Lancement `--incognito-tor` : ecran d'echec affiche avec le nouveau
     bouton "Installer le moteur Tor" (texte "Moteur Tor non installe."
     confirme par lecture directe de l'arbre UIA).
  2. Clic sur le bouton (`InvokePattern`) : le process se ferme de
     lui-meme apres quelques secondes (telechargement + verification +
     installation + fermeture reussis).
  3. Nouveau process Lumora detecte automatiquement (relance via
     `IncognitoProcessLauncher.Launch(torEnabled: true)`), fenetre
     Incognito+Tor ouverte : statut final "IP masquee : oui" lu directement
     dans l'arbre UIA du nouveau process.
  4. Fichier installe verifie a la main : `Get-FileHash` sur
     `<profil>/tor/tor.exe` donne exactement
     `ec7708e0b43e0e00b1533d11ed3ca244e6f11cb2a7b62d319ad73a7b13123033` -
     identique au hash deja verifie a l'etape 1 (meme binaire officiel,
     aucune corruption pendant telechargement/extraction).
  5. Capture d'ecran automatique tentee mais ratee (fenetre editeur capturee
     a la place, probable souci d'echelle DPI/`CopyFromScreen` deja observe
     a l'etape 1 sur le scenario d'echec) - preuve retenue via la lecture
     UIA directe par PID, non affectee par ce probleme d'affichage.
  6. Nettoyage post-verification : process `Lumora.WinUI.exe` et `tor.exe`
     du test arretes manuellement, dossier de profil temporaire supprime.

**Version :** `0.83.26-dev`.
