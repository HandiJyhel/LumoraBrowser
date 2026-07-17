# Navigation depuis les recherches et anti-parasite - 0.78.3.3-dev

## Probleme

Retour utilisateur : la recherche dans la barre d'adresse fonctionnait, mais
cliquer un resultat de recherche bloquait la navigation avec une erreur de
connexion. Le probleme n'etait pas specifique a Wikipedia/Tintin : tous les
clics vers des resultats pouvaient etre traites comme des redirections
parasites.

Cause : le durcissement anti-parasite de `0.78.3.1-dev` bloquait les
navigations cross-domaine depuis une page sous pression publicitaire. C'etait
utile contre les detournements automatiques, mais trop large pour un navigateur :
un clic utilisateur normal depuis un moteur de recherche est justement une
navigation cross-domaine legitime.

## Correction

- `NavigationHijackPolicy.Decide` recoit maintenant le signal
  `isUserInitiated`.
- Les domaines publicitaires connus restent bloques, meme sur clic utilisateur.
- Les tab-under apres popup recente restent bloques.
- Les clics utilisateur normaux vers un autre site sont autorises, meme si la
  page source a des requetes publicitaires bloquees.
- `NavigationHealthTracker` conserve le geste utilisateur sur toute la chaine de
  redirection du document principal. Cas vise : `google.com/url` puis redirection
  technique vers le resultat final.
- Le nettoyage d'URL (`ParameterCleaner` / `HttpsEnforcer`) conserve aussi cette
  legitimite sur l'URL nettoyee.

## Fichiers touches

- `Lumora.WinUI/NavigationHijackPolicy.cs`
- `Lumora.WinUI/NavigationHealthTracker.cs`
- `Lumora.WinUI/MainWindow.AdShield.cs`
- `Lumora.WinUI/MainWindow.Navigation.cs`
- `Lumora.Tests/NavigationHijackPolicyTests.cs`
- `Lumora.Tests/NavigationHealthTrackerTests.cs`
- `Lumora.WinUI/MainWindow.xaml.cs`
- `AGENTS.md`

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj` : 441/441 tests verts.
- `build-winui.cmd` : build Debug WinUI reussi, 0 erreur, 0 avertissement.
- `scripts\build-clean-test-artifact.ps1 -Version 0.78.3.3-dev` : publish
  Release autonome reussi, 0 erreur, 0 avertissement.
- Artefact propre :
  `artifacts\clean-test\Lumora-0.78.3.3-dev-win-x64-clean-20260715-222325`
- Manifeste SHA256 :
  `artifacts\signatures\Lumora-0.78.3.3-dev-clean-20260715-222353.sha256`
- Installeur :
  `artifacts\installer\LumoraSetup-0.78.3.3-dev-win-x64.exe`
- SHA256 installeur :
  `a2d19aa363540ec83c6e8a37053675ee7c3a08fdc31f33c2f6372344cb07dcf3`
- Manifeste SHA256 installeur :
  `artifacts\signatures\LumoraSetup-0.78.3.3-dev-20260715-222437.sha256`

## Limite

La verification live Google -> Wikipedia n'a pas ete pilotee automatiquement dans
cette passe. Le comportement est verrouille par les tests purs et par la
compilation WinUI avec le signal WebView2 `IsUserInitiated`.

**Version :** `0.78.3.3-dev`.
