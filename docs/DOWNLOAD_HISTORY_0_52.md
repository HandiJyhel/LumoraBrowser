# Pulse Browser 0.52.0-dev - Historique local des telechargements

## Objectif

Transformer le panneau `Telechargements` en outil utilisable au quotidien, pas seulement en liste de session.

## Ce qui change

- Les telechargements sont conserves dans `navigation/downloads.pulse`, par profil.
- Le fichier passe par `PulseFile`, donc il reste dans le stockage local protege de Pulse Browser.
- Le panneau affiche l'historique local au redemarrage.
- L'utilisateur peut retirer une entree precise ou effacer tout l'historique des telechargements.
- Les boutons `Ouvrir` et `Dossier` ne sont proposes que si le fichier existe encore.
- Le mode invite vide la liste en memoire et n'ecrit pas de nouvel historique de telechargements.

## Limites connues

- Cette etape ne remplace pas encore la boite native de choix de destination de WebView2.
- Les telechargements reels doivent encore etre testes manuellement sur plusieurs sites pour valider les variations d'etat WebView2.
