# Icône Téléchargements dans la barre d'outils - 0.84.0.8-dev

## Contexte

Après 0.84.0.7-dev (suppression de la boîte de dialogue native WebView2/Edge),
l'utilisateur a remarqué que le téléchargement fonctionne mais qu'il n'y a
plus aucune indication visible qu'un fichier a bien été téléchargé - la boîte
native supprimée était, de fait, le seul retour visuel existant. Demande :
un comportement proche de Google Chrome (icône dans la barre d'outils pendant
et après un téléchargement).

Le panneau "Téléchargements" existant est enterré à trois niveaux de menu
(Menu Lumora > Bibliothèque > Téléchargements), donc invisible pendant un
téléchargement actif.

## Changement

Reprise du patron déjà utilisé pour l'icône "Popups en attente" (0.84.0.6) :
même style de bouton (`NovaModuleIconButtonStyle`), même emplacement dans la
barre de modules, même structure badge + flyout.

- `DownloadsIndicatorButton` (nouveau, `MainWindow.xaml`) : masqué tant
  qu'aucun téléchargement n'a eu lieu durant la session. Devient visible dès
  le premier téléchargement démarré et le reste pour la session.
- Badge de compte (`DownloadsIndicatorBadge`/`Text`) : nombre de
  téléchargements "non vus" (démarrés depuis la dernière ouverture du
  flyout ou du panneau complet). Remis à zéro à l'ouverture de l'un ou
  l'autre.
- Flyout (`DownloadsIndicatorFlyout`) : liste les 5 téléchargements les plus
  récents en réutilisant `BuildDownloadCard` (déjà utilisé par le panneau
  complet - même rendu, mêmes boutons Ouvrir/Dossier/Retirer). Lien
  "Tout voir" qui ouvre le panneau complet.
- `MainWindow.DownloadsIndicator.cs` (nouveau) : logique de visibilité/badge
  (`NotifyDownloadStarted`, `RefreshDownloadsIndicator`).
- `CoreWebView2_DownloadStarting` (`MainWindow.History.cs`) : appelle
  `NotifyDownloadStarted()` en plus du câblage existant.
- `DownloadsMenu_Click` : remet aussi le badge à zéro (ouvrir le panneau
  complet compte comme "vu").

## Fichiers touchés

- `Lumora.WinUI/MainWindow.xaml`
- `Lumora.WinUI/MainWindow.DownloadsIndicator.cs` (nouveau)
- `Lumora.WinUI/MainWindow.History.cs`
- Fichiers de version → `0.84.0.8-dev`

## Vérification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 635/635 tests
  verts (pas de nouveau cas dédié - logique de câblage UI pure, dans la même
  veine que les autres icônes de modules non testées unitairement).
- Build MSBuild Debug : réussi, 0 erreur.
- Build propre Release + installateur (`0.84.0.8-dev`) : réussis.
- Vérification live (profil jetable, mode invité, pilotage UIA) : vrai
  téléchargement forcé via un serveur HTTP local. Capture 1 : icône visible
  dans la barre d'outils avec badge "1" pendant le téléchargement, message de
  statut "Téléchargement démarré". Capture 2 : clic sur l'icône → flyout
  affichant le fichier avec état "Terminé" et les boutons Ouvrir/Dossier/
  Retirer, badge disparu après ouverture.

## Limite

Le contenu du flyout est une photo prise à l'ouverture (comme le panneau
complet) : s'il reste ouvert pendant qu'un autre téléchargement progresse, il
ne se met pas à jour en direct - il faut le rouvrir. Comportement jugé
acceptable pour cette itération, identique à la limite déjà existante du
panneau complet.

**Version :** `0.84.0.8-dev`.
