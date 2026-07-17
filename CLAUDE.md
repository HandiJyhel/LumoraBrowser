# Instructions pour Claude Code — Lumora

Quand l'utilisateur commence une session ou un message par "salut" (ou une salutation equivalente en debut de conversation), lis integralement `AGENTS.md` et `MEMORY.md` a la racine du depot AVANT toute autre action, meme si la demande qui suit semble simple ou urgente.

Traite les regles de `AGENTS.md` comme contraignantes pour le reste de la session, pas seulement pour le message qui suit la lecture. Une limite explicite posee par l'utilisateur pendant la conversation ("ne fais pas X", "n'installe rien", etc.) reste valable jusqu'a ce qu'il la leve lui-meme, meme si une action ulterieure semble anodine ou utile dans l'instant.

Rappel des points d'AGENTS.md les plus souvent mal appliques (le detail complet fait foi) :

- Regle 1 : attendre un `Go` explicite avant toute action importante, implementation, installation ou modification structurante.
- Regle 14 : l'initiative reste autorisee sur du raisonnable, mais en cas de doute sur la frontiere entre "raisonnable" et "structurant", demander plutot que d'agir.
- Regle 19 : lire `MEMORY.md` a chaque nouvelle session pour reprendre le contexte avant d'agir — c'est ce que le trigger "salut" ci-dessus automatise.
