# Accessibilite socle et recherche d'accueil - 0.83.32-dev

## Contexte

Suite a l'audit produit/UX/accessibilite demande par l'utilisateur, premier
lot de corrections ciblees sur les points a fort impact et a faible risque :

- comportement fragile de la recherche sur `lumora://accueil` ;
- libelles accessibles insuffisants sur plusieurs controles dynamiques ;
- toggles d'epinglage des modules trop ambigus pour un lecteur d'ecran.

## Changements

### 1. Recherche de l'accueil

- Suppression du `readonly` temporaire sur le champ `#q` de la page
  d'accueil.
- Suppression du de-verrouillage differe par `setTimeout` et des handlers
  `pointerdown` / `focus` qui ne servaient qu'a contourner ce verrou.
- Conservation du nettoyage du champ au chargement (`clearSearch`) pour
  garder un accueil vierge sans introduire de comportement piege.

Effet recherche : moins de risque de premier caractere perdu, de focus
etrange au clavier, ou de sequence mal comprise par un lecteur d'ecran.

### 2. Modules epingles

- `UpdateModulesPinUi()` met maintenant a jour un vrai nom accessible et une
  vraie info-bulle pour chaque toggle d'epinglage, selon son etat.
- Exemple :
  - `Epingler Lecture a voix haute dans la barre de modules`
  - `Retirer Lecture a voix haute de la barre de modules`

Effet recherche : fin du libelle generique "Epingler" repete sur 9 toggles
deux fois (barre rapide + hub des modules), sans contexte vocal.

### 3. Controles dynamiques

Ajout de `ApplyNovaControlAccessibility(...)` sur des boutons/combos crees
par code dans :

- `MainWindow.History.cs`
  - ouvrir un telechargement
  - ouvrir son dossier
  - retirer l'entree de l'historique
- `MainWindow.WebApps.cs`
  - renommer une application
  - desinstaller une application
  - ouvrir une application
  - toggle de mode de fenetre
- `MainWindow.SavedTabGroups.cs`
  - ouvrir un groupe enregistre
  - supprimer un groupe enregistre
- `MainWindow.SiteControl.cs`
  - combo des permissions par site

## Tests

- Nouveau fichier : `Lumora.Tests/AccessibilityRegressionTests.cs`.
- Les assertions couvrent :
  - l'absence du vieux mecanisme `readonly` + `unlockSearch()`;
  - la presence des libelles accessibles explicites sur les surfaces
    dynamiques corrigees.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore`
  - premier essai : echec, acces refuse sur un fichier temporaire du cache
    `obj\Debug\net8.0-windows` dans le sandbox ;
  - relance autorisee : `569/569` verts.

## Limites restantes

- `AccessibilityLargeText` reste partiel : cette etape n'a pas refondu toute
  la typographie des panneaux profonds.
- Pas encore de passe complete Narrator/clavier sur l'application entiere.
- Pas encore d'unification des nombreux bandeaux/notifications au-dessus du
  web.

## Version

`0.83.32-dev`
