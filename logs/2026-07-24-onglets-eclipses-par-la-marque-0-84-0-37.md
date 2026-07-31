# 2026-07-24 - Les onglets reprennent le dessus sur le chrome (0.84.0.37-dev)

## Contexte

L'utilisateur a remonte, apres les passes d'identite des derniers jours
(0.84.0.32 a 0.84.0.36), que "les onglets de base ne sont plus reellement
visibles" - pas casse au sens ou l'appli refuse de demarrer, mais rate au
sens ergonomique : la marque Lumora et le bloc droit du chrome prenaient le
pas sur la bande d'onglets elle-meme.

Verifie en conditions reelles (profil invite, capture d'ecran UIA) avant
toute modification : confirme. Le badge "LUMORA lumiere locale" dans
`TabView.TabStripHeader` occupait presque autant de largeur qu'un onglet, et
la ligne des favoris juste en dessous portait un pave "Constellation" au
meme traitement visuel que ce badge - deux blocs de marque qui rivalisaient
avec le seul repere qui doit dominer cette zone : les onglets.

## Modifications

- `Lumora.WinUI/MainWindow.xaml`
  - `TabView.TabStripHeader` : badge reduit a la seule marque (cercle 24px),
    wordmark "LUMORA" et sous-titre "lumiere locale" retires de cette zone -
    le nom complet reste present ailleurs (menu Lumora, a propos), inutile
    de le repeter juste au-dessus des onglets ;
  - pave "Constellation" de la barre de favoris (positions haut et bas)
    remplace par un simple libelle attenue (opacite 0.6, sans fond ni
    bordure) pour qu'il ne rivalise plus visuellement avec les onglets.
- `Lumora.WinUI/MainWindow.xaml.cs`
  - `UpdateResponsiveChromeLayout()` : logique de repli du sous-titre du
    badge (`collapseTabBrandCaption`) supprimee, devenue sans objet vu que
    le badge est maintenant a taille fixe.
- `Lumora.Tests/UsageModeVisualIdentityTests.cs`
  - deux assertions de texte litteral corrigees pour suivre l'etat reel du
    code, sans lien avec cette passe : `Height = 28`/`FontSize = 12` (attendu
    par un test perime) remplaces par `Height = 36`/`FontSize = 13` (valeur
    reelle des boutons de la barre de favoris depuis une session anterieure
    non commitee) ; `lumiere locale` (sans accent, ne survivait plus que
    dans un commentaire) remplace par `lumière locale` (present dans les
    menus Lumora).
- Version alignee sur `0.84.0.37-dev` (quatrieme chiffre uniquement,
  AGENTS.md, xaml.cs, 2 scripts, test de garde-fou).

## Verification

- Build WinUI via MSBuild (Configuration Debug, Platform x64) : succes.
- Verification visuelle reelle : lancement en profil invite isole, 3 onglets
  ouverts, capture UIA avant/apres - la bande d'onglets redevient l'element
  dominant de sa zone, badge et pave "Constellation" desormais discrets.
- `dotnet test Lumora.Tests` (suite complete, 642 tests) : 641 reussis, 1
  echec preexistant et sans lien
  (`AccessibilityComfortNamingTests.Le_texte_d_indication_de_la_recherche_suit_le_contraste_eleve`,
  assertion sur un extrait CSS de `MainWindow.NewTabHome.cs` qui a deja
  derive avant cette session) - signale mais non traite, hors perimetre de
  cette passe.

## Livraison

- aucun installateur ni executable de release genere sur cette passe.
- reste ouvert, a la demande de l'utilisateur : alleger encore le cluster
  droit du chrome (Studio + icones + menu) si besoin apres relecture visuelle
  de cette correction ; le Studio de mise en page (positions rapides) est
  conserve tel quel, valide explicitement par l'utilisateur.
