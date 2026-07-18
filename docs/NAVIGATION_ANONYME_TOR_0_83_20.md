# Navigation anonyme (Tor) - architecture - 0.83.20-dev

## Objectif

Poursuite du masquage d'IP entame avec l'anti-fuite WebRTC : poser
l'architecture d'une fenetre de navigation routee via le reseau Tor
(masquage reel de l'adresse IP, pas un service maison - voir la discussion
qui a ecarte l'idee d'un reseau de relais propre a Lumora). Increment
volontairement limite : l'acquisition du moteur Tor (telechargement) est une
etape separee, pas encore cablee. Rien ne se telecharge dans cette version.

## Comportement

- Nouvelle entree de menu "Navigation anonyme (Tor)", a cote de "Nouvelle
  fenetre privee" (les deux copies du menu, barre haute et barre basse).
- Nouvelle fenetre `LumoraTorWindow`, independante de `MainWindow` (meme
  modele que la navigation privee) : chrome minimal (retour/avancer/recharger
  + barre d'adresse), protections reseau habituelles (bloqueur pub/trackers,
  anti-telemetrie, HTTPS, CNAME).
- Ecran d'etat affiche tant que le moteur Tor n'est pas connecte : explique
  le principe, affiche le statut ("Moteur Tor non installe.") et un bouton
  "Installer le moteur Tor" qui, pour cette version, se contente d'expliquer
  que le telechargement n'est pas encore cable - aucune action reseau,
  aucune fausse promesse de progression.
- `Lumora.WinUI/Tor/TorProcessManager.cs` : gestion REELLE (pas un stub) du
  cycle de vie d'un processus `tor.exe` s'il existe deja dans
  `<profil>/tor/tor.exe` - demarrage avec `--SocksPort`/`--DataDirectory`,
  suivi du bootstrap via les lignes de log ("Bootstrapped NN%"), arret
  propre. Fonctionnera des qu'un moteur sera present, sans modification.
- Une fois l'etat `Connected` atteint, la fenetre cree son propre
  environnement WebView2 (dossier de donnees dedie sous `<profil>/tor/`,
  donc processus Chromium separe du reste de Lumora) avec
  `--proxy-server=socks5://127.0.0.1:<port>` : seul le trafic de cette
  fenetre passe par Tor, jamais le reste de la navigation.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 517/517
  tests reussis (test de garde-fou d'alignement de version mis a jour vers
  `0.83.20-dev`).
- Build WinUI MSBuild x64 (Debug) : 0 avertissement, 0 erreur.
- Verification en conditions reelles : ouverture du menu, clic "Navigation
  anonyme (Tor)" - fenetre dediee ouverte, statut "Moteur Tor non installe."
  confirme, clic sur "Installer le moteur Tor" affiche le message honnete
  attendu sans effet de bord, aucune exception dans la trace runtime.

## Notes

- `dotnet build` seul reste inadapte pour WinUI sur cette machine (tache AppX
  `ExpandPriContent` absente du SDK .NET courant) : build via MSBuild Visual
  Studio x64.
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora en dehors de la verification.
- Prochaine etape (hors perimetre ici, necessitera un Go explicite avant
  toute action reseau) : cabler le telechargement du Tor Expert Bundle
  officiel (torproject.org) derriere le bouton "Installer le moteur Tor".
