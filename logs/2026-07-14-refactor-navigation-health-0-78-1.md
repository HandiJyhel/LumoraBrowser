# Refactor NavigationHealthTracker - 0.78.1-dev

## Contexte

Discussion avec l'utilisateur sur la solidite du projet : `MainWindow` est un
god-object (28 partiels, ~13 500 lignes, 132 champs prives partages). Le
decoupage en fichiers est deja fait ; ce qui manque, c'est l'encapsulation de
l'etat. Decision validee : premier pilote de refactor **sans regression**, sur
le cluster « sante de navigation » (le plus frais, et celui qui portait l'etat
par-onglet a l'origine du bug WebView2 0.76).

Version demandee par l'utilisateur : `0.78.1-dev` (troisieme nombre = refactor
sans changement de comportement).

## Changements

- **Nouveau** `NavigationHealthTracker.cs` (pur, sans UI ni WebView2, compile
  dans les tests) : possede les 6 dictionnaires/ensembles par-onglet et rend des
  verdicts. API : `TrackNavigationStart(int?,uri,isRedirect)`, `ForgetTab`,
  `IsMainDocument`, `ShouldTraceResponse`, `ClassifyMainDocumentResponse` (→
  `MainDocumentSignal` : None / PermanentRedirect / HttpError avec
  `TriggerPendingFailure`), `ArmPendingUnknownFailure`, `TakeMainDocumentHttpError`,
  `RegisterExplicitNavigation`, `TakeExplicitNavigation`, `AllowAdContinue`,
  `IsAdContinueAllowed`. `DocumentUriKey` et `UrisRoughlyEqual` deviennent
  prives au tracker.
- `MainWindow.SiteNotFound.cs` : champ `_navHealth = new()` ; retire les 6
  champs + 7 methodes deplaces ; `ObserveMainDocumentResponse` reecrit en thin
  (extraction WebView2 → `ClassifyMainDocumentResponse` → switch sur le verdict) ;
  `IndicatesSiteDead` reste (typé WebView2).
- `MainWindow.AdShield.cs` : retire `_explicitNavigationUris` / `_adContinueRoots`
  et la methode `RegisterExplicitNavigation` ; `ShouldStrictBlockNavigation` et
  `AdBlockedContinue_Click` delèguent a `_navHealth`.
- `MainWindow.Navigation.cs` : 7 call sites redirigés vers `_navHealth.*`
  (TrackNavigationStart, ForgetTab, TakeExplicitNavigation/RegisterExplicit,
  TakeMainDocumentHttpError, ArmPendingUnknownFailure, x2 RegisterExplicit).
- `NavigationHealthTrackerTests.cs` : 18 tests verrouillant le comportement.
- Version `0.78.1-dev`.

## Verification (skill verify)

- `grep` de controle : aucun symbole deplacé ni ancien appel résiduel hors du
  tracker ; `DocumentUriKey` n'est plus reference hors du tracker.
- `dotnet test` : **383/383 verts** (365 + 18 nouveaux).
- Build WinUI (MSBuild x64) OK.
- **Live (UIA + clic natif, mode invite)**, scenarios identiques 0.76/0.77 :
  - `zone-telechargement.win` → barre « erreur 522 » + boutons Essayer/Rechercher.
  - « Rechercher ce site » → `google.com/search?q=zone-telechargement`.
  - `twitter.com` → trace `Site relocation recorded: twitter.com -> https://x.com`.
  - `twitter.com:81/lumora-test` (echec force) → bouton « Aller sur x.com » →
    adresse `https://x.com/lumora-test`.
  - Page de test : popunder auto → `Popup blocked (BlockAutomatic)` ; clic vers
    `doubleclick.net` → `Popup blocked (BlockAdDomain)` ; aucun onglet parasite.
- Branche : `refactor/navigation-health-0-78-1` (rien sur main).

## Bilan

Surface partagee de MainWindow : 132 → 126 champs ; ~150 lignes d'etat/decision
extraites et isolees ; zone du bug WebView2 0.76 desormais testee unitairement.
Zero changement de comportement, confirme en live. Patron valide, reutilisable
cluster par cluster.
