# 2026-07-17 - Modes avec outils contextuels (0.83.7-dev)

Suite au retour utilisateur, les modes Lumora ne doivent pas seulement changer
d'ambiance : chacun doit proposer une fonction visible et une courte
presentation de ce qu'il apporte.

- Version passee a `0.83.7-dev`.
- Ajout d'une presentation de mode sur `lumora://accueil`.
- La presentation peut etre masquee par `Compris` et reste memorisee par mode
  dans les preferences locales (`LastIntroducedUsageMode`).
- Changer de mode remet la presentation a afficher pour le nouveau contexte.
- Ajout d'un outil contextuel sur l'accueil pour chaque mode :
  `Equilibre`, `Focus`, `Lecture`, `Creation`, `Recherche`, `Nuit`.
- Le mode `Creation` affiche un post-it local de capture d'idee.
- Les autres modes proposent aussi une saisie utile : objectif, note de
  lecture, piste de recherche ou rappel calme.
- Les notes rapides sont enregistrees via le module Notes Lumora et le
  `NoteStore` local du profil.
- Ajout d'un retour `aria-live`, de libelles accessibles et du respect du
  rendu statique quand les animations sont reduites.
- Documentation ajoutee :
  `docs/MODES_OUTILS_CONTEXTUELS_0_83_7.md`.
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora effectue.

**Verification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` hors sandbox
  apres blocage ACL local : 511/511 tests reussis.
- Restore MSBuild WinUI : reussie, 0 avertissement, 0 erreur.
- Build WinUI valide avec sortie alternative
  `artifacts\build-verify\winui-0.83.7-debug\` : 0 avertissement, 0 erreur.

**Version :** `0.83.7-dev`.
