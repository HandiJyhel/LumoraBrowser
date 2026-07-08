# Pulse Browser - Credentials 0.27.1-dev

`0.27.1-dev` ouvre un nouveau chantier propre pour le gestionnaire de mots de passe.

## Decision

Le coffre `vault.pulse` reste la seule source de verite. Chromium/WebView2 ne doit pas stocker ni remplir les mots de passe.

L'ancien systeme de capture reste present comme reference legacy, mais il n'est plus le chemin actif. Le nouveau chemin est isole dans `PulseBrowser.WinUI/Credentials/`.

## Nouveau module

- `CredentialService.cs` initialise le script de capture, recoit les messages WebView2 et lance l'autofill.
- `CredentialCaptureScript.js` detecte les identifiants cote page.
- `CredentialAutofillScript.js` remplit les formulaires avec les setters natifs et les evenements attendus par les frameworks JS.
- `CredentialMatcher.cs` choisit le meilleur identifiant par origine exacte, URL de connexion et domaine racine.
- `PublicSuffixService.cs` centralise la normalisation origine/domaine racine.
- `TestPages/credential-lab.html` sert de banc de test local pour les formulaires classiques, JS sans submit natif et logins en deux etapes.

## Validation attendue

1. Le magasin natif Chromium reste desactive.
2. Une saisie de mot de passe sur une page de login affiche la barre `Enregistrer`.
3. L'acceptation enregistre dans `vault.pulse`.
4. Le retour sur la page propose la barre `Remplir`.
5. Le remplissage fonctionne sur formulaire classique, formulaire JS et login en deux etapes.

## Limites

La vraie Public Suffix List Mozilla n'est pas encore integree. Le module utilise encore une liste courte de suffixes publics a deux niveaux, deplacee dans `PublicSuffixService`.
