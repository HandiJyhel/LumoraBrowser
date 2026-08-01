# Instructions pour Claude Code — Lumora

## Les règles suprêmes que Claude doit respecter quoi qu'il arrive

1. Comme Claude est tellement con, il doit rien faire sans mon accord et surtout faire la distinction quand je lui demande de faire quelque chose ou quand je lui demande un simple avis. 
2. À partir de maintenant, Claude ne doit écrire que dans les fichiers du dépôt, surtout pour le fichier mémoire. 
3. Après toute mise à jour d'une partie du code, Claude doit s'assurer que l'application reste complètement fonctionnelle (build + tests, et vérification réelle de la fonction touchée si c'est raisonnablement possible) avant de considérer la tâche terminée.
4. Il est rare que l'utilisateur propose quelque chose de complètement idiot. S'il sent lui-même qu'une idée qu'il avance est bancale, il le dira et demandera l'avis de Claude à ce moment-là. En dehors de ce cas, une demande de l'utilisateur n'est jamais faite pour rien : Claude doit la prendre au sérieux telle quelle, sans la remettre en doute par défaut.
5. Le fichier mémoire du dépôt nommé `MEMORY.md` doit être constamment mis à jour à chaque passe ou chaque modification du logiciel.

Quand l'utilisateur commence une session ou un message par "salut" (ou une salutation equivalente en debut de conversation), lis integralement `AGENTS.md` et `MEMORY.md` a la racine du depot AVANT toute autre action, meme si la demande qui suit semble simple ou urgente.

Traite les regles de `AGENTS.md` comme contraignantes pour le reste de la session, pas seulement pour le message qui suit la lecture. Une limite explicite posee par l'utilisateur pendant la conversation ("ne fais pas X", "n'installe rien", etc.) reste valable jusqu'a ce qu'il la leve lui-meme, meme si une action ulterieure semble anodine ou utile dans l'instant.

Rappel des points d'AGENTS.md les plus souvent mal appliques (le detail complet fait foi) :

- Regle 1 : attendre un `Go` explicite avant toute action importante, implementation, installation ou modification structurante.
- Regle 14 : l'initiative reste autorisee sur du raisonnable, mais en cas de doute sur la frontiere entre "raisonnable" et "structurant", demander plutot que d'agir.
- Regle 19 : lire `MEMORY.md` a chaque nouvelle session pour reprendre le contexte avant d'agir — c'est ce que le trigger "salut" ci-dessus automatise.

## Politique de versionnement (mise a jour le 2026-07-27, remplace la regle du 2026-07-22)

Palier courant : `0.93.0.0-dev` (chantier accessibilite handicap, demarre le 2026-07-27). Le premier (`0`) et le deuxieme (`93`) chiffre ne bougent que sur un changement de palier explicitement demande par l'utilisateur (nouvelle fonctionnalite globale majeure ou avancee importante) — ne jamais les monter de sa propre initiative.

A l'interieur d'un palier :
- Ajout de quelque chose (fonctionnalite, module, nouvelle aide, etc.) → seul le **troisieme chiffre** bouge : `0.93.0.0-dev` → `0.93.1.0-dev` → `0.93.2.0-dev`, etc.
- Micro-correction (correctif, rectification d'une version deja livree) → seul le **quatrieme chiffre** bouge, a la suite du troisieme chiffre courant : `0.93.0.1-dev`, `0.93.0.2-dev`, etc.

Cette regle remplace celle du 2026-07-22 (ou seul le quatrieme chiffre bougeait pour tout). En cas de doute sur la nature d'un changement (ajout vs micro-correction) ou son ampleur, ne pas trancher seul : demander a l'utilisateur.


## Langage (interaction avec l'utilisateur)

Claude doit parler Français (fr)
