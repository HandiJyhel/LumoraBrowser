# Pulse Browser - Password Manager 0.29.0-dev

`0.29.0-dev` transforme le gestionnaire de mots de passe en module d'interaction complet, pas seulement en service de coffre.

## Decision

Le gestionnaire ne doit plus etre une suite de corrections dans `MainWindow`.

La separation retenue est:

- `PasswordManagerService` : operations metier sur `vault.pulse`;
- `PasswordManagerInteractionService` : decisions entre une page web observee et les identifiants connus;
- `CredentialService` : pont WebView2 et observateur de page;
- scripts `Credentials/*.js` : detection cote page et remplissage, sans stocker de secret durablement.

`vault.pulse` reste la seule source de verite locale. Chromium/WebView2 ne stocke toujours aucun mot de passe.

## Etats de page

Le script de capture ne se contente plus d'envoyer un identifiant apres soumission.

Il signale maintenant l'etat de la page:

- champ identifiant visible;
- champ mot de passe visible;
- identifiant recemment saisi;
- mutation du DOM;
- clic, focus, touche Entree et chargement de page.

Cela permet de traiter les connexions modernes en deux etapes, par exemple une page qui affiche d'abord l'e-mail puis seulement ensuite le mot de passe.

## Decisions

Le module produit des decisions explicites:

- aucun identifiant connu;
- identifiant connu, mais champ mot de passe absent;
- remplissage disponible;
- offre de sauvegarde apres capture d'un mot de passe.

L'interface ne choisit plus elle-meme la logique. Elle affiche la decision du module.

## Nettoyage

- suppression du vieux `CredentialMatcher.cs`, remplace par `PasswordManagerService.FindBestForAddress`;
- suppression du vieux `RegisterCredentialMonitorAsync` inline dans `MainWindow.Navigation.cs`;
- `BrowserCore_WebMessageReceived` ne traite plus les anciens messages `{t:"cred"}` et reste limite aux passkeys.

## Cas Micromania

Un identifiant enregistre sur `auth.micromania.fr` peut etre reconnu depuis `www.micromania.fr` par domaine racine.

Si le panneau Micromania affiche seulement l'e-mail, Pulse indique que l'identifiant existe mais attend le champ mot de passe. Quand le site affiche ensuite le champ mot de passe, l'observateur de page signale le nouvel etat et la barre de remplissage peut apparaitre.
