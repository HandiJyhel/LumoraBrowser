# Reprise « site introuvable » - 0.75.0-dev

## Contexte

Demande utilisateur : il a voulu aller sur un site dont le nom de domaine ne
correspondait plus -> bloque dans Lumora. Le meme site dans Chrome : Google a
retrouve le bon domaine et l'a redirige malgre la mauvaise adresse. Il demande si
cette fonction est possible dans Lumora.

Analyse : la fonction de Chrome repose sur l'envoi automatique de chaque adresse
en echec aux serveurs de Google — contraire aux principes Lumora (aucune
divulgation sans action utilisateur). Proposition validee par « Go » : suggestion
locale d'abord, recherche web uniquement sur clic explicite via le moteur de
recherche configure. Option « redirection automatique » evoquee mais non
demandee ; non implementee.

## Changements

- `SiteNotFoundRecovery.cs` (nouveau, pur, compile dans les tests) :
  `SearchQueryFor` / `DisplayHostOf` (hote sans www., null hors web),
  `WwwVariantOf`, `ClosestKnownUrl` (distance d'edition bornee 1 ou 2 selon la
  longueur + regle « meme etiquette enregistrable, extension differente » via
  `PublicSuffixService` ; hote identique jamais propose).
- `MainWindow.SiteNotFound.cs` (nouveau partiel) : `OfferSiteNotFoundRecovery`
  (candidats = onglets ouverts + favoris + historique local),
  `HideSiteNotFoundBar`, handlers des trois boutons. La recherche passe par
  `SearchUrl()` existant (moteur configure).
- `MainWindow.Navigation.cs` : declenchement dans `BrowserView_NavigationCompleted`
  (onglet actif + `HostNameNotResolved` + URL web uniquement — les pannes
  transitoires ne declenchent rien) ; masquage dans `NavigationStarting` (onglet
  actif) et `ActivateTab`.
- `MainWindow.xaml` : `SiteNotFoundBar` Row 8 (patron des barres existantes),
  `BrowserHost` passe en Row 9, RowDefinition ajoutee.
- `Lumora.Tests/SiteNotFoundRecoveryTests.cs` : 18 tests (requete de recherche,
  variante www, faute de frappe, mauvaise extension, hote identique exclu, trop
  eloigne exclu, plus proche gagnant, entrees invalides).
- Version : `0.75.0-dev` (`MainWindow.xaml.cs` + `AGENTS.md`).

## Verification (skill verify)

- Build WinUI (MSBuild x64 Debug) OK ; `dotnet test` : 333/333 verts.
- **Live, reussie** — profil isole (`LUMORA_PROFILE_DIR` scratch) + mode invite,
  pilotage UIA (marche recursive Children avec elagage des noeuds Document =
  contenu WebView2 ; saisie par `ValuePattern.SetValue` + clics par
  `InvokePattern` : pas de refus, contrairement aux injections souris/clavier
  refusees en 0.74) :
  1. Navigation vers `https://domaine-disparu-lumora-test-075.fr` -> barre
     « Site introuvable : le domaine ... ne repond plus. Il a peut-etre change
     d'adresse. » avec boutons « Essayer www.domaine-... », « Rechercher ce site
     sur le web », « Fermer ».
  2. « Fermer » (match exact, pas « Fermer l'onglet ») -> barre masquee, onglet
     conserve (1 avant / 1 apres), page d'erreur inchangee.
  3. Nouvel echec -> barre reapparue -> « Rechercher ce site sur le web » ->
     adresse devient `https://www.google.com/search?q=domaine-disparu-lumora-test-075.fr...`,
     resultats Google charges, barre masquee.
- Captures : `sitenotfound-barre.png`, `sitenotfound-recherche.png` (scratchpad de
  session).

## Notes

- La page d'erreur sous la barre reste celle de WebView2, marquee « Microsoft
  Edge » : piste future = page d'erreur maison aux couleurs Lumora.
- L'instabilite aleatoire du process WinUI/WebView2 notee en 0.73/0.74 ne s'est
  PAS manifestee pendant cette verification (deux lancements complets).
