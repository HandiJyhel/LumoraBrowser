# Release 1.0.0 r10 — 2026-09-02

10e révision de la release 1.0.0, générée le 2026-09-02 via protocole worktree stable.

## Version affichée
- **"1.0.0 r10"** dans "À propos" (ReleaseVersion modifié uniquement dans le worktree)

## Artefacts produits
- Installateur: `artifacts/installer/LumoraSetup-1.0.0-win-x64.exe`
- SHA256 manifest: `artifacts/signatures/LumoraSetup-1.0.0-20260902-134548.sha256`
  - Hash: `5a6e5ee8b3d4aff12786a01ab43b533550640174b35f1046beb5a0654b290fb7`
- Fichier `.VERIFICATION.txt`

## Vérification live réussie ✅
- Titre de fenêtre: **"Lumora 1.0.0 r10"** (EXACT)
- Lancement en mode invité: OK
- Aucune ligne UNHANDLED dans le journal
- SHA256 vérifiés deux fois (script + Get-FileHash indépendant)

## Protocole appliqué (étapes complètes)
1. ✅ Worktree créé sur HEAD (4ba3f03 Update MEMORY.md)
2. ✅ Runtime WebView2 Fixed Version copié (150.0.4078.105)
3. ✅ ReleaseVersion = "1.0.0 r10" dans le worktree
4. ✅ Builds réussis (clean-test + installer)
5. ✅ Vérification live passée
6. ✅ SHA256 vérifiés
7. ✅ Artefacts copiés vers dépôt principal **AVANT** suppression worktree
8. ✅ Worktree supprimé avec --force
9. ✅ ReleaseVersion = null reconfirmé sur main (ligne 60 de MainWindow.xaml.cs)

## Notes
- Voir [[release-1-0-0-protocole-worktree]] pour les détails du protocole réutilisé.
- Titre de fenêtre exact = "Lumora 1.0.0 r10" confirmé en vérification live.
