# 2026-07-17 - Identite visuelle des modes (0.83.5-dev)

Suite au retour utilisateur, les modes d'usage Lumora doivent etre plus
identifiables visuellement des la premiere seconde.

- Version passee a `0.83.5-dev`.
- `lumora://accueil` gagne une signature visuelle propre aux modes :
  barre laterale de mode, motif de panneau, indicateur graphique et densite de
  rendu differenciee.
- Les fonds d'accueil deviennent plus distincts pour `Focus`, `Lecture`,
  `Creation`, `Recherche` et `Nuit`.
- Ajout de micro-animations propres aux modes :
  rythme bref pour `Focus`, flux lent pour `Lecture`, reaction plus vive pour
  `Creation`, balayage structurel pour `Recherche`, pulsation douce pour `Nuit`.
- Les options d'accessibilite `Reduire les animations` et contraste renforce
  conservent la priorite et coupent les animations.
- Ajout de tests de regression textuels pour eviter de retirer la signature
  visuelle des modes ou le chemin statique par erreur.
- Aucun installateur ni executable de release genere.

**Verification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` hors sandbox
  apres blocage ACL local : 509/509 tests reussis.
- `scripts\build-winui.ps1` dans le sandbox bloque par NuGet/ACL, puis hors
  sandbox compile mais ne peut pas remplacer `Lumora.WinUI.exe` car une instance
  utilisateur est ouverte (`Lumora.WinUI (2272)`).
- Build WinUI valide avec sortie alternative :
  `MSBuild Lumora.WinUI\Lumora.WinUI.csproj /t:Build /p:Configuration=Debug
  /p:Platform=x64 /p:OutDir=artifacts\build-verify\winui-0.83.5-debug\` :
  0 avertissement, 0 erreur.

**Version :** `0.83.5-dev`.
