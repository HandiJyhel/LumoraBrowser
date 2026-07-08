# Un WebView2 par onglet (0.32.0-dev)

## Probleme resolu

Depuis la 0.8.2, un seul controle WebView2 etait partage entre tous les onglets :
changer d'onglet appelait `NavigateBrowser()` et **rechargeait entierement la page**.
Etat perdu, navigation lente, formulaires vides. Identifie comme « chantier 3 »
depuis la 0.23.0.

## Architecture

- `BrowserTabState` (PulseModels.cs) porte desormais `View` (son WebView2) et
  `PendingAddress` (adresse a charger des que le moteur est pret).
- `BrowserHost` (Grid) empile les WebView2 de tous les onglets ; l'activation d'un
  onglet est un simple basculement de `Visibility` (`ActivateTab`).
- `_browserView` (MainWindow) designe la vue de l'onglet ACTIF : tout le code
  existant (boutons retour/avancer, purge de sessions, parametres) continue de
  fonctionner sans changement.
- **Creation paresseuse** (`EnsureTabView`) : un onglet restaure en arriere-plan ne
  cree son moteur qu'a sa premiere activation. Garde `_browserSurfaceReady` : aucune
  vue n'est creee avant l'activation de la fenetre (crash WebView2 historique).
- Fermeture d'onglet (`CloseTabView`) : detache le module Credentials, oublie les
  scripts privacy de ce moteur, retire la vue et appelle `Close()`.

## Scoping des evenements

Tous les handlers (`NavigationStarting/Completed`, `SourceChanged`,
`DocumentTitleChanged`, `FaviconChanged`) retrouvent l'onglet proprietaire via
`TabForView`/`TabForCore` et ne touchent l'UI globale (barre d'adresse, statut,
barres de remplissage) que si la vue est active (`IsActiveView`). Un onglet
d'arriere-plan met a jour son titre/favicon, jamais les barres.

## Modules par moteur

- **Privacy** : scripts cosmetique/consentement enregistres PAR moteur
  (`_cosmeticScriptIds`/`_consentScriptIds`, dictionnaires par `CoreWebView2`).
  Les toggles re-enregistrent sur tous les moteurs vivants (`AttachedCores`).
- **CredentialService** : `AttachAsync(core)` observe chaque moteur ;
  `SetActiveCore` designe celui qui pilote l'UI. Les messages `page-state` des
  onglets d'arriere-plan sont ignores ; les captures d'identifiants restent
  acceptees de tous les onglets (redirection post-login pendant un changement
  d'onglet). `FillAsync` remplit toujours l'onglet visible.
- **Sessions ephemeres** : purge au premier moteur initialise uniquement
  (profil Chromium partage entre tous les moteurs du process).
- **Migration mots de passe Chromium** : une fois par lancement
  (`_browserPasswordsMigrated`).

## Limites connues

- Le compteur « requetes bloquees sur cette page » du bouclier peut compter des
  requetes d'onglets d'arriere-plan (le compteur de PrivacyEngine est global).
- Chaque onglet actif consomme la memoire de son moteur (comportement standard
  des navigateurs modernes ; la creation paresseuse limite le cout au demarrage).
