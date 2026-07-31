# Studio compact, focus scroll et ecran PIN Lumora - 0.84.0.27-dev

## Contexte

Le `Studio Lumora` etait devenu trop fourni pour une personnalisation censee
rester immediate. En parallele, l'utilisateur remontait un probleme de molette
et d'ascenseurs qui ne reagissaient pas naturellement sans clic prealable, ainsi
qu'un ecran de connexion PIN encore trop proche d'une base generique.

## Changements

- simplification du `Studio Lumora` dans `Lumora.WinUI/MainWindow.xaml` :
  - suppression du surplus de cartes et de texte ;
  - retour a une structure courte : resume, trois presets, ajustement
    onglets/favoris et ambiance ;
  - conservation du caractere Lumora sans obliger l'utilisateur a chercher ;
- correction de focus au survol pour les zones scrollables et la page web :
  - focus automatique des `ScrollViewer` au passage du pointeur ;
  - focus automatique du `WebView2` au survol de la zone web ;
  - meme logique appliquee aussi a la fenetre Incognito ;
- relecture graphique de l'overlay de connexion :
  - ambiance plus Lumora avec carte de tete plus marquee ;
  - pave PIN plus present et plus lisible ;
  - contraste plus net entre le fond, la carte et les actions ;
- montee de version source a `0.84.0.27-dev`.

## Verification

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
  : succes, `0 avertissement`, `0 erreur` ;
- `dotnet test .\Lumora.Tests\Lumora.Tests.csproj --no-restore --filter UsageModeVisualIdentityTests -p:BaseIntermediateOutputPath=artifacts\tmp\tests\obj\ -p:MSBuildProjectExtensionsPath=artifacts\tmp\tests\obj\`
  : succes.

## Impact

Le Studio redevient un point de commande rapide au lieu d'un mini panneau trop
charge. La molette doit maintenant suivre le survol plus naturellement sur les
zones scrollables et les pages web. L'ecran PIN gagne enfin une presence plus
coherente avec l'identite Lumora.
