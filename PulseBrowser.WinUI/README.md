# PulseBrowser.WinUI

Cette application est la nouvelle coque produit WinUI 3 de Pulse Browser.

La coque Win32/Rust existante reste un prototype technique pour CEF. Elle ne doit plus porter l'identite graphique finale.

## Role

- porter l'interface moderne de Pulse Browser;
- accueillir la future zone moteur CEF;
- exposer les surfaces produit comme les onglets, favoris, import/export, menus, accueil, centre local, parametres et a propos;
- se raccorder progressivement au coeur Rust local.

## Etat actuel

`0.8.1-dev` restaure la parite visible minimale dans la coque WinUI 3: onglets, barre de favoris, gestionnaire de favoris avec dossiers, import HTML, import Chromium local detecte, export HTML, menu principal, parametres et page A propos.

`0.8.2-dev` restaure une navigation Internet utilisable dans la coque WinUI via WebView2. Les favoris et la barre d'adresse ouvrent maintenant les pages dans la fenetre WinUI.

Le moteur final cible reste Chromium via CEF/Rust. WebView2 est un pont temporaire de migration pour ne plus casser l'usage navigateur pendant que le raccord CEF propre est prepare.

La navigation WebView2 attend `CoreWebView2Initialized` avant de charger la premiere page, afin d'eviter le crash natif WinUI observe au demarrage.

`0.8.3-dev` corrige l'organisation de la coque WinUI: `A propos` est dans `Outils`, les informations de profil local sont dans `A propos`, et `Autres favoris` est accessible directement depuis le menu `Favoris`.

`0.8.4-dev` restaure une parite plus correcte du gestionnaire de favoris dans la coque WinUI: icones de dossiers/liens, bouton visible `Autres favoris` dans la barre, ouverture normale des dossiers, navigation depuis les liens, menu contextuel et import navigateur avec choix fusion/remplacement.

`0.8.5-dev` consolide la direction produit WinUI: la gestion des favoris est aussi exposee dans `Parametres`, `Autres favoris` est separe a droite de la barre, le bouton `Gerer` quitte la barre, les onglets verticaux deviennent fonctionnels, les favicons sont cachees localement quand WebView2 les fournit, et le prototype Win32/CEF est archive comme reference technique.

`0.8.6-dev` corrige l'organisation demandee apres usage: la gestion des favoris quitte `Parametres` et passe dans `Outils > Favoris`, le rail d'onglets verticaux devient redimensionnable et reductible en mode icones, les favicons sont reutilisees aussi dans les onglets, et les dossiers de favoris affiches dans les menus exposent leurs actions utiles directement.

`0.8.7-dev` rend les reglages UI durables: la barre de favoris, l'activation des onglets verticaux, le mode compact et la largeur du rail sont stockes dans `ui-settings.json` dans le profil local. Les onglets retrouvent aussi les favicons deja cachees localement par URL quand elles existent.

`0.39.1-dev` remplace l'ajout direct de l'etoile par un vrai dialogue de favori: nom modifiable, choix du dossier, detection d'un favori deja present et suppression possible depuis le meme dialogue. Le store de favoris sait maintenant mettre a jour et deplacer un lien existant sans creer de doublon.

`0.40.0-dev` ajoute une recuperation locale pour le profil et le coffre : une cle de recuperation est creee avec le profil, peut etre regeneree depuis les parametres, et permet de definir un nouveau mot de passe depuis l'ecran de connexion si l'ancien est oublie. Sans cette cle, Pulse Browser ne contourne pas le chiffrement.

`0.40.1-dev` corrige l'interface compacte : elle ne masque plus la barre des favoris par defaut. Le masquage des favoris devient une option separee et volontaire. Apparence gagne aussi un premier reglage d'effet translucide local (`Mica` ou `Acrylic`) avec retour au rendu solide.

`0.41.0-dev` reprend l'approche translucide validee dans Pulse Explorer : backdrop WinUI plus alpha de fenetre Win32 configurable. Le menu principal est allege pour ne garder que les actions rapides, et les raccourcis de `pulse://accueil` peuvent etre ajoutes, modifies ou supprimes directement depuis la page.

`0.42.0-dev` met en place le multi-utilisateur local : Pulse Browser decouvre les profils presents sur l'ordinateur, permet d'en creer un autre, separe les donnees par dossier de profil, et redemarre proprement quand l'utilisateur change de profil afin que les favoris, l'historique, le coffre et les reglages pointent sur le bon stockage.

`0.50.1-dev` retire l'option utilisateur d'effet translucide. Le chrome revient volontairement a un rendu solide pour garder une identite Pulse plus lisible, plus stable et plus compatible avec les choix d'accessibilite.

`0.51.0-dev` ajoute une premiere gestion locale des permissions par site dans le Centre du site : camera, micro, localisation, notifications, presse-papiers, telechargements multiples et fichiers locaux peuvent etre demandes, autorises ou bloques par domaine.

`0.52.0-dev` rend les telechargements plus utilisables au quotidien : l'historique devient local et persistant par profil, le panneau permet d'effacer ou de retirer des entrees, et les actions d'ouverture sont masquees si le fichier n'existe plus.

`0.53.0-dev` renforce l'import des mots de passe CSV dans le coffre local : Proton Pass, Chrome, Firefox, Bitwarden et formats proches sont reconnus, avec conservation du nom/libelle et de l'URL de connexion quand ils sont presents. L'import demande confirmation apres lecture du CSV avant d'ecrire dans `vault.pulse`.

`0.54.0-dev` ajoute une vraie surface de gestion des utilisateurs/profils dans `Parametres > Profil` : liste des profils locaux, profil actif visible, bascule vers un autre profil, ouverture du dossier de profil, creation de profil et mise en quarantaine securisee des profils non actifs.

`0.54.1-dev` corrige l'ouverture des profils avec emplacement personnalise apres installation : le selecteur recharge le chemin reel du profil choisi, l'import de favoris d'onboarding ecrit dans le dossier cible, et l'installateur ne force plus `PULSE_BROWSER_PROFILE_DIR`.

`0.54.2-dev` rend les imports visibles apres connexion : le menu Pulse expose l'import de favoris, l'ouverture du gestionnaire de mots de passe et l'import CSV des mots de passe, avec les memes acces dans la palette de commande et la section Coffre des parametres.

`0.55.0-dev` ajoute l'import de mots de passe depuis un navigateur Chromium tiers installe sur la machine (Chrome, Edge, Brave, Vivaldi, Opera, Opera GX) : detection automatique des profils, dechiffrement local DPAPI/AES-GCM (comme pour le magasin interne), et un choix CSV/navigateur avant chaque import.

`0.55.1-dev` corrige 4 regressions signalees apres installation du vrai installateur : le WebView2 actif recoit maintenant le focus programmatique (la molette fonctionnait uniquement apres un premier clic) ; la barre de favoris a une vraie respiration verticale et un separateur visuel (elle etait collee a la barre d'adresse) ; le refus automatique des cookies couvre desormais les bandeaux maison sans classe/id reconnaissable (repli par texte exact sur toute la page, corrige amazon.fr) ; la zone de "drag" du titre est recalculee dynamiquement sur l'espace vide de la barre d'onglets, pour que le double-clic maximise la fenetre comme dans les autres navigateurs.

## Commandes

- `build-winui.cmd`: restaure et compile la coque WinUI 3 avec MSBuild Visual Studio.
- `run-winui.cmd`: compile puis lance la coque WinUI 3.
- `run-dev.cmd`: desactive volontairement l'ancien prototype Win32/CEF afin d'eviter les modifications produit au mauvais endroit.
