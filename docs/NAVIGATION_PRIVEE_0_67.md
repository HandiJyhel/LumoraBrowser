# Lumora 0.67.0-dev - Fenetre de navigation privee

## Objectif

Offrir une session de navigation ephemere, attendue de tout navigateur oriente vie privee : rien ne doit rester sur la machine ni dans le profil Lumora apres la fermeture.

## Ce qui change

- Nouvelle fenetre `LumoraPrivateWindow`, ouverte depuis le menu `Naviguer > Nouvelle fenetre privee`, la palette de commandes ou `Ctrl+Shift+N`.
- Le moteur tourne sur un profil WebView2 InPrivate (`IsInPrivateModeEnabled`) : cookies, cache et stockages restent en memoire et sont purges par le moteur a la liberation du profil.
- Par construction, rien ne touche l'historique, le coffre, les favoris ou les favicons de Lumora : la fenetre n'est reliee a aucun store du profil.
- Les protections reseau restent actives (bloqueur pubs/trackers, anti-telemetrie, HTTPS, nettoyage de parametres, CNAME), avec la meme liste blanche que la fenetre principale.
- L'enregistrement de mots de passe Chromium et l'autofill natif restent coupes, comme partout dans Lumora.
- Une page cible (`target=_blank`, `window.open`) ouvre une nouvelle fenetre privee : la navigation ne s'echappe jamais vers le profil normal.
- La barre d'adresse partage la logique de normalisation de la fenetre principale, extraite dans `AddressNormalizer` (classe pure testee).

## Limites connues

- Pas d'onglets dans la fenetre privee (v1) : chaque nouvelle page cible ouvre une fenetre.
- Comme pour les fenetres d'application web, le filtre cosmetique et le refus automatique des bannieres cookies restent reserves a la fenetre principale.
- Les fichiers telecharges volontairement restent, eux, sur le disque (comportement standard des navigations privees).
