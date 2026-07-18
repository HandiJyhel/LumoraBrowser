# 2026-07-17 - Personnalisation et animations Lumora (0.80.0-dev)

Retour utilisateur : Lumora devenait fonctionnel, mais restait trop basique en
personnalisation par rapport a des navigateurs comme Opera ou Zen. Objectif :
que l'utilisateur puisse vraiment habiter son navigateur, se sentir a l'aise et
etre plus productif, sans copier Chrome ni transformer Lumora en interface
generique.

- Version passee a `0.80.0-dev`.
- Ajout de `UiSettings.PersonalizationMotionStyle`, persiste par profil.
- Ajout du bloc `Personnalite Lumora` dans `Parametres > Personnalisation`,
  avec le reglage `Animations et reactions` : `Discret`, `Lumineux`,
  `Dynamique`.
- Le reglage est applique via le bouton global existant
  `Appliquer les changements`, comme les autres options de personnalisation.
- `lumora://accueil` gagne une couche d'animation locale : entree douce de la
  marque, respiration lumineuse du logo, ligne lumineuse animee, apparition
  progressive des raccourcis et trace de lumiere modulee selon le style.
- Le reglage d'accessibilite `Reduire les animations` et le contraste renforce
  forcent un rendu statique.
- Aucun installateur ni executable de release n'a ete genere, conformement a la
  nouvelle regle projet.

**Verification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 507/507 tests
  verts (hors sandbox apres refus d'ecriture dans `obj`).
- `powershell -ExecutionPolicy Bypass -File scripts\build-winui.ps1` : build
  WinUI reussi hors sandbox, 0 avertissement, 0 erreur.

**Version :** `0.80.0-dev`.
