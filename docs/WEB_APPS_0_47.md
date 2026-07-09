# Applications web Pulse — 0.47.0-dev

## Objectif

Reprendre l'idée du « Créer un raccourci » / « Installer comme application » de
Chrome/Edge, en mieux : un site épinglé s'ouvre dans une fenêtre dédiée sans
onglets ni barre d'adresse, avec un raccourci Menu Démarrer (et Bureau en
option), tout en gardant les principes Pulse (protections actives, aucune
donnée hors machine).

## Modèle et stockage

- `Models/WebApps.cs` : `PulseWebApp` (Id, Title, Url, RootDomain, IconFile,
  AlwaysOnTop, ShortcutFileName, HasDesktopShortcut, CreatedAt).
- `WebApps/WebAppStore.cs` : registre `webapps.pulse` (même chiffrement DPAPI
  que bookmarks/historique/paramètres — pas de données sensibles ici, même
  tier de protection). Gate mode invité comme les autres stores.

## Fenêtre d'application (`PulseAppWindow`)

- Fenêtre minimale : pas d'onglets, pas de barre d'adresse, un seul `WebView2`
  occupant tout l'espace, titre = nom de l'application.
- **Même profil que la fenêtre principale** : `WebView2Bootstrap.ConfigureOnce`
  centralise la configuration process-wide (dossier de profil WebView2 +
  arguments anti-télémétrie moteur), appelée à la fois par `MainWindow` et par
  `App.xaml.cs` avant de créer une fenêtre d'application en processus séparé.
  Cookies et sessions sont donc partagés — installer Gmail en application ne
  crée pas une deuxième copie de la session.
- **Protections actives** : bloqueur de pubs/trackers, anti-télémétrie,
  nettoyeur de paramètres, HTTPS-only, détection CNAME cloaking — le même
  `PrivacyEngine` que la fenêtre principale, instancié localement. **Limite
  documentée v1** : le filtre cosmétique (masquage visuel des emplacements
  pub) et le refus automatique des bannières cookies ne sont pas encore
  branchés dans les fenêtres d'application (nécessitent une injection de
  script par page, réservée à la fenêtre principale pour l'instant).
  L'auto-remplissage identifiants/cartes n'est pas non plus branché ici v1.
- **Confinement de domaine « doux »** (`WebApps/WebAppUrlPolicy.cs`, logique
  pure testée) : jamais de blocage de navigation — un blocage dur aurait cassé
  les redirections de connexion Google/Microsoft (le problème même que le
  palier précédent venait de corriger). À la place, une barre discrète
  apparaît quand la page affichée sort du domaine racine de l'application,
  avec un bouton « Retour à l'application ».

## Installation depuis la page active

- Menu Pulse et palette `Ctrl+K` → « Installer comme application ».
- Dialogue : nom modifiable (pré-rempli avec le titre de la page), case
  « Ajouter aussi un raccourci sur le Bureau ».
- **Synergie avec les sessions éphémères (0.46.0-dev)** : installer une
  application ajoute automatiquement son domaine aux sites de confiance
  (`SetTrustedSessionSite`) — installer Gmail suppose vouloir y rester
  connecté après fermeture de Pulse Browser.
- Icône du raccourci : le favicon déjà mis en cache par la navigation normale
  est encapsulé en `.ico` par `WebApps/IcoWriter.cs` (logique pure testée) —
  Windows accepte un PNG brut dans un conteneur ICO minimal depuis Vista,
  aucune conversion de pixels ni dépendance externe nécessaire.
- Raccourci Windows : `WebApps/ShellShortcut.cs` (interop COM
  `IShellLinkW`/`IPersistFile`, aucune dépendance NuGet) crée un `.lnk` dans
  `Menu Démarrer\Programmes\Pulse Apps\` pointant vers
  `PulseBrowser.WinUI.exe --app=<id>`, et sur le Bureau si demandé.

## Lancement direct (`--app=<id>`)

- `WebApps/WebAppLaunchArgs.cs` (logique pure testée) extrait l'id depuis les
  arguments de ligne de commande.
- `App.xaml.cs` : si un id valide est présent et trouvé dans le registre
  local, crée directement une `PulseAppWindow` sans jamais afficher la
  fenêtre principale. Si l'application a été supprimée entre-temps, retombe
  proprement sur le navigateur normal (vérifié : `--app=<id-inconnu>` ouvre
  bien la fenêtre principale sans erreur).

## Panneau « Applications »

- Accessible via le menu Pulse et `Ctrl+K`.
- Liste des applications installées : icône (favicon en cache), titre,
  domaine, toggle « Toujours au premier plan » (persisté, appliqué via
  `OverlappedPresenter.IsAlwaysOnTop`), bouton Ouvrir, Renommer (recrée le
  raccourci avec le nouveau nom), Désinstaller (supprime le(s) raccourci(s)
  et l'icône, sans toucher aux favoris ni à la session).

## Correction incidente

`ShowPanel` (`MainWindow.xaml.cs`) ne masquait jamais `WalletPanel` en
changeant de panneau (oubli lors de l'ajout du portefeuille en 0.45.0-dev) :
corrigé en même temps que l'ajout de `WebAppsPanel` à la même liste.

## Tests

`WebAppLaunchArgsTests`, `WebAppUrlPolicyTests`, `IcoWriterTests` — logique
pure uniquement. La création réelle de raccourci (`ShellShortcut`, COM Shell)
et le rendu de `PulseAppWindow` ne sont pas unitairement testables et
nécessitent une vérification manuelle (installation d'une app réelle,
lancement depuis le raccourci Menu Démarrer).
