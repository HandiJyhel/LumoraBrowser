# 2026-07-23 - Visibilite theme clair du Nouvel onglet (0.84.0.15-dev)

## Demande

Apres la refonte claire/sombre de la page Nouvel onglet en `0.84.0.14-dev`,
l'utilisateur montre une nouvelle capture du theme clair et signale que
plusieurs elements internes restent trop peu visibles, malgre une base deja
beaucoup plus coherente.

## Corrections appliquees

- `Lumora.WinUI/MainWindow.NewTabHome.cs`
  - renforcement des variables de contraste du theme clair pour le champ de
    recherche ;
  - placeholder et icone de recherche assombris pour rester lisibles sur fond
    ivoire ;
  - bordure et ombre du champ de recherche renforcees ;
  - cartes de raccourcis dotees d'une vraie bordure par defaut, d'une ombre
    legere et d'un hover plus net ;
  - bouton "Ajouter" dote d'un fond, d'une ombre et d'un pictogramme interne
    plus visibles ;
  - tuiles outils harmonisees avec des surfaces claires dediees, au lieu d'un
    rendu trop efface herite de la logique sombre ;
  - nouvelles variables CSS dediees a ces surfaces et etats, notamment
    `--nt-tool-bg`, `--nt-shortcut-border`,
    `--nt-shortcut-shadow`, `--nt-add-shortcut-shadow` et
    `--nt-add-shortcut-dot-bg`.
- montee de version en `0.84.0.15-dev` dans :
  - `MainWindow.xaml.cs`
  - `AGENTS.md`
  - `scripts/build-installer.ps1`
  - `scripts/build-clean-test-artifact.ps1`
  - `Lumora.Tests/UsageModeVisualIdentityTests.cs`

## Verification

- test cible passe :
  - `dotnet test Lumora.Tests\Lumora.Tests.csproj --filter UsageModeVisualIdentityTests`
  - resultat : `11/11` tests reussis

## Livraison

- artefact propre genere avec succes :
  - `artifacts/clean-test/Lumora-0.84.0.15-dev-win-x64-clean-20260723-204049`
- manifeste SHA256 artefact propre :
  - `artifacts/signatures/Lumora-0.84.0.15-dev-clean-20260723-204128.sha256`
- installateur genere avec succes :
  - `artifacts/installer/LumoraSetup-0.84.0.15-dev-win-x64.exe`
- fichier de verification installeur :
  - `artifacts/installer/LumoraSetup-0.84.0.15-dev-win-x64.VERIFICATION.txt`
- manifeste SHA256 installeur :
  - `artifacts/signatures/LumoraSetup-0.84.0.15-dev-20260723-204202.sha256`
- SHA256 installeur :
  - `7e36f01131b3ff84e7ac44f4632d53ae1ec1ab79845d7880dc255b6a0f05b734`
- ancien installateur `0.84.0.14-dev` laisse en place sur cette passe ;
- cette iteration livre donc a la fois la finition visuelle du theme clair et
  le setup associe en `0.84.0.15-dev`.

## Version

`0.84.0.15-dev`
