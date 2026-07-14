# Lumora 0.77.0-dev - Renforcement anti-publicite (popups et redirections)

## Objectif

Retour utilisateur : apres avoir visite un site, il s'est retrouve « sur une pub
de jeux impossible a l'enlever ». Le bloqueur reseau (EasyList, EasyPrivacy,
uBlock, AdGuard) filtre les requetes DANS les pages, mais deux portes restaient
grandes ouvertes :

1. **les popups** (`window.open`) etaient toujours acceptees et ouvertes dans un
   onglet — un popunder publicitaire obtenait donc un onglet gratuit ;
2. **les navigations de l'onglet lui-meme** n'etaient jamais filtrees — un clic
   detourne (`location.href` vers un domaine de pub) etait suivi sans broncher.

La 0.77 ferme ces deux portes, en gardant l'utilisateur maitre.

## Ce qui change

- **Blocage des popups** :
  - une popup NON declenchee par un geste utilisateur (popunder automatique) est
    bloquee ;
  - une popup declenchee par un clic mais visant un **domaine repertorie
    publicitaire** (clic detourne) est bloquee ;
  - les **fenetres de connexion** (Google/OAuth, `login.`, `sso.`, chemins
    `/oauth`, `/signin`, `/login`) restent TOUJOURS ouvrables, meme « automatiques » :
    certains flux ouvrent leur fenetre apres un aller-retour reseau ;
  - un site **whitelist** par l'utilisateur garde toutes ses popups.
- **Blocage des redirections publicitaires** : si l'onglet est envoye vers un
  domaine repertorie publicitaire, la navigation est annulee et une barre
  « Navigation bloquee : X est repertorie comme domaine publicitaire » propose
  **« Continuer quand meme »** (autorisation pour la session). Une adresse tapee
  dans la barre, un favori ou une suggestion ne sont JAMAIS bloques.
- **Listes renforcees** : ajout de **Liste FR** (pubs des sites francophones) et
  de **uBlock annoyances** (overlays et pop-ins insistants) aux quatre listes
  existantes. Meme mecanique : telechargement public, cache local, rien d'envoye.
- **Reglages** (Parametres > Confidentialite) : deux interrupteurs
  (popups / redirections), actifs par defaut. Le bouclier compte les popups et
  redirections bloquees comme le reste.

## Position produit

Tout est local : la decision de blocage s'appuie sur les listes deja telechargees
et mises en cache, aucune donnee de navigation n'est envoyee. L'utilisateur garde
la main (barre « Continuer », whitelist par site, interrupteurs).

## Architecture

- `PopupPolicy.cs` (pur, teste) : `Decide` retourne Allow / BlockAutomatic /
  BlockAdDomain a partir de (uri popup, uri opener, geste utilisateur, bloqueur
  actif, predicat domaine-pub, predicat whitelist). `IsLikelyAuthenticationPopup`
  protege les flux de connexion.
- `MainWindow.AdShield.cs` : `BlockPopupIfUnwanted` (branche dans
  `NewWindowRequested`, avant toute creation d'onglet), `ShouldStrictBlockNavigation`
  + barre « Continuer » (branche dans `NavigationStarting`), suivi des
  navigations explicites (`_explicitNavigationUris`) pour ne jamais bloquer ce
  que l'utilisateur demande lui-meme.
- `PrivacyEngine.RecordManualBlock` : compte les blocages hors pipeline de
  requetes (popups, redirections) dans le meme bouclier.
- `NetworkBlockerModule.IsWhitelisted` : expose la whitelist utilisateur a la
  politique de popups.
- `FilterListManager` : deux sources ajoutees (liste_fr, annoyances uBlock).
- `UiSettings.PopupBlockerEnabled` / `StrictAdBlockEnabled` (defaut true).

## Limites connues

- Le blocage strict ne vise que les **domaines** entierement repertories, pas les
  regles de chemin (« /ads/ ») : evite les faux positifs sur des pages legitimes.
- « Continuer quand meme » autorise le domaine pour la **session** seulement ;
  la whitelist des parametres reste le choix durable.
- Fenetre privee : politique de popups non branchee la-bas dans cette version.

## Verification

- Build WinUI OK ; 365/365 tests verts dont 12 nouveaux (`PopupPolicyTests`).
- **Live (UIA + clic natif, mode invite)** sur une page de test locale :
  - popunder automatique (`window.open` au chargement) → **bloque**
    (`Popup blocked (BlockAutomatic)`), la page recoit `null`, aucun onglet
    parasite (capture `077-popups.png`) ;
  - clic sur un bouton `window.open('https://doubleclick.net/...')` → **bloque**
    (`Popup blocked (BlockAdDomain)`), aucun onglet parasite ;
  - popup vers site normal et fenetres d'authentification : autorisees (tests).

Details : `logs/2026-07-14-anti-pub-0-77-0.md`.
