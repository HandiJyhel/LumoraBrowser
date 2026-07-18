# 2026-07-17 - Modes compagnons et accueil aere (0.83.8-dev)

Suite au retour utilisateur, l'accueil des modes etait trop centre et compacte.
Le besoin clarifie est aussi de faire evoluer les modes vers des compagnons
disponibles pendant la navigation, pas seulement sur `lumora://accueil`.

- Version passee a `0.83.8-dev`.
- `lumora://accueil` passe d'un empilement central a une composition en deux
  zones sur desktop : marque/recherche/reperes a gauche, mode/outil a droite.
- Le responsive garde une colonne simple sur petite largeur.
- Ajout d'un bouton permanent `Compagnon du mode` dans le chrome, a cote du
  bouton `Mode d'usage`.
- Le compagnon adapte son icone, ses textes et ses actions au mode actif.
- Actions raccordees :
  - `Equilibre` : modules et palette de commande.
  - `Focus` : palette de commande et plein ecran.
  - `Lecture` : mode lecture et notes.
  - `Creation` : notes/post-it et assistant de recherche si actif.
  - `Recherche` : historique local et favoris.
  - `Nuit` : mode lecture et voix locale.
- Le compagnon utilise les modules locaux existants et n'ajoute aucun service
  distant.
- Le bouton compagnon est relie aux traitements d'accessibilite et a la palette
  visuelle du mode actif.
- Documentation ajoutee :
  `docs/MODES_COMPAGNONS_ACCUEIL_AERE_0_83_8.md`.
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora effectue.

**Verification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` hors sandbox
  apres blocage ACL local : 512/512 tests reussis.
- Restore MSBuild WinUI : reussie, 0 avertissement, 0 erreur.
- Build WinUI valide avec sortie alternative
  `artifacts\build-verify\winui-0.83.8-debug\` : 0 avertissement, 0 erreur.

**Version :** `0.83.8-dev`.
