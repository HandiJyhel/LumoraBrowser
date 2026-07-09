# Portefeuille numérique — 0.45.0-dev

## Objectif

Retenir localement les moyens de paiement de l'utilisateur et les remplir sur les
pages de paiement, **sans jamais permettre de traçabilité** : rien ne quitte la
machine, rien n'est exposé aux scripts des sites, rien n'est confié au moteur web.

## Principes de sécurité (par conception)

| Principe | Implémentation |
|---|---|
| Coffre souverain | Cartes stockées uniquement dans `vault.pulse` (blob `cards`), même clé et même AES-256-GCM/Argon2id que les mots de passe. Autofill Chromium désactivé. |
| CVV jamais stocké | Pas de champ CVV dans le modèle ni les dialogues ; l'utilisateur le saisit au paiement. |
| Pas de pré-remplissage silencieux | Le remplissage n'a lieu QUE sur clic explicite (barre de proposition ou panneau). Un script de page ne peut pas sonder le portefeuille. |
| Détection passive | Le script moniteur signale la présence d'un champ carte, ne lit aucune valeur, n'expose rien à la page. |
| Affichage masqué | `•••• •••• •••• 1234` partout ; le numéro complet n'apparaît jamais à l'écran. |
| Copie protégée | Presse-papiers hors historique/synchro cloud, effacement automatique à 30 s. |
| Barrière d'accès | PIN ou mot de passe redemandé à chaque ouverture du panneau ; verrouillage de session purge la clé (`Lock()`). |

## Stockage

- `Wallet/VaultPaymentCard.cs` : `id`, `label`, `holder`, `number` (chiffres
  normalisés), `exp_month`, `exp_year`, `note`, horodatages.
- `VaultStore` : blob `cards` séparé dans l'en-tête, toujours GCM. Copie de
  récupération `recovery_cards` maintenue comme celle des identifiants
  (déverrouillage par clé de secours restaure aussi les cartes). Mode DPAPI
  couvert. **Rétro-compatible** : un coffre sans champ `cards` s'ouvre normalement
  avec un portefeuille vide.

## Validation à la saisie

`Wallet/PaymentCardUtil.cs` (logique pure, testée) : somme de Luhn (12-19
chiffres) pour détecter les fautes de frappe, détection du réseau
(Visa/Mastercard/American Express/Discover) par préfixes publics, normalisation
d'année (27 → 2027), carte valable jusqu'à la fin de son mois d'expiration.

## Remplissage

1. Script moniteur injecté à la création de chaque document (tous moteurs) :
   sélecteurs `autocomplete*="cc-number"` + heuristiques conservatrices
   name/id (`cardnumber`, `card-number`, `card_number`, `ccnumber`), avec
   `MutationObserver` pour les checkouts SPA. Signale une fois, puis se déconnecte.
2. `WalletFillBar` s'affiche sur l'onglet actif (aucune donnée de carte visible).
3. Au clic `Remplir` : déverrouillage si nécessaire, choix de la carte si
   plusieurs, puis injection : numéro, titulaire, expiration (champ combiné
   `MM/AA` ou mois/année séparés, `input` ou `select`), événements
   `input`/`change`/`keyup`/`blur` déclenchés pour les frameworks JS.
4. Le CVV n'est jamais touché.

## Accès

- Palette `Ctrl+K` → « Portefeuille ».
- `Paramètres > Coffre > Ouvrir le portefeuille`.
- Depuis le panneau : « Utiliser sur la page active ».

## Limites connues

- Les formulaires de paiement dans des iframes cross-origin (Stripe Elements,
  Adyen…) sont inaccessibles au remplissage — message explicite à l'utilisateur.
- Pas de capture automatique d'une carte saisie sur un site marchand (choix v1 :
  ajout manuel uniquement).
- IBAN et adresses : hors périmètre v1, le modèle du coffre est extensible.
