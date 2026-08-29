# Lumora

Un navigateur différent, pensé pour vous.

Lumora est un navigateur web pour Windows, conçu autour d'une idée simple : la
confidentialité et l'accessibilité ne devraient pas être des options qu'il faut
aller chercher dans un menu — elles sont le point de départ. Aucune donnée
utilisateur (favoris, historique, mots de passe, profil) ne quitte votre
ordinateur ; il n'y a pas de compte cloud, pas de synchronisation vers un
serveur, pas de télémétrie.

> Lumora est en développement actif. Ce dépôt reflète l'état du code à
> l'instant présent, pas une version stable garantie.

## Ce que Lumora fait

- **Coffre local chiffré** : mots de passe, passkeys et codes d'authentification
  à deux facteurs (TOTP), jamais synchronisés vers un service tiers.
- **Bloqueur de publicités et traqueurs** intégré, activé par défaut.
- **Profils locaux** protégés par mot de passe ou code PIN, avec un vrai mode
  invité pour naviguer sans rien laisser derrière soi.
- **Accessibilité comme fonctionnalité de premier ordre** : aides à la lecture,
  loupe, lecture à voix haute, modes visuels adaptés à la fatigue ou la basse
  vision — pas des réglages annexes.
- **Tor en option** (désactivé par défaut), pour qui en a besoin, sans imposer
  le compromis de vitesse à tout le monde.
- **Assistant de premier lancement**, sauvegarde/restauration complète du
  profil, et un installateur qui n'exige aucun prérequis système.

## Pile technique

- [.NET 8](https://dotnet.microsoft.com/) + [WinUI 3](https://learn.microsoft.com/windows/apps/winui/winui3/)
  pour l'interface.
- [Microsoft Edge WebView2](https://learn.microsoft.com/microsoft-edge/webview2/)
  (mode *Fixed Version* embarqué) comme moteur de rendu web.
- Application *self-contained* : aucun runtime .NET à installer au préalable
  sur la machine cible.

## Construire et lancer le projet

`dotnet build` échoue sur ce projet à cause du packaging PRI du Windows App
SDK (limitation connue du SDK .NET, pas du code) — utiliser MSBuild de
Visual Studio à la place :

```powershell
.\scripts\run-winui.ps1
```

Ce script compile en configuration Debug et lance l'application. Pour un
build de test isolé (sans toucher à votre profil Lumora existant), voir
`scripts\build-clean-test-artifact.ps1` puis `scripts\build-installer.ps1`.

### Tests

```powershell
cd Lumora.Tests
dotnet test
```

## Licence

Lumora est distribué sous licence **GNU GPLv3** (voir [`LICENSE`](LICENSE)),
avec une exception explicite autorisant la liaison avec WebView2 et le
Windows App SDK, tous deux nécessaires au fonctionnement sur Windows mais non
couverts par une licence libre — voir
[`LICENSE-EXCEPTIONS.md`](LICENSE-EXCEPTIONS.md) pour le détail.

## Auteur

Développé par **HandiJyhel**.
