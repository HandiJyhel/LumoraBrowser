# Pulse Browser - Instructions de projet

## Contexte

Pulse Browser est un navigateur web en developpement dont l'objectif est de proposer une navigation simple, moderne et securisee, sans transformer la securite en contrainte permanente pour l'utilisateur.

Le navigateur doit rester different de Firefox, Chrome, Edge ou Brave dans son experience, son interface et sa logique produit. Le moteur web sert de base technique stable, mais Pulse Browser doit garder sa propre identite.

## Objectif produit

- Creer un navigateur web simple d'acces, moderne et securise.
- Garder les donnees de l'utilisateur sur son ordinateur.
- Eviter toute divulgation de donnees vers un serveur externe.
- Trouver des mecanismes locaux pour stocker les donnees sensibles de maniere protegee.
- Conserver une experience utilisateur pratique, notamment pour les cookies, sessions et connexions, sans forcer l'utilisateur a se reconnecter inutilement.

## Stack cible

- Langage principal souhaite: Rust.
- Interface cible: WinUI 3 pour une experience Windows moderne.
- Moteur web cible: Chromium via CEF (Chromium Embedded Framework).

Le moteur initialement envisage etait Gecko. La decision projet actuelle remplace Gecko par Chromium via CEF, car CEF est plus adapte a un navigateur desktop personnalisable, solide et embarquable.

## Principes de securite

- Aucune donnee personnelle ne doit etre envoyee sur un serveur Pulse Browser.
- Les donnees utilisateur doivent etre stockees localement.
- Les fichiers contenant des informations sensibles devront etre proteges, idealement chiffres, et lisibles uniquement par Pulse Browser ou par un mecanisme local controle.
- Les logs ne doivent jamais contenir de mots de passe, tokens, cookies de session, cles secretes, donnees bancaires ou informations personnelles inutiles.
- Toute fonctionnalite de synchronisation, telemetrie ou service distant est exclue tant qu'elle n'a pas ete explicitement discutee et validee.

## Versionnement

La version de depart du projet est:

`0.0.0-dev`

La version courante du projet est:

`0.45.0-dev`

La numerotation suit ce schema:

- Premier nombre: mise a jour majeure ou avancee globale importante.
- Deuxieme nombre: mise a jour intermediaire ou ajout de fonctionnalites globales.
- Troisieme nombre: mise a jour mineure ou correction de bug.
- Suffixe `-dev`: version de developpement.

Toute modification de version doit respecter cette regle.

## Fichiers de gouvernance

- `AGENTS.md` contient le contexte permanent du projet, les choix techniques, les regles et les contraintes.
- `MEMORY.md` contient uniquement l'historique chronologique des etapes effectuees.
- `MEMORY.md` ne doit pas etre une redite de `AGENTS.md`.
- Le dossier `logs/` sert a conserver les traces techniques utiles au projet.

## Regles de collaboration

1. Attendre une validation explicite par `Go` avant toute action importante, implementation, installation ou modification structurante.
2. L'utilisateur autorise l'installation des outils necessaires, mais les installations doivent rester justifiees.
3. Codex peut creer tous les fichiers, dossiers, scripts, modules ou documents necessaires au projet, en nombre suffisant, si cela sert une architecture propre et lisible.
4. Codex peut utiliser ou ajouter une technologie, dependance, outil ou runtime particulier sans redemander une autorisation projet a chaque fois, si ce choix est utile, justifie et coherent avec Pulse Browser.
5. Les demandes d'autorisation imposees par l'environnement d'execution, le systeme ou le bac a sable restent possibles meme si l'autorisation projet est generale.
6. Il est autorise de contredire l'utilisateur si un choix technique semble fragile ou risque.
7. Il est autorise de proposer des idees, tant qu'elles restent au service du cadre donne.
8. Le ton peut etre familier, direct et naturel.
9. Le code doit rester propre, lisible et maintenable.
10. Toute demande de l'utilisateur doit etre recontextualisee avant action.
11. Il est autorise de poser des questions quand elles aident vraiment le projet.
12. Faire attention aux fautes d'orthographe dans les fichiers projet et la documentation.
13. Si une demande n'est pas realisable, le dire clairement tout de suite.
14. Prendre des initiatives raisonnables, sans depasser le cadre donne.
15. Ne pas masquer les erreurs, blocages ou approximations.
16. Respecter scrupuleusement la numerotation de developpement.
17. Les decisions de release seront traitees plus tard.
18. `MEMORY.md` doit etre mis a jour soigneusement apres chaque etape significative.
19. A chaque nouvelle ouverture de session ou de chat sur Pulse Browser, Codex doit lire `MEMORY.md` pour reprendre le contexte historique du projet avant d'agir.

## Direction technique actuelle

La premiere phase a consolide les fondations techniques dans une coque Win32 provisoire: architecture, prototype minimal, stockage local, isolation des profils, politique de cookies, gestion des permissions, favoris et premiers onglets.

La direction produit courante est maintenant la migration vers une vraie coque WinUI 3. La coque Win32 est un prototype historique archive, utile uniquement comme reference technique pour CEF et le coeur local. Elle ne doit plus recevoir l'identite graphique finale, ni les fonctions produit, ni les corrections d'experience utilisateur.

La seule interface produit active est `PulseBrowser.WinUI`. Toute nouvelle fonction visible, tout changement de favoris, d'onglets, de menus, de parametres, d'import/export, d'accueil ou d'experience navigateur doit etre implemente dans `PulseBrowser.WinUI`, sauf demande explicite contraire de l'utilisateur.

Les lanceurs ambigus du prototype Win32 sont desactives. `run-winui.cmd` est le chemin de lancement normal du projet. Le script legacy sous `archive/win32-cef-prototype/` sert uniquement a un diagnostic technique volontaire et ne doit pas etre utilise pour valider une fonction produit.

Le coeur local Rust doit rester responsable des donnees, profils, favoris, historique, coffre, regles de confidentialite et integration moteur. La coque WinUI 3 doit porter l'experience utilisateur, l'identite visuelle et les surfaces produit modernes.

La migration WinUI 3 ne doit pas provoquer de regression fonctionnelle visible: les onglets, favoris, menus, import/export de favoris, parametres et page A propos deja acquis dans le prototype doivent etre repris dans la nouvelle interface, puis ameliores progressivement.

La version WinUI peut utiliser WebView2 comme pont temporaire pour restaurer la navigation Internet dans l'interface moderne. Ce pont ne remplace pas la cible moteur du projet: le moteur final vise reste Chromium via CEF raccorde au coeur Rust local.

Pulse Browser doit avancer par petites versions coherentes plutot que par grosses promesses fragiles.
