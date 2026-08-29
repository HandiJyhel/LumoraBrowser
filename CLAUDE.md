# Instructions pour Claude Code — Lumora

## Les règles suprêmes que Claude doit respecter quoi qu'il arrive

1.  attendre un `Go` explicite avant toute action importante, implementation, installation ou modification structurante
2. Comme Claude est tellement con, il doit rien faire sans mon accord et surtout faire la distinction quand je lui demande de faire quelque chose ou quand je lui demande un simple avis. 
3. À partir de maintenant, Claude ne doit écrire que dans les fichiers du dépôt, surtout pour le fichier mémoire. 
4. Après toute mise à jour d'une partie du code, Claude doit s'assurer que l'application reste complètement fonctionnelle (build + tests, et vérification réelle de la fonction touchée si c'est raisonnablement possible) avant de considérer la tâche terminée.
5. Il est rare que l'utilisateur propose quelque chose de complètement idiot. S'il sent lui-même qu'une idée qu'il avance est bancale, il le dira et demandera l'avis de Claude à ce moment-là. En dehors de ce cas, une demande de l'utilisateur n'est jamais faite pour rien : Claude doit la prendre au sérieux telle quelle, sans la remettre en doute par défaut.
6. Le fichier mémoire du dépôt nommé `MEMORY.md` doit être constamment mis à jour à chaque passe ou chaque modification du logiciel.
7. Quand une phrase se termine par un "?", ça veut dire que c'est une question, donc j'attends une réponse de ta part avant que tu fasses quoi que ce soit


Quand l'utilisateur commence une session ou un message par "salut" (ou une salutation equivalente en debut de conversation), lis integralement `CLAUDE.md` et `MEMORY.md` a la racine du depot AVANT toute autre action, meme si la demande qui suit semble simple ou urgente.


## Politique de versionnement (mise a jour le 2026-07-27, remplace la regle du 2026-07-22)

Palier courant : `0.94.0.0-dev` (chantier "Control panel and start menu" - refonte du panneau Reglages et du menu Demarrer, demarre le 2026-08-23, palier precedent `0.93.x` = accessibilite handicap). Le premier (`0`) et le deuxieme (`94`) chiffre ne bougent que sur un changement de palier explicitement demande par l'utilisateur (nouvelle fonctionnalite globale majeure ou avancee importante) — ne jamais les monter de sa propre initiative.

A l'interieur d'un palier :
- Ajout de quelque chose (fonctionnalite, module, nouvelle aide, etc.) → seul le **troisieme chiffre** bouge : `0.93.0.0-dev` → `0.93.1.0-dev` → `0.93.2.0-dev`, etc.
- Micro-correction (correctif, rectification d'une version deja livree) → seul le **quatrieme chiffre** bouge, a la suite du troisieme chiffre courant : `0.93.0.1-dev`, `0.93.0.2-dev`, etc.

Cette regle remplace celle du 2026-07-22 (ou seul le quatrieme chiffre bougeait pour tout). En cas de doute sur la nature d'un changement (ajout vs micro-correction) ou son ampleur, ne pas trancher seul : demander a l'utilisateur.


## Langage (interaction avec l'utilisateur)

Claude doit parler Français (fr)
