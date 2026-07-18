# 2026-07-18 - Modes Neutre et Equilibre distincts (0.83.13-dev)

Suite au retour utilisateur, le mode Neutre et le mode Equilibre etaient trop
proches visuellement. Le logo Lumora avait aussi ete trop retire du mode
Neutre.

- Version passee a `0.83.13-dev`.
- Correction de normalisation : `balanced` est maintenant reconnu explicitement
  par l'accueil et par les messages WebView2.
- Le mode Neutre affiche de nouveau le logo Lumora, de maniere discrete, avec
  le nom, l'heure et la recherche.
- Ajout d'un accueil dedie au mode Equilibre :
  `balanced-home`, `balanced-lead`, `balanced-dock`.
- Equilibre affiche une surface quotidienne plus identifiable :
  marque Lumora, recherche, raccourcis, presentation de mode et actions
  Favoris / Historique / Modules.
- Les animations et transitions tiennent compte des nouvelles classes
  `balanced-*` et `neutral-*`.
- Tests ajoutes pour verrouiller la distinction entre `neutral` et `balanced`.
- Documentation ajoutee :
  `docs/MODE_NEUTRE_EQUILIBRE_DISTINCTS_0_83_13.md`.
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora effectue.

**Verification** :

- `dotnet test` hors sandbox apres blocage NuGet/ACL local : 514/514 tests
  reussis.
- Restore MSBuild WinUI hors sandbox : 0 avertissement, 0 erreur.
- Build WinUI valide avec sortie alternative
  `artifacts\build-verify\winui-0.83.13-debug\` : 0 avertissement, 0 erreur.

**Version :** `0.83.13-dev`.
