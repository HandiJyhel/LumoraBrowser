# 2026-07-17 - Modes d'usage et accueil vivant (0.82.0-dev)

Suite au retour utilisateur demandant des idees plus originales, Lumora gagne
une premiere brique visible de personnalisation comportementale : un mode
d'usage qui change la posture de l'accueil.

- Version passee a `0.82.0-dev`.
- Ajout de `UiSettings.UsageMode`, persiste par profil.
- Ajout du selecteur `Mode d'usage` dans `Parametres > Mon Lumora`.
- Modes disponibles : `Equilibre`, `Focus`, `Lecture`, `Creation`,
  `Recherche`, `Nuit`.
- `lumora://accueil` affiche une capsule vivante avec salutation locale,
  nom du mode, intention du mode et actions suggerees.
- La capsule respecte les reglages existants de theme, palette, contraste et
  reduction des animations.
- Aucun service distant, aucune synchronisation, aucun installateur genere.

**Verification** :
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 507/507 tests verts.
- `powershell -ExecutionPolicy Bypass -File scripts\build-winui.ps1` : build WinUI reussi hors sandbox apres blocage NuGet/obj attendu dans le sandbox, 0 avertissement, 0 erreur.

**Version :** `0.82.0-dev`.
