# 2026-07-19 - Build WinUI isolee et script de build repare

## Contexte

Le lot `0.83.35-dev` sur le confort par site avait supprime une erreur
de compilation liee a `WebView2.ZoomFactor`, mais la build WinUI locale
restait fragile. Selon l'etat du poste, `scripts/build-winui.ps1`
retombait ensuite sur :

- des fichiers generes obsoletes encore presents dans `Lumora.WinUI\obj` ;
- des doublons de compilation quand l'intermediaire etait deplace ;
- des copies vers `Lumora.WinUI\bin` qui echouaient a cause de fichiers
  verrouilles.

L'objectif de ce correctif etait de rendre la build locale fiable sans
nettoyage manuel destructif.

## Cause reelle

Le probleme venait de trois sources qui s'alimentaient entre elles :

1. des restes historiques dans `Lumora.WinUI\obj` et `Lumora.WinUI\bin` ;
2. le fait que certains targets WinUI/XAML utilisent `OutputPath`, pas
   seulement `OutDir`, pour copier les fichiers generes ;
3. quand `BaseIntermediateOutputPath` quittait le dossier `obj` du
   projet, les globs SDK recommencaient a voir `Lumora.WinUI\obj\**` comme
   des fichiers source normaux si ce dossier n'etait pas explicitement
   exclu.

## Correctifs appliques

- Creation de `scripts/winui-build-common.ps1`.
- Ajout d'un contexte de build isole sous
  `artifacts\tmp\winui-build\Lumora.WinUI\...`.
- Redirection des proprietes MSBuild suivantes :
  - `BaseIntermediateOutputPath`
  - `MSBuildProjectExtensionsPath`
  - `OutputPath`
  - `OutDir`
- Copie des metadonnees NuGet de `Lumora.WinUI\obj` vers le contexte
  isole pour reutiliser le cache local.
- Saut du restore reseau si un cache local exploitable est deja present.
- Migration de `scripts/build-winui.ps1`, `scripts/run-winui.ps1` et
  `scripts/build-clean-test-artifact.ps1` vers ce helper.
- Ajout dans `Lumora.WinUI.csproj` de l'exclusion explicite de
  `bin\**` et `obj\**` via `DefaultItemExcludes`.

## Verification

Commande verifiee :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1
```

Resultat :

- build WinUI complete reussie ;
- `0 avertissement` ;
- `0 erreur` ;
- temps observe : `00:00:39.10`.

Commande de lancement verifiee :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-winui.ps1
```

Resultat :

- build isolee reussie ;
- executable `Lumora.WinUI.exe` trouve dans le dossier de sortie isole ;
- lancement delegue a `Start-Process` sans erreur.

Sortie produite dans :

```text
artifacts\tmp\winui-build\Lumora.WinUI\x64\Debug\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\
```

## Impact

La build locale ne depend plus de l'etat des anciens `obj/bin` du projet
et le script standard `scripts/build-winui.ps1` redevient utilisable dans
un environnement hors ligne ou avec des fichiers verrouilles residuels.
