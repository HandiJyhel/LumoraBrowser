# 2026-07-09 — Lecteur vidéo flottant (Picture-in-Picture) 0.48.0-dev

- Troisième demande de la session : un « lecteur vidéo flottant » évoqué en aparté par l'utilisateur, sans trace dans MEMORY.md/docs/logs — recherché explicitement avant implémentation, aucune proposition antérieure retrouvée. Traité comme du Picture-in-Picture standard (interprétation la plus probable, confirmée à l'utilisateur dans la réponse précédente).
- `MainWindow.PictureInPicture.cs` (nouveau) : action à la demande uniquement, aucun script en tâche de fond. Script JS asynchrone exécuté via `CoreWebView2.ExecuteScriptAsync`, cible la vidéo en lecture ou la plus grande vidéo du document, appelle `requestPictureInPicture()` en attendant la promesse — un rejet du moteur remonte tel quel (pas de faux succès).
- UI : bouton dédié dans la barre de navigation (`DetachVideoButton`, nouvelle 12e colonne du `NavigationToolbar`), entrée dans les deux menus Pulse existants, entrée dans la palette `Ctrl+K`.
- Limite documentée : vidéo dans un iframe cross-origin inaccessible au script ; exigence de geste utilisateur côté page potentiellement non satisfaite selon la propagation d'activation de WebView2 — non vérifié interactivement dans cette session (pas d'outillage d'automatisation UI disponible), à confirmer par l'utilisateur sur une page vidéo réelle.
- Aucune nouvelle classe pure : logique entièrement côté script JS à la demande, dans la continuité des moniteurs identifiants/paiement/passkeys déjà présents (non unitairement testables).
- Vérification : `dotnet test` 94/94 verts (inchangé, pas de nouvelle logique pure). `build-winui.cmd` 0 avertissement, 0 erreur. Lancement court : fenêtre `Pulse Browser 0.48.0-dev` répondante.

**Version :** `0.48.0-dev`.
