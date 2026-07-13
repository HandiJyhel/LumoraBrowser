# 2026-07-13 - Dictee : arret instantane, gain auto, filtre anti-charabia (0.65.7-dev)

## Contexte

Retour utilisateur apres `0.65.6-dev` (dictee continue) :

1. Le micro reste bien allume, mais impossible d'arreter la dictee : le
   programme semble presque planter.
2. A distance egale, un autre outil de dictee retranscrit correctement, mais
   Lumora "ecrit n'importe quoi".

## Causes

1. **Arret gele** : `RecognizeAsyncStop()` etait appele sur le thread UI. Il
   bloque jusqu'a la finalisation de la phrase en cours (mesure : >4 s), ce
   qui figeait toute la fenetre — et le minuteur de secours (DispatcherQueue,
   meme thread) ne pouvait pas prendre le relais. Impression de plantage.
2. **Charabia** : deux facteurs cumules, mesures sur la machine :
   - la capture WaveIn brute n'a aucun controle de gain, contrairement au
     pipeline SAPI par defaut calibre par Windows : pics mesures a ~5-13% de
     la pleine echelle sur le micro webcam ;
   - a bas niveau, le vieux moteur SAPI "hallucine" des phrases entieres
     (charabia a 14-27% de confiance sur son ambiant) et l'app inserait tout
     sans filtre.

## Corrige / ajoute

- **Arret non bloquant** (`StopDictation`) : la source audio est coupee en
  premier (StopRecording + fin de flux) — le moteur se termine alors de
  lui-meme — et `RecognizeAsyncStop()` part sur un thread d'arriere-plan.
  Le thread UI n'attend plus jamais le moteur. Mesure : arret confirme en
  0,27 s (contre >4 s bloquants avant).
- **`DictationAutoGain`** (nouveau, teste) : gain automatique doux sur la
  capture du micro explicite — enveloppe de crete, cible ~30% de la pleine
  echelle, plafond x8, montee lissee, silence jamais amplifie, ecretage
  propre sans enroulement.
- **Filtre de confiance** : une phrase reconnue sous 30% de confiance n'est
  plus inseree ; message "je n'ai pas bien compris, repetez ou
  rapprochez-vous du micro" a la place du charabia. Verifie en reel : le
  bruit ambiant (17-27%) est filtre.
- **Culture du moteur robuste** : moteur choisi parmi les recognizers SAPI
  installes (culture UI exacte, sinon meme langue, sinon premier installe) +
  trace de la culture retenue.
- Tests : +4 (`DictationAutoGainTests`), total 251/251 verts.
- Version passee a `0.65.7-dev`.

## Verification

- Test reel machine (micro MX Brio, pipeline identique au code produit) :
  gain auto x2,3 sur ambiant, phrases ambiantes filtrees (17-27% < seuil),
  arret confirme en 0,27 s, `RecognizeCompleted` recu proprement.
- `cmd /c build-winui.cmd` : reussi, 0 avertissement, 0 erreur.
- `dotnet test` : 251/251 tests verts.
- Artifact propre :
  `artifacts\clean-test\Lumora-0.65.7-dev-win-x64-clean-20260713-025158`.
- Installateur :
  `artifacts\installer\LumoraSetup-0.65.7-dev-win-x64.exe`.
- SHA256 installateur :
  `49f901019f111bb94607fc38e503abe3345d2884f468dabef14d96cf907e4bc7`.
- Installation locale remplacee (app non lancee au moment de la copie).
- Entree uninstall HKCU : `DisplayVersion = 0.65.7-dev`.

## Non fait / a savoir

- Le moteur SAPI reste un moteur des annees 2000 : meme bien alimente, il ne
  rivalisera pas avec les recognizers neuronaux modernes (Win+H, telephones).
  Si la qualite reste insuffisante apres ce correctif, le vrai chantier est
  un moteur local moderne (ex. Whisper via ONNX Runtime, deja present dans
  les dependances pour SearchAssist).
- L'entrainement du profil vocal Windows ("Reconnaissance vocale" > ameliorer
  la precision) peut aussi ameliorer sensiblement SAPI.
- Seuil de confiance (0,30) et delai d'inactivite (20 s) : constantes simples
  a ajuster selon retour utilisateur.
