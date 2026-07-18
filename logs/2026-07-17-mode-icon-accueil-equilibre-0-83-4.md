# 2026-07-17 - Icône de mode et accueil Équilibre (0.83.4-dev)

Suite au retour utilisateur, l'accueil en mode `Équilibre` est simplifié et le
changement de mode devient accessible en permanence depuis la barre principale.

- Version passée à `0.83.4-dev`.
- Suppression des cartes d'actions artificielles en mode `Équilibre`.
- Les cartes d'avantages restent visibles uniquement pour les modes spécialisés.
- Ajout d'un bouton permanent `Mode d'usage` à côté du bouton modules.
- L'icône du bouton change selon le mode actif.
- Le bouton ouvre un sélecteur rapide avec `Équilibre`, `Focus`, `Lecture`,
  `Création`, `Recherche` et `Nuit`.
- Le changement de mode par ce bouton applique les mêmes presets que les autres
  sélecteurs et synchronise les contrôles existants.
- Aucun profil local supprimé pendant cette étape.
- Aucun installateur ni exécutable de release généré.

**Vérification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 507 tests
  réussis.
- `powershell -ExecutionPolicy Bypass -File scripts\build-winui.ps1` : build
  WinUI réussi, 0 avertissement, 0 erreur.
- `run-winui.cmd` avec `LUMORA_PROFILE_DIR` temporaire isolé : restore/build
  réussis, fenêtre lancée avec le titre `Lumora 0.83.4-dev`, handle principal
  non nul et application répondante.
- Profil temporaire de vérification supprimé.

**Version :** `0.83.4-dev`.
