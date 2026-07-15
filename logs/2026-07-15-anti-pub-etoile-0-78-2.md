# Renforcement anti-pub + etoile de favori - 0.78.2-dev

## Demande

Priorites utilisateur (image « PRIORITAIRE M.A.J. 0.78.2-dev ») : des pubs
intrusives passent encore le controle sur certains sites et forcent un
changement d'onglet ; ajouter une indication graphique quand le site courant
est en favori. (Notes et refonte du coffre reportees en 0.78.3 / 0.79.)

## Anti-pub : trois brèches fermees

1. **Heuristique d'authentification abusable** : toute URL contenant `/login`,
   `/signin` ou `oauth` dans le chemin passait toujours, meme hors geste et
   vers un domaine inconnu. Desormais (`PopupPolicy`) :
   - fournisseurs d'identite connus (liste d'hotes, match suffixe) : toujours
     ouvrables ;
   - prefixes d'hote conventionnels (`login.`, `auth.`, `sso.`, ...) :
     toujours ouvrables ;
   - mots-cles de chemin : ouverture sur geste utilisateur uniquement ;
   - le test « domaine repertorie publicitaire » est eliminatoire AVANT
     l'heuristique de chemin (`pub.example/login/...` ne passe plus).
2. **Vol de focus** : toute popup autorisee devenait l'onglet actif. Nouveaux
   verdicts `PopupVerdict.AllowInBackground` (site sous pression publicitaire,
   >= 3 requetes bloquees sur la page : la popup s'ouvre sans voler le focus)
   et `BlockGestureFlood` (plafond d'une popup par geste utilisateur, fenetre
   d'1 s par onglet opener).
3. **Tab-under** : l'onglet qui vient d'ouvrir une popup et se redirige
   lui-meme cross-domaine dans les 3 s est bloque avec barre « Continuer quand
   meme » (destinations d'authentification et sites whitelistes exemptes).
   Suivi pur dans `NavigationHealthTracker` (`RegisterPopupOpened`,
   `CountPopupsInGestureWindow`, `HadRecentPopup`, heure injectee).

`MainWindow.AdShield.cs` : `DecidePopupVerdict` + `ReportBlockedPopup`
(popups), `ClassifyNavigationForAdShield` (verdict type Allow / BlockAdDomain /
BlockTabUnder, consommation unique du marqueur « navigation explicite »).

## Etoile de favori

`FontIcon` nomme dans le bouton favoris de la barre d'outils : etoile pleine
(E735) + couleur accent + libelle « Page en favori - modifier ou retirer »
quand la page courante est en favori ; contour (E734) + « Ajouter aux favoris »
sinon. Rafraichie a chaque navigation, changement d'onglet, restauration et
rechargement des favoris (`UpdateBookmarkStar`). Egalite d'URL insensible au
slash final (`https://site.fr` == `https://site.fr/`), aussi appliquee a la
recherche de favori existant du bouton (bug preexistant).

## Favoris du mode invite

Decouvert en verification : en mode invite le store favoris etait un no-op
silencieux alors que l'UI annoncait « Favori ajoute ». Les favoris invites
vivent desormais en memoire pour la session (`_guestNodes`), toujours rien
d'ecrit sur disque.

## Verification

- 395/395 tests verts (12 nouveaux : PopupPolicy durcie, rafale, arriere-plan,
  fenetres de geste / tab-under du tracker).
- Build Debug + Release 0 avertissement / 0 erreur.
- Live UIA (profil jetable, mode invite) : navigation example.com, ajout aux
  favoris via l'etoile + dialogue, etoile passee a l'etat plein (nom UIA
  « Page en favori - modifier ou retirer »), favori visible dans la barre,
  capture d'ecran a l'appui.

## Artefacts

- Artefact propre :
  `artifacts\clean-test\Lumora-0.78.2-dev-win-x64-clean-20260715-145154`
- Manifeste :
  `artifacts\signatures\Lumora-0.78.2-dev-clean-20260715-145225.sha256`
- Installeur : `artifacts\installer\LumoraSetup-0.78.2-dev-win-x64.exe`
  SHA256 `5c3f1ad60f047d1164ddd01f333a97af9c4cd0105e1c1916b313bef47b522216`
- Manifeste installeur :
  `artifacts\signatures\LumoraSetup-0.78.2-dev-20260715-145406.sha256`

**Version :** `0.78.2-dev`.
