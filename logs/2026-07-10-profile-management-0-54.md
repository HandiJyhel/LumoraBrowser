# 2026-07-10 - Gestion des utilisateurs/profils 0.54.0-dev

## Objectif

Transformer la base multi-profil existante en une surface de gestion plus claire dans l'application, directement depuis `Parametres > Profil`.

## Changements

- Passage de la version source a `0.54.0-dev`.
- Ajout d'une section `Gestion des utilisateurs` dans `Parametres > Profil`.
- Affichage des profils utilisateurs locaux detectes sur l'ordinateur.
- Affichage explicite du profil actif et du chemin de chaque profil.
- Action `Basculer` vers un profil non actif, avec redemarrage pour eviter le melange des stores deja charges.
- Action `Ouvrir le dossier` pour inspecter l'emplacement reel du profil.
- Action `Mettre en quarantaine` pour les profils non actifs seulement.
- La mise en quarantaine deplace le dossier dans `.pulsebrowser-profile-quarantine` a cote des profils, au lieu de detruire immediatement les donnees.
- Confirmation par saisie du nom du profil avant quarantaine.
- Ajout de tests `ProfileRegistryTests`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 157/157 tests verts.
- Premier `build-winui.cmd` bloque par le sandbox reseau NuGet (`NU1301`), puis relance autorisee.
- `build-winui.cmd` : 0 avertissement, 0 erreur.
- Aucun installateur ni artifact de distribution regenere pendant cette etape.
