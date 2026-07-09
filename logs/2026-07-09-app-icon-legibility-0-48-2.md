# 2026-07-09 — Lisibilité de l'icône d'application 0.48.2-dev

- Retour utilisateur : « j'avais défini une icône propre à l'application, pourquoi elle n'apparaît plus ? ».
- Investigation avant tout code : journal de démarrage (`ApplyAppIcon()` sans exception), extraction Win32 de l'icône réellement appliquée à la fenêtre (`WM_GETICON`), extraction de l'icône embarquée dans l'exe (copie fraîche + `ExtractAssociatedIcon`, pour écarter le cache Shell). Les deux montrent une tache orange floue. Le fichier source `.ico` lui-même, à 256×256, montre le vrai logo net — donc pas de régression de code : le motif détaillé ne survit pas à la réduction en dessous de 64px.
- Cause : `scripts/generate-app-icon.ps1` dessine le même tourbillon (traits fins, courbes, petit point d'accent) à toutes les tailles avec une mise à l'échelle linéaire — jamais vérifié visuellement à la taille réelle depuis 0.44.1-dev.
- Correction : rendu simplifié pour 16/24/32/48px (traits grossis proportionnellement ×3, bordure/surbrillance/petit point retirés, point central agrandi), rendu détaillé inchangé pour 64/128/256px.
- Choix utilisateur (question posée) : simplifier le tourbillon pour le petit format plutôt que de basculer sur la tuile « P » utilisée dans l'app.
- Itération : un premier essai avec un plancher absolu (« au moins 96px effectifs ») rendait l'épaisseur identique à 16 et 48px, écrasant le petit format — remplacé par un facteur proportionnel (×3) qui grossit sans aplatir la progression entre tailles.
- Vérification : extraction et inspection visuelle de chaque taille (16 à 256) avant et après ; 32/48/64/128/256 nets, 16/24 limités par la physique du pixel (anneau + point + fond dans 256px, limite reconnue et documentée). Icône réellement appliquée à la fenêtre en direct et icône embarquée dans l'exe toutes deux re-vérifiées après reconstruction : anneau net.
- `dotnet test` : 94/94 verts (aucun changement C#). `build-winui.cmd` : 0 avertissement, 0 erreur. Lancement court : fenêtre `Pulse Browser 0.48.2-dev` répondante.

**Version :** `0.48.2-dev`.
