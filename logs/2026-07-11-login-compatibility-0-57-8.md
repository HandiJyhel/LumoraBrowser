# 2026-07-11 - Pulse Browser 0.57.8-dev - Mode compatibilite connexion par site

## Contexte

Apres les correctifs OAuth/Google Identity et consentement cookies, l'utilisateur clarifie la politique produit :
Pulse Browser ne doit pas eviter un serveur Pulse inexistant, il doit surtout limiter les fuites vers les GAFAM, la publicite ciblee et la telemetrie.

Le besoin n'est donc pas de desactiver la securite globalement, mais de permettre a un site choisi par l'utilisateur de terminer un flux de connexion federée quand les protections standard cassent le retour de session.

## Correctifs

- Ajout d'une liste locale `LoginCompatibilitySites` dans les reglages UI.
- Ajout d'un interrupteur `Mode compatibilite connexion` dans le panneau `Site actuel`.
- Le mode est persistant par domaine racine et se reapplique aux scripts de consentement deja enregistres.
- Le gestionnaire de cookies evite le refus automatique sur les domaines en compatibilite connexion.
- Le bloqueur reseau laisse passer, dans ce contexte precis, les ressources Google Identity necessaires (`accounts.google.com`, `apis.google.com`, `gstatic`) tout en gardant le filtrage ads/analytics/telemetrie.
- Le resume du bouclier indique quand la compatibilite connexion est active pour le site.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 174/174 tests verts.
- `dotnet build PulseBrowser.WinUI\PulseBrowser.WinUI.csproj --no-restore` : echec attendu avec le SDK .NET seul (`ExpandPriContent` WinUI manquant).
- MSBuild Visual Studio Release x64 : reussi, 0 avertissement/erreur.
- Artefact propre genere localement via MSBuild Visual Studio :
  `artifacts\clean-test\PulseBrowser-0.57.8-dev-win-x64-clean-20260711-031906`
- SHA256 `PulseBrowser.WinUI.dll` : `c11a2359a1f20abc90e70b82f1184dd52c59acaf48cce39a536166f201c2652e`
- SHA256 exe hote : `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`

## Limite

- `scripts\build-clean-test-artifact.ps1` a ete bloque par NuGet en ligne (`NU1301`) dans le bac a sable.
- La demande d'autorisation reseau a ete refusee par la plateforme car le quota Codex etait atteint.
- `scripts\build-installer.ps1` n'a pas pu produire l'exe unique : `dotnet publish` demande `Microsoft.NET.ILLink.Tasks` via NuGet pour `PublishSingleFile=true`.
- Ajout du fallback local `scripts\build-installer-netfx.ps1`, qui compile un installateur WinForms .NET Framework 4.8 avec le payload embarque, sans NuGet/ILLink.
- `scripts\build-installer-netfx.ps1 -Version 0.57.8-dev` : reussi, 0 avertissement/erreur.
- Installateur unique : `artifacts\installer\PulseBrowserSetup-0.57.8-dev-win-x64.exe`, SHA256 `2f9e6f767c481010afbf5b5661e177b290cd640e211bc22ed01a47c9cb09d4a6`.
- Variante verifiee : l'installateur compile/publie aussi en dossier sans `PublishSingleFile`.
- Sortie setup non-single-file : `artifacts\installer\staging-dotnet\publish-folder\PulseBrowserSetup.exe`, SHA256 `086df31b716f6e61f2d5c875c6c73eaa1eead377b636837de9bccbd183e0d082`. Ce fichier doit rester avec `PulseBrowserSetup.dll`, `.deps.json` et `.runtimeconfig.json`.

## Usage attendu

Pour tester le site concerne : ouvrir le site, aller dans `Site actuel`, activer `Mode compatibilite connexion`, recharger la page, puis relancer `Connexion`.
