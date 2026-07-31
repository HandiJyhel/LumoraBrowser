# Détournement de navigation sans réseau publicitaire répertorié - 0.84.0.3-dev

## Problème

Retour utilisateur : sur `https://ww1.fit/vidlox/`, un clic sur une vignette de
film redirigeait l'onglet en cours (pas de nouvel onglet) vers un domaine
inconnu (`preinvtive.muvonix.shop`) affichant une fausse page de vérification
type Cloudflare en anglais.

Cause : `NavigationHijackPolicy.Decide` ne bloquait une navigation cross-domaine
sans geste utilisateur que si le bouclier réseau avait déjà mesuré une
« pression publicitaire » sur la page (≥ 3 requêtes bloquées) ou si une popup
venait de s'ouvrir (tab-under). Le réseau publicitaire de ce redirecteur était
entièrement absent des listes de filtres : aucune requête bloquée, donc aucune
pression détectée, donc la redirection automatique passait par défaut.

## Correction

- `NavigationHijackPolicy.Decide` bloque désormais toute navigation SANS
  geste utilisateur qui change de domaine, sans condition de pression
  publicitaire mesurée. Toutes les autres protections restent inchangées et
  passent AVANT ce verrou : adresse explicite, whitelist, authentification,
  domaine publicitaire répertorié, même site racine, clic utilisateur réel.
- Paramètre `pageUnderAdPressure` retiré de `NavigationHijackPolicy.Decide`
  (devenu inutilisé) ; `MainWindow.AdShield.cs` ajusté en conséquence. La
  propriété `PageUnderAdPressure` reste utilisée par `PopupPolicy` (popups),
  non touchée par ce durcissement.
- Ne touche PAS à la règle de la régression 0.78.3.3 (« un clic utilisateur
  réel vers un autre site est toujours autorisé ») : le nouveau verrou ne
  s'applique qu'aux navigations où WebView2 indique explicitement l'absence
  de geste utilisateur.

## Fichiers touchés

- `Lumora.WinUI/NavigationHijackPolicy.cs`
- `Lumora.WinUI/MainWindow.AdShield.cs`
- `Lumora.Tests/NavigationHijackPolicyTests.cs`
- `Lumora.WinUI/MainWindow.xaml.cs` (version)
- `AGENTS.md`, `scripts/build-clean-test-artifact.ps1`,
  `scripts/build-installer.ps1` (version)
- `Lumora.Tests/UsageModeVisualIdentityTests.cs` (version)

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj` : 621/621 tests verts,
  dont un nouveau cas dédié
  (`RedirectionAutomatique_DomaineInconnuDesListes_BloqueParasite`) qui
  reproduit exactement le cas vidlox/muvonix.shop en test pur.
- Build Debug WinUI (MSBuild) : réussi, 0 erreur.
- Vérification live (profil jetable, mode invité) : navigation normale
  (adresse explicite vers une page Wikipedia avec contenu riche) toujours
  fonctionnelle après le changement, aucune régression observée.

## Limite

Le clic réel sur `https://ww1.fit/vidlox/` n'a pas pu être rejoué
automatiquement dans cette passe : le contenu WebView2 de la page n'expose
presque rien à l'automatisation UI (arbre d'accessibilité quasi vide côté
page), et l'injection de clic/clavier au niveau système est refusée dans cet
environnement de test. Le comportement est donc verrouillé par le test pur
ciblé et par la vérification manuelle faite par l'utilisateur (captures
d'écran du détournement d'origine), pas par un rejeu automatisé bout en bout.

**Version :** `0.84.0.3-dev`.
