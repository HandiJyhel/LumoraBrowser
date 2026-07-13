# 2026-07-13 - Dictee : retrait du moteur SAPI, bouton micro devenu astuce Win+H (0.66.0-dev)

## Contexte

Retour utilisateur apres `0.65.7-dev` : le micro choisi est bien detecte,
mais la retranscription reste du n'importe quoi malgre le gain automatique
et le filtre de confiance. Test comparatif sur la meme machine : la dictee
moderne de Windows (Win+H) fonctionne parfaitement, y compris dans les
champs de Lumora.

## Decision

Le moteur SAPI (`System.Speech.Recognition`) date de l'ere Vista/7 et sa
reconnaissance du francais est structurellement mediocre : ce n'est pas un
bug de Lumora (trois versions de correctifs 0.65.5 -> 0.65.7 l'ont confirme),
c'est le moteur lui-meme. Plutot que de s'acharner (ou d'embarquer un modele
Whisper/ONNX, piste lourde), Lumora s'appuie sur la dictee Windows native :

- **Le bouton micro reste dans la barre d'adresse** (toujours active par le
  reglage d'accessibilite "Dictee vocale").
- **Au survol**, l'infobulle explique le geste : cliquer dans un champ de
  texte puis appuyer sur Win+H.
- **Au clic**, la meme astuce s'affiche dans la barre de statut.
- Pas d'appel programmatique de la dictee Windows : il n'existe pas d'API
  publique, seule une simulation clavier Win+H serait possible (fragile,
  dependante des reglages systeme). Choix assume : une simple astuce.

## Supprime

- `MainWindow.Dictation.cs` reecrit : ~590 lignes -> ~30 (astuce seule).
- `DictationAudioInput.cs`, `DictationAudioStream.cs`, `DictationAutoGain.cs`.
- `Dictation/DictationFillScript.js`, `Dictation/DictationRememberTargetScript.js`.
- `tools/DictationDiagnostic/` (outil de diagnostic SAPI complet).
- Tests `DictationAudioStreamTests.cs` (6) et `DictationAutoGainTests.cs` (4).
- Dependances NuGet `System.Speech` et `NAudio.WinMM` (plus aucun usage).
- Reglage `DictationMicrophoneDeviceId` + selecteur de micro des Parametres
  (combo + bouton Actualiser) : sans moteur integre, plus rien a choisir.
  La cle obsolete dans les `ui-settings` existants est simplement ignoree a
  la deserialisation.

## Consequence confidentialite

La description du reglage ne promet plus "reconnaissance 100% locale" : la
voix est traitee par la dictee Windows selon les parametres systeme de
l'utilisateur (potentiellement en ligne selon sa configuration). Lumora
n'ouvre plus jamais le micro lui-meme — c'est desormais explicite dans le
panneau Parametres.

## Verification

- Build MSBuild Debug x64 : OK, 0 erreur, 0 avertissement.
- Tests : 241/241 reussis (251 - 10 tests dictee supprimes).

## Version

`0.66.0-dev`
