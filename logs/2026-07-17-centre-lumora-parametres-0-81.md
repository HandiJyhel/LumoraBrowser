# 2026-07-17 - Centre Lumora pour les paramètres (0.81.0-dev)

Retour utilisateur : les paramètres faisaient trop Google/Chrome. L'objectif
est de s'écarter de cette logique quand c'est possible et de faire des
paramètres un espace plus Lumora, orienté confort, productivité et données
locales.

- Version passée à `0.81.0-dev`.
- Le panneau `Paramètres` devient visuellement `Centre Lumora`.
- Ajout d'une section `Vue d'ensemble` avec cartes d'accès rapide :
  `Mon Lumora`, `Espace de travail`, `Vie privée locale`, `Coffre et données`,
  `Profils locaux`, `Confort`.
- La navigation latérale est réorganisée par intention :
  `Espace personnel`, `Travail quotidien`, `Données locales`.
- Les sections visibles sont renommées pour sortir d'une organisation trop
  navigateur classique : `Mon Lumora`, `Espace de travail`, `Ouverture`,
  `Vie privée locale`, `Coffre et données`, `Profils locaux`,
  `Stockage local`, `Confort`.
- Les tags et handlers internes existants sont conservés pour éviter une
  régression fonctionnelle.
- Aucun installateur ni exécutable de release n'a été généré.

**Vérification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 507/507 tests
  verts.
- `powershell -ExecutionPolicy Bypass -File scripts\build-winui.ps1` : build
  WinUI réussi hors sandbox après blocage NuGet attendu dans le sandbox,
  0 avertissement, 0 erreur.

**Version :** `0.81.0-dev`.
