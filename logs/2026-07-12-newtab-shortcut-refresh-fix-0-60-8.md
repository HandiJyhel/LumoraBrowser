# 2026-07-12 - Correctif rafraichissement raccourci nouvel onglet (0.60.8-dev)

## Contexte

L'utilisateur a signale que le bouton "Ajouter" de la page nouvel onglet ne
fonctionnait pas : ajout d'un raccourci "Google" avec son lien, clic sur
"Enregistrer", et le raccourci n'apparaissait pas.

## Diagnostic (confirme par lecture de code, pas par test live)

Bug reproductible par analyse statique, sans ambiguite :

1. A l'ouverture d'un nouvel onglet, `tab.Address` vaut `"pulse://accueil"`
   (`AddTab`).
2. `NavigateTabView` charge la page d'accueil via
   `browser.CoreWebView2.NavigateToString(HomePageHtml())` (pas de vraie
   navigation HTTP, pas d'URL reelle).
3. Quand cette "navigation" se termine, `BrowserView_NavigationCompleted`
   lit `sender.Source` — qui vaut `"about:blank"` pour une page chargee via
   `NavigateToString` — et appelait `UpdateTab(tab, title, address, ...)`
   avec cette valeur, ecrasant `tab.Address` : il ne valait plus jamais
   `"pulse://accueil"` une fois la page d'accueil chargee (quasi immediat).
4. `RefreshPulseHomePages()` (appelee apres l'ajout/edition/suppression d'un
   raccourci) filtre les onglets sur `tab.Address == "pulse://accueil"` pour
   savoir lesquels recharger. Ce filtre ne trouvait donc plus JAMAIS l'onglet
   nouvel onglet actuellement ouvert : le raccourci etait bien sauvegarde
   dans `_uiSettings.NewTabShortcuts` (persistant sur disque), mais la page
   affichee ne se rafraichissait jamais pour le montrer. Un nouvel onglet
   frais l'aurait affiche correctement (nouvelle page = nouvelle lecture des
   parametres), ce qui correspond exactement au symptome rapporte.

Le code avait deja une correction partielle pour ce probleme, mais seulement
sur la barre d'adresse visible (`SyncActiveAddressBar`, avec un commentaire
explicite "on garde l'adresse logique de l'onglet") — pas sur `tab.Address`
lui-meme, qui est la valeur dont depend `RefreshPulseHomePages()`.

## Changements

- `BrowserView_NavigationCompleted` : calcule desormais une seule valeur
  `addressForTab` (adresse reelle si `BookmarkStore.IsWebUrl`, sinon
  `tab.Address` inchange) et l'utilise a la fois pour `UpdateTab` et
  `SyncActiveAddressBar`, au lieu d'ecraser `tab.Address` avec
  `"about:blank"`.
- Meme protection dans `BrowserCore_DocumentTitleChanged`, second point
  d'entree avec le meme risque (declenche par un changement de titre de
  document, pas seulement par la fin de navigation).
- Passage de version source a `0.60.8-dev`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj` : 233 tests
  reussis.
- `scripts\build-clean-test-artifact.ps1 -Version 0.60.8-dev` : build MSBuild
  reussi, 0 avertissement, 0 erreur.
- `scripts\build-installer.ps1 -Version 0.60.8-dev` : installateur genere.
- Installateur :
  `artifacts\installer\PulseBrowserSetup-0.60.8-dev-win-x64.exe`.
- SHA256 installateur :
  `ca90a305e3c034de58d9f153ac16748ec325047e216c9db86710d40aad6ae43a`.

## Confiance

Contrairement aux correctifs plein ecran de 0.60.7-dev (speculatifs, cause
non confirmee), celui-ci repose sur un mecanisme de bug identifie sans
ambiguite par lecture de code : `NavigateToString` rapporte toujours
`"about:blank"` comme `Source`, et `UpdateTab` ecrasait toujours
`tab.Address` avec la valeur recue. Ce n'est pas une hypothese parmi
d'autres. Reste malgre tout **non teste manuellement** (aucun pilotage UI
disponible dans cette session) : a confirmer par l'utilisateur en ajoutant un
raccourci depuis un onglet nouvel onglet deja ouvert.
