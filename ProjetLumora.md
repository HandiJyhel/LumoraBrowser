# &#x09;				**Projet Lumora**







## Avant propos











Salut, ici, on va créer un navigateur web qui va pas révolutionner le monde et qui sera sécurisé pour l'utilisateur et pratique.



Tu vas commencer par créer un fichier que tu as nommé AGENTS.md, ce fichier va contenir la structure du projet ainsi que toutes les règles que tu devras respecter et le contexte. 

Tu vas créer un dossier longue, tout simplement, qui va lui te permettre de stocker ? Bah les différentes erreurs ou non qui se passent lors du projet, des logs quoi.

Tu dois créer aussi un fichier mémoire que tu vas appeler MEMORY.md et ce fichier va contenir toutes les étapes qui ont été effectuées pour le projet. Tu vas classer les étapes du plus ancien au plus récent 

MEMORY.md N'est pas une redite du fichier AGENTS.md, il est uniquement là pour la mémoire du projet.



Ce projet a pour but d'être simple et sécurisé, je le répète et aucune des données ne devra être envoyée sur un serveur quelconque, Tout sera stocké sur l'ordinateur de l'utilisateur, peut-être dans des fichiers spéciaux que seul le navigateur pourra décréter, mais Je le dis encore une fois, ici Aucune donnée ne devra être divulguée sur Internet, donc faudra trouver à un moyen.







## **Contexte**





Le but de ce projet est de créer un navigateur internet sécurisé, mais simple d'accès. Eh bien évidemment, avec une interface moderne. Pour donner un exemple, j'ai testé des navigateurs web sécurisés avec une sécurité renforcée donc qui ne prenait pas en compte les cookies qui demandaient toujours de se ré identifier, ce qui peut être très redondant pour l'utilisateur et décourager. Donc moi justement je veux créer un navigateur avec une couche de sécurité relativement forte mais avec une simplicité d'utilisation.



J'aimerais coder le navigateur en RUST, j'aimerais lui donner une interface moderne basée sous WINUI 3.



&#x20;Le Moteur web utilisé sera Gecko, ATTENTION  Je sais que c'est le moteur utilisé par Firefox, mais graphiquement et même au niveau des fonctionnalités et de la logique. Je ne veux pas une copie de Firefox. Je veux juste utiliser son moteur pour sa stabilité. Et parce que je sais qu'il est open source Je pense que là tu peux.







### **Numérotation**





En ce qui concerne la numérotation des versions, on va travailler sur une version que je vais appeler de développement, donc que tu commenceras à 0.0.0-dev Tu feras l'incrémentation de cette façon :



* Le premier 0 sera pour les mises à jour majeure (Pour tout ce qui est une avancée globale de la version, c'est à dire grosse fonctionnalité, des trucs comme ça);
* Le 2e 0 pour les mises à jour intermédiaires, Ajout de fonctionnalités globalement pour ça que ce numéro doit bouger Ou un regroupement de petites choses qui commencent à devenir une grosse chose)
* Et le dernier pour les mises à jour mineurs (Correction de Bug).











## **Règle que tu dois respecter**







1. Tu dois attendre Toute validation de ma part par un Go,
2. Je te donne la possibilité d'installer tout ce que tu as besoin,
3. Tu as le droit de me contredire,
4. Tu as le droit de me donner tes propres idées,
5. Tu peux me parler avec un langage familier,
6. Tu dois t'arranger pour que le code reste toujours propre et lisible.
7. A chaque fois que je te donne quelque chose, je veux que tu le recontextualise,
8. Tu as le droit de me poser des questions,
9. Attention aux fautes d'orthographe,
10. Si je te demande quelque chose et que tu vois que tu peux pas le réaliser, je veux que tu me le dises tout de suite.
11. Garde en tête que je sais que t'es une machine, donc tu as le droit de prendre des initiatives mais pas trop non plus
12. Je vois quand tu fais des bêtises, donc pas la peine d'essayer de me cacher.
13. Tu dois respecter scrupuleusement la numérotation mise en place pour le développement
14. Pour la release, on verra plus tard, tu t'en occupes 
15. MEMORY.md Doit être scrupuleusement mis à jour sans faute.






