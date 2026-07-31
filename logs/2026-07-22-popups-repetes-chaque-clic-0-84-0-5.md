# Popup rouvert à chaque clic séparé - 0.84.0.5-dev

## Problème

Après 0.84.0.4-dev, l'utilisateur confirme toujours des nouveaux onglets
indésirables : à chaque clic sur un lien du site en cause, un nouvel onglet
s'ouvre vers un autre site, obligeant à revenir sur l'onglet d'origine pour
continuer.

Diagnostic : le correctif 0.84.0.4 ne couvre pas ce schéma précis. Il ne
durcit les popups suivants que si CET onglet a déjà eu une navigation
bloquée par `NavigationHijackPolicy` (redirection dans l'onglet lui-même).
Or ici, l'onglet d'origine ne redirige jamais tout seul : chaque clic ouvre
son propre popup, indépendant. Comme chaque clic est un geste neuf,
`CountPopupsInGestureWindow` (fenêtre d'1 s) et `HadRecentPopup` (fenêtre de
3 s) retombent à zéro entre deux clics espacés, et rien ne s'accumule jamais
sur ce schéma - le correctif précédent ne se déclenche donc jamais ici.

## Correction

- `NavigationHealthTracker` : nouveau compteur `_totalPopupsOpenedByTab`,
  jamais élagué par le temps (contrairement à `_openedPopupsByTab`) -
  incrémenté dans `RegisterPopupOpened`, interrogeable via
  `HasOpenedPopupBefore`. Oublié à la prochaine navigation fraîche et à la
  fermeture de l'onglet, comme les autres états par-onglet.
- `MainWindow.AdShield.cs` : `DecidePopupVerdict` inclut désormais ce
  troisième signal dans le calcul de `openerUnderAdPressure`, en plus du
  compteur réseau (0.84.0) et de la navigation bloquée (0.84.0.4). Dès le
  DEUXIÈME popup ouvert par un même onglet - même des minutes plus tard,
  même vers un domaine encore inconnu - le popup suivant vers un domaine
  différent est bloqué, sauf whitelist, fournisseur d'identité connu, ou même
  site racine (ces exceptions passent toujours avant, inchangées dans
  `PopupPolicy.cs`, non modifié).
- Limite assumée inchangée : le tout premier popup d'un onglet reste
  indiscernable d'un popup légitime, non bloqué par choix.

## Fichiers touchés

- `Lumora.WinUI/NavigationHealthTracker.cs`
- `Lumora.WinUI/MainWindow.AdShield.cs`
- `Lumora.Tests/NavigationHealthTrackerTests.cs`
- Fichiers de version → `0.84.0.5-dev`

## Vérification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj` : 629/629 tests verts.
- Build Debug WinUI (MSBuild) : réussi, 0 erreur.
- Build propre (Release) + installateur `0.84.0.5-dev` générés avec succès ;
  ancien installateur (`0.84.0.4-dev`) supprimé de `artifacts/installer/` à
  la demande explicite de l'utilisateur.

**Version :** `0.84.0.5-dev`.
