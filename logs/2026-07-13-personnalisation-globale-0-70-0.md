# Personnalisation globale Lumora - 0.70.0-dev

## Contexte

L'utilisateur voulait une vraie personnalisation globale du navigateur :
avatar personnalise durable, visible pendant la navigation, menus mieux
organises et bouton d'application general dans les parametres.

## Changements

- Deplacement de la gestion de l'avatar vers `Parametres > Personnalisation`.
- Conservation de l'avatar comme fichier local du profil, jamais synchronise ni
  envoye a un serveur.
- Ajout d'une barre d'action globale en bas des parametres avec `Appliquer les
  changements` et `Annuler`.
- Suppression du bouton de validation limite a la seule section
  Personnalisation.
- Rangement de la navigation des parametres : Navigation, Personnalisation,
  Profil, Confidentialite, Coffre, Accessibilite, Stockage, Demarrage.
- Le bloc profil du bandeau d'etat devient cliquable et ouvre directement la
  personnalisation.
- Le bandeau d'etat reste visible pendant la navigation normale, avec avatar et
  etat du profil.
- Le changement de nom de profil met aussi a jour le bandeau d'etat.

## Verification

- `cmd /c .\build-winui.cmd` : reussi, 0 avertissement, 0 erreur.
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` :
  276/276 tests verts.
- Le premier build sous sandbox a ete bloque par l'acces reseau NuGet, puis le
  build hors sandbox a restaure depuis le cache/source NuGet et compile
  correctement.

**Version :** `0.70.0-dev`.
