# Pulse Browser - UX plein ecran, raccourcis, accessibilite (0.60.3-dev)

## Contexte

L'utilisateur a signale plusieurs regressions visibles dans `PulseBrowser.WinUI` :
anciens raccourcis rapides encore presents dans les parametres, menus trop centres
en plein ecran, options d'accessibilite trop pauvres, section Navigation trop peu
explicite, barre de favoris trop compacte, champ de recherche non vide au lancement,
et mode plein ecran qui conservait une chrome trop lourde.

## Changements

- Suppression des raccourcis rapides par defaut `Accueil`, `Google`, `YouTube` et
  `GitHub` pour les nouveaux profils.
- Migration douce des profils existants : si la liste contient exactement ces quatre
  anciens raccourcis par defaut, elle est videe et le toggle reste desactive. Les
  raccourcis personnalises ne sont pas touches.
- Correction confidentialite de la barre d'adresse : `pulse://accueil` s'affiche
  maintenant comme un champ vide, pour eviter de revoir une ancienne saisie ou un
  fragment de PIN au demarrage.
- Correction de la page d'accueil : le champ de recherche HTML est force a `value=""`
  et vide sur `DOMContentLoaded`/`pageshow`, avec autocomplete/autocorrect desactives.
- Refonte du mode plein ecran : la chrome principale, les onglets horizontaux, les
  favoris et le rail vertical se replient ; une barre Pulse compacte reste disponible
  avec nouvel onglet, accueil, sortie plein ecran et menu.
- En plein ecran, la palette de commande est ancree plus pres du bord droit et reduite,
  au lieu de rester comme un grand panneau centre.
- Parametres > Navigation enrichi : explication claire de `Ctrl+K`, actions rapides
  vers la palette, le demarrage et l'accessibilite.
- Parametres > Accessibilite enrichi : descriptions des effets contraste, texte,
  reduction de mouvement, focus clavier et rappel des raccourcis clavier utiles.
- Barre de favoris legerement aeree : hauteur 28 px, espacement horizontal augmente,
  boutons 24 px et padding legerement plus confortable.
- Passage de version a `0.60.3-dev`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` :
  233 tests reussis.
- `build-winui.cmd` : restore/build WinUI reussis apres autorisation reseau
  NuGet, 0 avertissement, 0 erreur.
- `scripts\build-clean-test-artifact.ps1 -Version 0.60.3-dev` : publish
  autonome reussi apres autorisation reseau NuGet.
- Artefact propre :
  `artifacts\clean-test\PulseBrowser-0.60.3-dev-win-x64-clean-20260712-133244`.
- SHA256 exe hote : `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`.
- `scripts\build-installer.ps1 -Version 0.60.3-dev` : installateur genere
  apres autorisation reseau NuGet pour le projet setup.
- Installateur :
  `artifacts\installer\PulseBrowserSetup-0.60.3-dev-win-x64.exe`.
- Taille installateur : 69 019 786 octets.
- SHA256 installateur :
  `eb1fed96bb4858e3f7ae8d56d24c7676b8bae052aac4ac6bf8cbcd514a435092`.
- Non teste manuellement par l'IA : rendu visuel du plein ecran, position des
  menus en conditions multi-ecran/plein ecran, lecture reelle au lecteur
  d'ecran et confirmation que le champ d'accueil ne reprend plus l'ancien PIN
  sur le poste utilisateur.
