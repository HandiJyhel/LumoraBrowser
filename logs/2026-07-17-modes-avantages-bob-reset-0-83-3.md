# 2026-07-17 - Modes avec avantages et reset Bob (0.83.3-dev)

Suite au retour utilisateur, les modes d'usage Lumora ne doivent plus etre de
simples libelles. Chaque mode applique maintenant une posture visible et utile.

- Version passee a `0.83.3-dev`.
- Les changements de mode appliquent des presets non destructifs :
  - `Focus` : accueil minimal, recherche focalisee, interface compacte, palette
    de commande activee.
  - `Lecture` : accueil calme, modules lecture/notes/voix locale rapproches.
  - `Creation` : animations dynamiques, notes, assistant de recherche et
    raccourcis rapides.
  - `Recherche` : onglets verticaux, favoris visibles, suggestions locales et
    outils de collecte.
  - `Nuit` : theme sombre, interface compacte, rendu calme et outils de lecture.
  - `Equilibre` : retour a une posture standard.
- `lumora://accueil` affiche une zone d'avantages propre au mode actif, avec
  des boutons relies aux panneaux ou modules utiles.
- Le profil local actif `default`, utilise comme profil test Bob, a ete supprime
  de `%LOCALAPPDATA%\Lumora\profiles`.
- Le profil `testcoffre` n'a pas ete supprime.
- Aucun installateur ni executable de release genere.

**Verification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 507 tests
  reussis.
- `powershell -ExecutionPolicy Bypass -File scripts\build-winui.ps1` : build
  WinUI reussi, 0 avertissement, 0 erreur.
- `run-winui.cmd` avec `LUMORA_PROFILE_DIR` temporaire isole : restore/build
  reussis, fenetre lancee avec le titre `Lumora 0.83.3-dev`, handle principal
  non nul et application repondante.
- Profil temporaire de verification supprime.
- Profil Bob `default` toujours absent de `%LOCALAPPDATA%\Lumora\profiles`.

**Version :** `0.83.3-dev`.
