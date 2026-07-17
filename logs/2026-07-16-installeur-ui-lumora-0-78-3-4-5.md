# 2026-07-16 - Installeur Lumora plus presentable (0.78.3.4.5-dev)

## Demande

Rendre l'installateur Lumora plus agreable visuellement, sans changer le modele
local-first ni embarquer de donnees personnelles.

## Changements

- Version passee a `0.78.3.4.5-dev`.
- L'installateur WinForms genere par `scripts/build-installer.ps1` a ete
  retravaille :
  - fenetre agrandie ;
  - entete Lumora plus identifiable avec logo, version visible et badges ;
  - sections `Dossier d'installation`, `Contenu installe` et `Options`
    repositionnees et harmonisees ;
  - boutons et couleurs retouches pour moins ressembler a un formulaire brut ;
  - barre de progression raccordee aux etapes reelles de l'installation.
- Les options existantes restent presentes :
  - telecharger WebView2 depuis Microsoft si le runtime manque ;
  - installation propre du profil installe precedent ;
  - raccourcis Bureau et menu Demarrer ;
  - lancement de Lumora apres installation.
- Les garanties conservees :
  - aucun profil utilisateur embarque ;
  - aucun dossier de profil force au lancement ;
  - WebView2 telecharge uniquement si absent, avec verification de signature
    Microsoft ;
  - installation locale par defaut sous `LOCALAPPDATA`.
- Les valeurs par defaut de `build-clean-test-artifact.ps1` et
  `build-installer.ps1` pointent maintenant vers `0.78.3.4.5-dev`.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` :
  442/442 tests verts.
- `cmd /c .\build-winui.cmd` :
  premiere tentative bloquee par le reseau sandboxe (`NU1301`), puis relance
  autorisee reussie avec 0 avertissement et 0 erreur.
- Artefact propre Release autonome :
  `artifacts\clean-test\Lumora-0.78.3.4.5-dev-win-x64-clean-20260716-031121`.
- SHA256 executable :
  `e94f299f13279fb76de4f892e136a1152d93c37d10e4621eac7616fbd3bce67f`.
- Installeur construit :
  `artifacts\installer\LumoraSetup-0.78.3.4.5-dev-win-x64.exe`.
- Taille installeur :
  `77 966 954` octets.
- SHA256 installeur :
  `0626f2827a0522225631dad1ce8f58a50250f4d8e2aa430fa17c0ddd56380453`.
- Fichier de verification :
  `artifacts\installer\LumoraSetup-0.78.3.4.5-dev-win-x64.VERIFICATION.txt`.

## Note

L'interface du setup a ete compilee dans l'installateur. L'installateur n'a pas
ete lance en interaction pour eviter une installation non demandee pendant la
verification automatisee.
