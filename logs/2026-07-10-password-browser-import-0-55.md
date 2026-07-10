# 2026-07-10 - Import de mots de passe depuis un navigateur installe 0.55.0-dev

## Contexte

Retour utilisateur apres l'installateur `0.54.x` : l'import de mots de passe ne
proposait qu'un fichier CSV. Aucune option ne permettait de recuperer directement
les identifiants deja enregistres dans un autre navigateur installe sur la
machine (Chrome, Edge, Brave...), alors que l'import de FAVORIS le fait deja
depuis `0.6`/`0.8.x` via `BrowserImportSource`.

`ChromiumCredentialReader` existait deja, mais uniquement pour rapatrier ce que
le moteur WebView2 interne de Pulse aurait enregistre avant la bascule 100%
maison (migration `SyncFromBrowserStore`). Il ne pointait jamais vers le dossier
`User Data` d'un navigateur tiers.

## Changements

- `ChromiumCredentialReader.cs` : la logique de dechiffrement (cle maitre DPAPI
  dans `Local State`, mots de passe AES-256-GCM `v10`/`v11` dans `Login Data`)
  est extraite dans `ReadFrom(localStatePath, loginDataPath)`, generique. `Read`
  (magasin interne WebView2) devient un simple appel a `ReadFrom`. Ajout de
  `CountLogins` (comptage rapide sans dechiffrement, pour l'affichage de la
  liste de sources avant import). Les deux requetes filtrent maintenant
  `blacklisted_by_user = 0` (les entrees "ne jamais enregistrer" de Chromium
  ne doivent pas etre importees).
- `Models/InstalledBrowsers.cs` (nouveau) :
  - `InstalledChromiumBrowsers` factorise la liste des dossiers `User Data`
    (Chrome, Edge, Brave, Chromium, Vivaldi, Opera, Opera GX), deja utilisee
    par `BrowserImportSource` (favoris) et desormais partagee.
  - `PasswordImportSource` : detecte les profils Chromium tiers contenant des
    identifiants, expose un `Label` avec le compte detecte, et
    `ReadCredentials()` qui delegue a `ChromiumCredentialReader.ReadFrom`.
- `Models/Bookmarks.cs` : `BrowserImportSource.Discover` reutilise
  `InstalledChromiumBrowsers` au lieu de dupliquer la liste de dossiers.
- `MainWindow.Vault.cs` : nouveau point d'entree `ImportPasswordsAsync` —
  detecte les sources navigateur disponibles, propose un choix (CSV ou
  navigateur detecte avec son nombre d'identifiants) via
  `PromptPasswordImportSourceAsync`, puis importe avec confirmation explicite
  (`ImportPasswordsFromBrowserAsync`). `ImportPasswordsMenu_Click` et
  `VaultImportButton_Click` passent par ce nouveau point d'entree au lieu
  d'ouvrir directement le selecteur de fichier CSV.
- `MainWindow.xaml` / `MainWindow.CommandPalette.cs` : infobulle et description
  de commande mises a jour pour mentionner l'import navigateur.
- Passage de version source a `0.55.0-dev` (`MainWindow.xaml.cs`, `AGENTS.md`,
  `PulseBrowser.WinUI/README.md`, defauts de
  `scripts/build-clean-test-artifact.ps1` et `scripts/build-installer.ps1` non
  modifies, version passee explicitement en parametre lors du build).

## Portee volontairement exclue

- Firefox (format `key4.db`/NSS, PBKDF2 + 3DES ou AES-256-CBC) n'est pas
  couvert : dechiffrement nettement plus complexe a implementer correctement
  en C# pur sans `libnss3`, et plus risque a valider sans vraie installation
  Firefox avec mots de passe. Decision utilisateur (2026-07-10) : se limiter a
  la famille Chromium pour cette iteration.

## Tests

- `PulseBrowser.Tests/ChromiumCredentialReaderTests.cs` (nouveau) : fabrique un
  couple `Local State` (cle DPAPI) + `Login Data` (SQLite, mots de passe
  AES-256-GCM `v10`) et verifie que `ReadFrom` dechiffre correctement plusieurs
  entrees, ignore les lignes `blacklisted_by_user`, et que `CountLogins`
  compte sans dechiffrer. Ajout de `Microsoft.Data.Sqlite` a
  `PulseBrowser.Tests.csproj` (dejà utilise par le projet WinUI).
- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj` : 163/163 tests
  verts (159 existants + 4 nouveaux).

## Verification build

- `build-winui.cmd` (MSBuild, avec restore) : 0 avertissement, 0 erreur.
- `scripts/build-clean-test-artifact.ps1 -Version 0.55.0-dev` : build Release
  propre reussi. SHA256 executable :
  `512b669edd5cec018c2d48fffbd20477ebec554ef5acce5fbe996a6edd2e1eae`.
- `scripts/build-installer.ps1 -Version 0.55.0-dev` : installateur genere.

## Artifact genere

```text
artifacts/installer/PulseBrowserSetup-0.55.0-dev-win-x64.exe
```

SHA256 :

```text
1735beab9ef4c33d133524e2393f859611383a85867217292f78a5eff01cdb71
```

Fichier de verification :

```text
artifacts/installer/PulseBrowserSetup-0.55.0-dev-win-x64.VERIFICATION.txt
```

`artifacts/installer` ne contient que l'exe final et son `.VERIFICATION.txt`
(les anciens artefacts `0.54.x` sont retires automatiquement par le script).

## Limites

- L'installateur n'est pas signe Authenticode ; Windows peut afficher
  "Editeur inconnu".
- L'import depuis un navigateur ne fonctionne que si Pulse tourne sous le
  meme compte Windows que celui utilise pour enregistrer les mots de passe
  dans ce navigateur (la cle maitre est protegee par DPAPI, liee au compte).
- Si le navigateur source est en cours d'execution, la copie temporaire de
  `Login Data` peut echouer sur un verrou exclusif rare ; fermer le navigateur
  source avant import si l'exe indique 0 identifiant detecte malgre un compte
  connu.
