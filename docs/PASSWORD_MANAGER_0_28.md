# Pulse Browser - Password Manager 0.28.0-dev

`0.28.0-dev` separe le gestionnaire de mots de passe du remplissage web.

## Decision

Le gestionnaire de mots de passe devient un module produit autonome. Le navigateur ne doit plus porter directement la logique metier du coffre.

`vault.pulse` reste le fichier chiffre local et la source de verite. WebView2/Chromium ne stocke toujours aucun mot de passe.

## Module

Nouveau dossier:

- `PulseBrowser.WinUI/PasswordManager/PasswordManagerService.cs`
- `PulseBrowser.WinUI/PasswordManager/PasswordManagerEntryDraft.cs`

Le service sait:

- lister les identifiants;
- rechercher par nom, site, domaine, URL ou identifiant;
- ajouter ou mettre a jour une entree;
- renommer une entree;
- supprimer une entree;
- importer/exporter via le coffre existant;
- retrouver le meilleur identifiant pour une adresse web.

Le module ne depend pas de WebView2 ni de `MainWindow`.

## Interface

L'ancien panneau coffre devient un vrai panneau `Gestionnaire de mots de passe`:

- recherche locale;
- ajout manuel avec nom, origine, URL de connexion, identifiant et mot de passe;
- ouverture de la page de connexion;
- renommage;
- suppression avec confirmation;
- copie de l'identifiant;
- copie du mot de passe.

## Integration navigateur

Le navigateur devient client du module:

- la page courante fournit une URL;
- `PasswordManagerService.FindBestForAddress(...)` cherche l'identifiant pertinent;
- l'autofill reste une couche separee, appelee seulement quand l'utilisateur accepte.

Cette structure permet de rendre le gestionnaire utile meme quand l'autofill d'un site complexe doit encore etre ajuste.
