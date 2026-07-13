# Focus clavier rendu au WebView2 au retour d'un panneau + re-offre remplissage - 0.71.2-dev

## Contexte

Retour utilisateur sur holy.com, deux symptomes :
1. Le coffre n'a rien propose au remplissage sur la page de connexion, alors
   qu'un compte existe (l'utilisateur l'a retrouve dans le coffre pour copier
   l'identifiant).
2. Apres etre passe par le coffre et revenu sur le site, plus de menu contextuel
   (clic droit) ni de raccourcis clavier (Ctrl+V...) sur la page.

## Diagnostic

**Symptome 2 (bug confirme).** Le retour a la page via la barre d'etat
(`BackToPageButton_Click`) appelait `ShowPanel(BrowserPanel)` mais NE redonnait
pas le focus clavier au WebView2. Le focus restait dans le XAML du panneau
quitte -> les raccourcis de la page et le menu contextuel ne repondaient plus
tant qu'on n'avait pas reclique dans la page. Le meme correctif existe deja a
l'activation d'onglet (`SwitchToTab` -> `tab.View.Focus`), mais pas au retour
d'un panneau interne.

**Symptome 1 (cause cote site, pas cote coffre).** Verifie : le login
deverrouille le coffre (`EnsureUnlockedWith`/`UnlockWithPin` avant
`DismissLoginOverlay`), et l'auto-verrouillage force un re-login (donc re-
deverrouillage). Pendant la navigation normale, le coffre est DEVERROUILLE :
l'absence d'offre ne vient donc pas d'un coffre verrouille. Cause la plus
probable : le formulaire de connexion de holy.com apparait dans un pop-in /
drawer (boutiques Shopify) ou une iframe, sans champ mot de passe au chargement,
donc aucune offre au moment du `NavigationCompleted`.

## Changements

- `MainWindow.xaml.cs` / `BackToPageButton_Click` :
  - `view.Focus(FocusState.Programmatic)` sur le WebView2 de l'onglet actif au
    retour sur la page (si l'adresse est une page web) -> raccourcis et menu
    contextuel de nouveau operationnels ;
  - `OfferAutoFill(tab.Address)` : re-proposer le remplissage sur la page en
    cours (l'utilisateur revient peut-etre du coffre ou il a consulte un
    identifiant ; l'offre initiale ne se declenche qu'au chargement).

## Points connus / limites

- Le clic droit peut aussi etre bloque en JavaScript par le site lui-meme
  (courant sur les boutiques). Si holy.com le fait, le correctif de focus ne le
  retablira pas : ce ne serait alors pas un bug Lumora.
- Symptome 1 : le correctif re-offre le remplissage au RETOUR sur la page en
  s'appuyant sur le dernier etat de page connu pour le domaine. Si holy.com
  ouvre son formulaire dans un pop-in/iframe non capte, l'offre peut rester
  absente ; il faudra alors regarder la structure exacte de sa page de
  connexion (a confirmer avec l'utilisateur).

## Verification

- Build Debug via MSBuild.exe (vswhere) : OK.
- `dotnet test` : 283/283 verts.
- Artefact propre Release :
  `artifacts\clean-test\Lumora-0.71.2-dev-win-x64-clean-20260713-194221`,
  SHA256 exe `fcb11e91f7abae95adb08e59f9aa2729024ae78256c08b850ea0dcf86a7b546c`.
- Installateur : `artifacts\installer\LumoraSetup-0.71.2-dev-win-x64.exe`,
  SHA256 `3f6ed8b57baeecdb9a0ee3616bf25ffa20ae839f2dfb75ca9ac7f02144cb8c94`.
- Pas de validation manuelle interactive : a verifier par l'utilisateur sur
  holy.com (revenir du coffre -> clic droit + Ctrl+V de nouveau operationnels).

**Version :** `0.71.2-dev`.
