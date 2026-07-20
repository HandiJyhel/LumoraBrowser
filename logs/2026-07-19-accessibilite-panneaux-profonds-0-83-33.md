# Accessibilite profonde des panneaux dynamiques - 0.83.33-dev

## Contexte

Suite directe de `0.83.32-dev`.

Le premier lot avait traite le socle :

- recherche d'accueil non verrouillee artificiellement ;
- toggles d'epinglage des modules rendus explicites ;
- premiers libelles accessibles sur `Telechargements`, `Applications`,
  `Groupes enregistres` et permissions du `Centre du site`.

Ce second lot etend l'effort aux surfaces profondes qui restaient encore
faiblement couvertes par `Texte plus lisible` et par les conventions de
focus/noms accessibles.

## Changements

### 1. Tailles mutualisees

Ajout de deux helpers :

- `AccessibilityBodyFontSize()`
- `AccessibilitySecondaryFontSize()`

Objectif : eviter de laisser des `12px` et `13px` figes dans des cartes
dynamiques alors que l'utilisateur a active `Texte plus lisible`.

### 2. Rerender quand les reglages changent

`ApplyAccessibilitySettings()` relance maintenant, quand ils sont visibles :

- `RenderDownloads()`
- `RenderSavedTabGroups()`
- `RefreshWebAppsPanel()`
- `RenderPasskeysPanel()`
- `RefreshWalletPanel()`
- `RefreshVaultPanel()`
- `RefreshSessionsPanelAsync()`
- `RefreshSiteControlAsync(site)`

Effet : les panneaux ouverts n'attendent plus une fermeture/reouverture pour
refleter les nouveaux reglages de taille et d'accessibilite.

### 3. Surfaces couvertes

#### Sessions

- tailles de texte alignees sur les regles accessibilite ;
- nom accessible sur le toggle de conservation de session ;
- nom accessible sur le bouton d'oubli des cookies du site.

#### Passkeys

- tailles de texte alignees ;
- nom accessible sur le bouton de suppression ;
- icone de corbeille restauree (`\uE74D`) au lieu d'un glyphe vide.

#### Portefeuille

- tailles de texte alignees dans les cartes ;
- noms accessibles sur les actions modifier / supprimer / copier /
  utiliser sur la page active.

#### Acces rapide du coffre

- tailles de texte alignees dans le flyout ;
- noms accessibles sur deverrouillage, ouverture du coffre complet,
  remplissage, copies identifiant/mot de passe/TOTP.

#### Panneaux precedemment traites

Les cartes `Telechargements`, `Groupes enregistres`, `Applications` et les
combos du `Centre du site` ont aussi ete raccroches a ces tailles
mutualisees pour garder une logique uniforme.

## Tests

`Lumora.Tests/AccessibilityRegressionTests.cs` a ete enrichi pour verifier :

- la presence de `RefreshAccessibilitySensitivePanels()`;
- le rerender des panneaux dynamiques depuis `ApplyAccessibilitySettings()`;
- la presence des nouveaux libelles accessibles sur `Sessions`, `Passkeys`,
  `Portefeuille` et `VaultQuickAccess`;
- l'existence des helpers de taille mutualisee.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore`
  - resultat : `570/570` tests reussis.

## Limites restantes

- Pas encore de passe Narrator/clavier complete sur l'application reelle.
- Les nombreux bandeaux/notifications au-dessus du web ne sont pas encore
  simplifies ou unifies.
- `AccessibilityLargeText` n'a pas encore ete harmonise ligne par ligne sur
  tous les dialogues de creation/modification du coffre et des profils.

## Version

`0.83.33-dev`
