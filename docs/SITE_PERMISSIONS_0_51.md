# Permissions par site - 0.51.0-dev

Ce palier ajoute une premiere gestion locale des permissions sensibles par domaine.

## Objectif

Pour un usage quotidien, Pulse Browser doit donner a l'utilisateur un endroit clair pour verifier et changer ce qu'un site peut demander : camera, micro, localisation, notifications, presse-papiers, telechargements multiples et acces fichiers.

## Changements

- Ajout d'une politique locale `SitePermissionPolicy` testable, stockee dans `UiSettings.SitePermissions`.
- Les permissions sont enregistrees par domaine racine et par type de permission avec trois etats :
  - `Demander`
  - `Autoriser`
  - `Bloquer`
- `Centre du site` affiche maintenant une carte `Permissions`.
- Les choix faits dans cette carte sont persistes dans le profil local.
- `CoreWebView2.PermissionRequested` est branche sur la politique locale :
  - `Autoriser` force l'autorisation WebView2.
  - `Bloquer` force le refus WebView2.
  - `Demander` laisse le comportement normal du moteur.
- Les anciennes protections, sessions, mots de passe et historique du centre du site restent inchanges.

## Limites

- Ce palier ne remplace pas encore une page globale listant tous les sites ayant des permissions modifiees.
- Les libelles suivent les permissions WebView2 les plus utiles au quotidien ; d'autres permissions rares peuvent etre normalisees mais ne sont pas encore exposees dans l'UI.
- La verification automatisee ne simule pas une vraie demande camera/micro ; elle couvre la logique pure et le build valide le branchement WebView2.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 145/145 verts.
- `build-winui.cmd` : premier essai bloque par le sandbox reseau NuGet (`NU1301`), relance autorisee reussie avec 0 avertissement et 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` : fenetre `Pulse Browser 0.51.0-dev`, processus repondant, fermeture propre du processus de test.

**Version :** `0.51.0-dev`.
