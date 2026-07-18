# 2026-07-17 - Mode d'usage dans Modules et reset Bob (0.83.2-dev)

Suite au retour utilisateur, Lumora rend le changement de mode plus accessible
depuis le hub `Modules Lumora`.

- Version passée à `0.83.2-dev`.
- Ajout d'un sélecteur `Mode d'usage` au début du panneau `Modules Lumora`.
- Le sélecteur applique immédiatement le mode choisi.
- Le sélecteur `Mon Lumora` reste synchronisé.
- Les pages `lumora://accueil` ouvertes sont rafraîchies après changement.
- Le profil actif local `default`, utilisé comme profil test Bob, a été remis
  en état d'interface neuve : modules épinglés vides, raccourcis du nouvel
  onglet vidés/masqués, mode `balanced`, wizard de personnalisation non terminé.
- Aucun installateur ni exécutable de release généré.

**Vérification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 507 tests
  réussis.
- `powershell -ExecutionPolicy Bypass -File scripts\build-winui.ps1` : build
  WinUI réussi, 0 avertissement, 0 erreur.
- `run-winui.cmd` : restore/build réussis, fenêtre lancée avec le titre
  `Lumora 0.83.2-dev`, handle principal non nul et application répondante.

**Version :** `0.83.2-dev`.
