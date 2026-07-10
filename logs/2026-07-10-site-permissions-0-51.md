# 2026-07-10 - Permissions par site 0.51.0-dev

Suite au plan d'amelioration pour rendre Pulse Browser plus utilisable au quotidien, ajout d'un premier centre de permissions par site.

## Changements

- Ajout de `Privacy/SitePermissions/SitePermissionPolicy.cs`.
- Ajout de `UiSettings.SitePermissions` pour persister les decisions localement par domaine racine.
- `Centre du site` : nouvelle carte `Permissions` avec Camera, Microphone, Localisation, Notifications, Presse-papiers, Telechargements multiples et Fichiers locaux.
- Chaque permission peut etre mise sur `Demander`, `Autoriser` ou `Bloquer`.
- `CoreWebView2.PermissionRequested` applique les decisions locales : autorisation ou refus force selon le choix utilisateur.
- Ajout de `SitePermissionPolicyTests`.
- Version projet passee a `0.51.0-dev`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 145/145 tests verts.
- Premier `build-winui.cmd` bloque par le sandbox reseau NuGet (`NU1301`), puis relance autorisee reussie avec 0 avertissement et 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` : fenetre `Pulse Browser 0.51.0-dev`, processus repondant, fermeture propre du processus de test.

**Version :** `0.51.0-dev`.
