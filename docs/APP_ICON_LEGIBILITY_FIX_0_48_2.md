# Lisibilité de l'icône d'application — 0.48.2-dev

## Symptôme rapporté

L'utilisateur avait défini une icône propre à l'application (tourbillon
orange/noir, 0.44.1-dev) mais ne la « voyait plus » dans la barre des
tâches / barre de titre.

## Diagnostic

Ce n'était pas une régression de code. Vérifié précisément :

- `WinUiRuntimeTrace` (journal opt-in `PULSE_BROWSER_TRACE_STARTUP=1`) :
  `ApplyAppIcon()` s'exécute sans exception.
- Icône **réellement appliquée** à la fenêtre en cours d'exécution,
  extraite via `WM_GETICON` (Win32) sur le process réel : une tache orange
  floue, méconnaissable.
- Icône **embarquée dans l'exe** (`<ApplicationIcon>`), extraite via
  `System.Drawing.Icon.ExtractAssociatedIcon` sur une copie fraîche du
  binaire (pour écarter tout cache d'icône Shell Windows) : la même tache
  floue.
- Le fichier source `Assets/PulseBrowser.ico` lui-même, à sa taille native
  (256×256), affiche bien le tourbillon Pulse net et reconnaissable. Mais
  extrait à 16×16, c'est la même tache floue.

**Cause** : `scripts/generate-app-icon.ps1` dessinait le même motif détaillé
(traits fins, courbes, petit point d'accent) à toutes les tailles, mis à
l'échelle linéairement. Un motif pensé pour un rendu à 1024px devient illisible
une fois ses traits réduits à moins d'un pixel de large — problème classique
de design d'icône, jamais vérifié visuellement à la taille réelle d'affichage
depuis son introduction en 0.44.1-dev.

## Correction

`scripts/generate-app-icon.ps1` : rendu **simplifié** pour les tailles ≤ 48px
(16/24/32/48), rendu détaillé inchangé pour 64/128/256 :

- Épaisseur des traits (orange et charcoal) grossie proportionnellement
  (facteur ×3, pas un plancher absolu — 16px reste plus fin que 48px).
- Bordure du carré, trait de surbrillance et petit point d'accent retirés
  (détails invisibles ou parasites en dessous de 48px).
- Point central agrandi pour rester un ancrage visuel net.

## Vérification

- Extraction et inspection visuelle de chaque taille générée (16 à 256px) :
  32/48/64/128/256 nets et reconnaissables comme le motif « anneau/pulse ».
  16/24 restent limités (badge orange plein, anneau à peine perceptible) —
  limite physique réelle : à 16×16, un anneau + point central + fond carré
  ne tient tout simplement pas dans 256 pixels de façon détaillée ; c'est
  vrai pour la plupart des logos non conçus dès le départ pour cette taille.
- Icône **réellement appliquée** à la fenêtre en direct re-vérifiée via
  `WM_GETICON` après reconstruction : anneau orange/charcoal net et
  reconnaissable (avant/après confirmé par capture).
- Icône embarquée dans l'exe re-vérifiée de la même façon (copie fraîche +
  `ExtractAssociatedIcon`) : même résultat net.
- `dotnet test` : 94/94 verts (aucun changement de logique C#, uniquement le
  script de génération d'assets et les fichiers `.ico`/`.png` régénérés).
- `build-winui.cmd` : 0 avertissement, 0 erreur.
- Lancement court : fenêtre `Pulse Browser 0.48.2-dev` répondante.
