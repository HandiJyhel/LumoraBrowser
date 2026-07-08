# Profil local et confidentialite

## Objectif

Pulse Browser doit rester pratique: l'utilisateur ne doit pas etre force de se reconnecter sans cesse. Les sessions, cookies essentiels, preferences de sites et futures donnees d'identification doivent donc persister localement.

Cette persistance ne doit pas devenir une dispersion de donnees entre sites. Les donnees utiles a Google/YouTube, par exemple, ne doivent pas servir silencieusement sur un autre site sans action explicite de l'utilisateur.

## Profil local

La version `0.3.0-dev` cree un profil local par defaut:

- base: `%LOCALAPPDATA%\PulseBrowser`
- profil: `%LOCALAPPDATA%\PulseBrowser\profiles\default`
- donnees CEF: `%LOCALAPPDATA%\PulseBrowser\profiles\default\cef-profile`
- donnees de navigation locales: `%LOCALAPPDATA%\PulseBrowser\profiles\default\navigation`
- coffre transparent: `%LOCALAPPDATA%\PulseBrowser\profiles\default\vault\default.pbvault`

Le chemin `root_cache_path` de CEF pointe vers la racine locale Pulse Browser. Le chemin `cache_path` est dans le profil local et reste enfant de cette racine, afin que CEF accepte la persistance des cookies, sessions et stockages web.

## Cookies et sessions

Pulse Browser active les cookies de session persistants. C'est volontaire: un navigateur securise mais inutilisable n'est pas l'objectif.

La politique de depart est:

- accepter les cookies first-party et same-site necessaires au site visite;
- bloquer les cookies dans les contextes tiers quand l'origine de la requete ne correspond pas au site principal;
- bloquer les cookies quand le contexte first-party est inconnu;
- ne pas envoyer ces donnees a un serveur Pulse Browser.

Cette premiere politique est appliquee par un `CookieAccessFilter` CEF. Elle bloque l'envoi et l'enregistrement de cookies tiers, sans bloquer toute la requete reseau.

## Historique et favoris locaux

La version `0.4.0-dev` ajoute une premiere couche de donnees navigateur locales:

- historique web dans `%LOCALAPPDATA%\PulseBrowser\profiles\default\navigation\history.tsv`;
- favoris web dans `%LOCALAPPDATA%\PulseBrowser\profiles\default\navigation\favorites.tsv`;
- exclusion des pages internes `data:` de l'historique et des favoris;
- aucune synchronisation distante.

Ces fichiers sont volontairement separes du coffre d'identifiants. Ils ne doivent pas recevoir de mots de passe, cookies, tokens ou secrets. Ils peuvent contenir des URL de navigation, car c'est leur role local, mais ils ne doivent pas etre envoyes a un serveur Pulse Browser.

La version `0.5.0-dev` conserve ce stockage local et ajoute:

- l'import local de favoris depuis Chrome, Edge, Brave et Chromium;
- une barre de favoris alimentee par `favorites.tsv`;
- l'absence d'envoi des favoris importes vers un serveur Pulse Browser.

La version `0.5.1-dev` rend cet import explicite: l'utilisateur choisit le profil source detecte. En mode remplacement, Pulse Browser sauvegarde d'abord le fichier `favorites.tsv` existant dans le meme dossier local.

La version `0.5.2-dev` ajoute la suppression locale des favoris:

- suppression individuelle depuis le menu;
- suppression complete des favoris Pulse;
- sauvegarde locale automatique avant vidage complet;
- prise en compte de Vivaldi, Opera et Opera GX dans les sources Chromium locales.

La version `0.6.0-dev` ajoute `bookmarks.tsv` pour les favoris hierarchiques. L'ancien fichier `favorites.tsv` reste conserve comme source de migration et les favoris plats existants sont deplaces dans `Autres favoris > Anciens favoris importes`.

## Coffre local transparent

Le coffre local est une brique interne. L'utilisateur n'a pas a le gerer au quotidien.

La version `0.3.0-dev` cree un fichier `default.pbvault` avec:

- une signature de format Pulse Browser;
- une version de format;
- un marqueur chiffre par Windows DPAPI pour l'utilisateur Windows courant.

La version `0.3.1-dev` transforme ce coffre en conteneur d'identifiants chiffre:

- payload interne versionne;
- liste d'identifiants par origine;
- remplacement local d'un identifiant existant pour le meme couple origine/nom d'utilisateur;
- recherche locale par origine pour le futur remplissage transparent.

La version `0.3.3-dev` raccorde ce coffre au prototype de navigation:

- detection locale de certaines connexions envoyees en `POST` URL-encode;
- enregistrement chiffre et silencieux par origine web exacte;
- remplissage transparent sur la meme origine apres chargement de la page;
- absence de mot de passe, token, cookie ou URL complete dans les nouveaux statuts et documents de log.

L'extension `.pbvault` sert a identifier le format Pulse Browser. Elle ne protege pas a elle seule. La protection vient du chiffrement local.

Limite actuelle: la premiere detection vise les formulaires compatibles `application/x-www-form-urlencoded` et les requetes `POST` de navigation principale ou XHR. Certains sites modernes qui utilisent des payloads JSON, des flux multi-etapes ou des connexions federes complexes ne seront pas encore captures.

## Logs et affichage

Les nouveaux statuts de navigation evitent d'afficher les URL completes et preferent le domaine. Les logs projet ne doivent jamais contenir de mots de passe, cookies, tokens, cles secretes ou informations personnelles inutiles.
