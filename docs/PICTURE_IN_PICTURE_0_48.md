# Lecteur vidéo flottant — 0.48.0-dev

## Objectif

Détacher la vidéo en cours de lecture dans une petite fenêtre système toujours
au premier plan (Picture-in-Picture natif de Chromium), pour continuer à
regarder pendant qu'on navigue ailleurs dans Pulse Browser ou dans une autre
application. Fonctionnalité évoquée par l'utilisateur en aparté (« lecteur
vidéo ») ; recherche dans MEMORY.md/docs/logs : aucune trace d'une proposition
antérieure précise — traitée ici comme du Picture-in-Picture standard, la
lecture la plus courante de cette demande.

## Fonctionnement

- Action à la demande uniquement : **aucun script ne tourne en tâche de fond**
  sur les pages visitées. `MainWindow.PictureInPicture.cs` exécute un script
  ponctuel via `CoreWebView2.ExecuteScriptAsync` seulement quand l'utilisateur
  déclenche l'action.
- Accès : bouton dédié dans la barre de navigation, menu Pulse (« Détacher la
  vidéo (Picture-in-Picture) »), palette `Ctrl+K`.
- Le script cible la vidéo « active » : celle en lecture en priorité, sinon la
  plus grande vidéo présente dans le document.
- Script asynchrone : `WebView2` attend la promesse retournée par
  `requestPictureInPicture()`. En cas de rejet par le moteur (aucune vidéo,
  API non disponible, refus du navigateur), le message d'erreur réel de
  Chromium remonte tel quel dans la barre de statut plutôt que d'afficher un
  faux succès.

## Limites connues

- **Document principal uniquement** : une vidéo dans un iframe cross-origin
  (lecteur intégré tiers) reste inaccessible au script — limite du même ordre
  que les iframes de paiement du portefeuille.
- **Geste utilisateur** : `requestPictureInPicture()` exige normalement une
  activation utilisateur côté page. Un clic sur le bouton Pulse est un geste
  utilisateur côté application WinUI, pas nécessairement reconnu comme tel par
  la page web elle-même selon la manière dont WebView2 propage l'activation
  au script injecté. Si le moteur refuse pour cette raison, le message
  d'erreur exact de Chromium s'affiche (pas de faux positif) — **non vérifié
  interactivement dans cette session** faute d'outillage d'automatisation UI ;
  à confirmer par l'utilisateur sur une vraie page vidéo (YouTube ou autre).

## Tests

Aucune nouvelle classe pure : la logique est entièrement côté script JS
exécuté à la demande dans la page, comme les moniteurs identifiants/paiement/
passkeys déjà présents (non unitairement testables, vérification manuelle).
