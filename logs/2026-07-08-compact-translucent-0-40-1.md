# 2026-07-08 - Interface compacte et translucidite - 0.40.1-dev

## Contexte

Suite au retour utilisateur : le mode compact inspire de Zen masquait la barre des favoris, ce qui annulait l'interet du travail recent sur la gestion des favoris. L'utilisateur a aussi demande d'explorer des fonctions inspirees de Zen Browser, notamment une interface translucide.

## Changements

- Version projet passee a `0.40.1-dev` dans `AGENTS.md` et `MainWindow.xaml.cs`.
- Renommage visible du reglage `Mode compact inspire de Zen` en `Interface compacte`.
- Correction : l'interface compacte ne masque plus la barre de favoris par defaut.
- Ajout du reglage separe `Masquer les favoris en interface compacte`, desactive par defaut.
- Ajout du champ persistant `CompactModeHidesBookmarks` dans `UiSettings`.
- Ajout d'un reglage `Effet translucide` dans `Parametres > Apparence` avec choix `Desactive`, `Mica` et `Acrylic`.
- Ajout du champ persistant `WindowBackdrop` dans `UiSettings`.
- Application de `MicaBackdrop` ou `DesktopAcrylicBackdrop` quand disponible, avec retour au rendu solide en cas d'erreur.
- Ajustement des surfaces du chrome Pulse pour laisser apparaitre l'effet translucide sans sacrifier la lisibilite.

## Verification

- Premiere tentative `build-winui.cmd` bloquee par l'acces NuGet du bac a sable (`NU1301`).
- Relance autorisee avec acces reseau : restore WinUI reussi, build WinUI reussi avec 0 avertissement et 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` reussi : fenetre `Pulse Browser 0.40.1-dev`, processus repondant, fermeture du processus de test.
