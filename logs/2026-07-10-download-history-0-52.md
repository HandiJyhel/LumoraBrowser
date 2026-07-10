# 2026-07-10 - Telechargements persistants 0.52.0-dev

## Changements

- Ajout de `Models/DownloadHistory.cs` avec `DownloadHistoryEntry` et `DownloadHistoryStore`.
- Ajout de `PulseProfilePaths.DownloadsFile` vers `navigation/downloads.pulse`.
- `HistoryPanelController` porte maintenant un store de telechargements persistant.
- `CoreWebView2.DownloadStarting` enregistre le telechargement au demarrage puis met a jour l'entree sur progression et changement d'etat.
- Le panneau `Telechargements` affiche l'historique local, permet de retirer une entree et d'effacer la liste.
- Les actions `Ouvrir` et `Dossier` sont masquees quand le fichier termine n'existe plus.
- Le mode invite desactive la persistance des nouveaux telechargements.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 149/149 tests verts.
- Premier `build-winui.cmd` bloque par le sandbox reseau NuGet (`NU1301`), puis relance autorisee reussie avec 0 avertissement et 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` reussi : fenetre `Pulse Browser 0.52.0-dev`, processus repondant, fermeture propre du processus de test.
