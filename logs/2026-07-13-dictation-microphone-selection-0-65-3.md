# 2026-07-13 - Selection du microphone de dictee (0.65.3-dev)

## Contexte

Retour utilisateur : la dictee vocale Lumora fonctionnait, mais utilisait le
mauvais micro, en pratique le micro de la manette PlayStation 5. Lumora ne
proposait aucun choix de microphone.

## Cause

La dictee utilisait `SpeechRecognitionEngine.SetInputToDefaultAudioDevice()`.
Le moteur Windows classique prenait donc uniquement le peripherique d'entree
par defaut expose a SAPI/Windows, sans preference propre a Lumora.

## Corrige / ajoute

- Ajout d'une preference locale `DictationMicrophoneDeviceId` dans
  `UiSettings`.
- Ajout d'une liste de microphones dans Parametres > Accessibilite :
  - `Micro par defaut Windows`,
  - microphones WaveIn detectes,
  - bouton `Actualiser`.
- Ajout de `DictationAudioInput` pour enumerer les micros via
  `NAudio.WinMM`.
- Quand un micro precis est choisi, Lumora capture ce micro en PCM mono
  16 kHz et alimente `SpeechRecognitionEngine` via `SetInputToAudioStream`.
- Le choix par defaut Windows reste disponible pour conserver le comportement
  precedent.
- La barre de statut indique le micro selectionne au moment de la dictee.
- Robustesse ajoutee si l'enumeration ou l'ouverture d'un micro echoue.
- Version passee a `0.65.3-dev`.

## Verification

- `dotnet restore Lumora.WinUI\Lumora.WinUI.csproj` : reussi.
- `cmd /c build-winui.cmd` : reussi, 0 avertissement, 0 erreur.
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` :
  241/241 tests verts.
- `scripts\build-clean-test-artifact.ps1 -Version 0.65.3-dev -NoRestore` :
  reussi.
- Artifact propre final :
  `artifacts\clean-test\Lumora-0.65.3-dev-win-x64-clean-20260713-001232`.
- Verification artifact / installation locale :
  `Lumora.WinUI.exe`, `App.xbf`, `MainWindow.xbf`, `LumoraAppWindow.xbf`,
  `Lumora.WinUI.pri`, `NAudio.Core.dll`, `NAudio.WinMM.dll` et
  `System.Speech.dll` presents.
- `NAudio.dll` absent de l'installation finale : seuls les modules necessaires
  sont embarques.
- Installateur final :
  `artifacts\installer\LumoraSetup-0.65.3-dev-win-x64.exe`.
- SHA256 installateur :
  `51901f27b23a1e1cecb0717b4f49652e214c8ba2bc3015db2314db3e1a14602e`.
- Installation locale `%LOCALAPPDATA%\Programs\Lumora` remplacee par
  l'artefact final `0.65.3-dev`.
- Entree uninstall HKCU verifiee : `DisplayVersion = 0.65.3-dev`.

## Non fait / a savoir

Pas de validation vocale interactive avec le vrai micro utilisateur dans cette
passe. Le correctif a ete valide par build, tests, packaging, installation
locale et verification des dependances audio embarquees.

