# Coffre : doublons et affichage ; repli HTTP affine - 0.70.3-dev

## Contexte

Deux retours utilisateur :

1. « Dans mon coffre a mots de passe c'est le bordel, plein de logos, plein de
   trucs qui ne vont pas » — apres l'import depuis son gestionnaire tiers.
2. Une boite « passer en HTTP parce que le site ne gere pas HTTPS » l'a surpris ;
   demande d'explication et validation du refus automatique des cookies (deja
   actif par defaut, rien a changer).

## Diagnostic coffre

- Balayage des `FontIcon` du projet : un seul glyphe reellement casse, le
  bouton supprimer des passkeys (`MainWindow.Vault.cs`, chaine vide — caractere
  perdu). Les 3 icones des cartes du coffre (globe/renommer/supprimer) etaient
  intactes.
- `DisplayName` conservait le prefixe `www.` : « amazon.fr » et
  « www.amazon.fr » affichaient deux noms differents pour le meme site et se
  triaient a deux endroits opposes de la liste.
- Aucun outil de fusion des doublons d'import : le meme compte enregistre sous
  plusieurs origines du meme site (www./apex/sous-domaines) restait en
  plusieurs cartes.

## Changements

- `MainWindow.Vault.cs` : glyphe corbeille (U+E74D) retabli sur le bouton
  supprimer des passkeys.
- `PasswordManagerService.DisplayName` : retire le prefixe `www.` (le nom
  personnalise reste prioritaire).
- `PasswordManagerService.FindDuplicates()` / `MergeDuplicates()` : un doublon
  = meme domaine racine + meme identifiant (casse ignoree) + MEME mot de passe.
  Deux entrees dont le mot de passe differe ne sont jamais fusionnees
  (multi-comptes, rotation). L'entree conservee est la plus renseignee (nom
  perso, page de connexion) puis la plus recente ; suppression par Id avec
  tombstone (pas de reimport Chromium).
- `MainWindow.xaml` + `MainWindow.Vault.cs` : bouton « Fusionner les doublons »
  (balai, U+EA99) dans l'en-tete du panneau coffre — detection d'abord, dialogue
  de confirmation avec le nombre exact, puis fusion et statut.
- `MainWindow.Navigation.cs` : le repli HTTPS->HTTP n'est propose que si
  l'echec signifie vraiment « pas de HTTPS utilisable » (certificat invalide /
  expire / revoque, connexion refusee ou reinitialisee, reponse serveur
  invalide). Timeout, DNS, reseau coupe, annulation : plus de proposition — une
  panne transitoire toucherait aussi le HTTP et le dialogue etait un faux
  signal « site en HTTP » pour des sites sains.

## Verification

- Build Debug via MSBuild.exe (vswhere) : OK, 0 erreur (app + tests).
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-build` : 283/283 verts,
  dont 7 nouveaux (`VaultDuplicateMergeTests`) : fusion www/apex/sous-domaine,
  jamais deux mots de passe differents, comptes distincts preserves, entree la
  plus renseignee conservee, dry-run sans suppression, DisplayName sans www.
  et priorite au nom personnalise.
- Artefact propre Release (0 avertissement, 0 erreur) :
  `artifacts\clean-test\Lumora-0.70.3-dev-win-x64-clean-20260713-164236`.
- SHA256 de `Lumora.WinUI.exe` :
  `800e54e340a8051e2d6d1309cce34aca5c04fa524f00122c5ec107152f150037`.
- Contenu verifie dans l'artefact : chaines `0.70.3-dev` et `Fusion terminee`
  (UTF-16) presentes dans `Lumora.WinUI.dll`, `MainWindow.xbf` present.
- Manifeste artefact :
  `artifacts\signatures\Lumora-0.70.3-dev-clean-20260713-164304.sha256`.
- Installateur :
  `artifacts\installer\LumoraSetup-0.70.3-dev-win-x64.exe`.
- SHA256 installateur :
  `cf7d7f4263c37eafa35d705e481c10918d6d0316ed8775ea1b5fe63273f21cdd`.
- Manifeste installateur :
  `artifacts\signatures\LumoraSetup-0.70.3-dev-20260713-164543.sha256`.
- Pas de validation manuelle interactive dans cette passe : bouton
  « Fusionner les doublons » a essayer sur le coffre reel de l'utilisateur
  (dialogue de confirmation avant toute suppression).

**Version :** `0.70.3-dev`.
