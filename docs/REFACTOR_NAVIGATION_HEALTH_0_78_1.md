# Lumora 0.78.1-dev - Refactor : NavigationHealthTracker (sans regression)

## Objectif

`MainWindow` est reparti en 28 fichiers partiels (~13 500 lignes) qui partagent
132 champs prives : le decoupage en fichiers est fait, mais l'etat n'est pas
encapsule — n'importe quel partiel peut toucher l'etat d'un autre via `this`.
C'est cette absence de frontiere qui avait produit le bug d'ordre d'evenements
WebView2 de la 0.76.

Cette version est un **refactor sans changement de comportement** : premier pilote
pour prouver le patron « extraire l'etat et les decisions dans un collaborateur
possede, laisser la colle UI mince sur MainWindow » — le meme patron que les
stores existants (`BookmarkStore`, `PrivacyEngine`, `HistoryPanelController`...).

## Ce qui change (interne uniquement)

Aucune fonction visible ne change. Le cluster « sante de navigation »
(site introuvable + memoire des demenagements + bouclier anti-pub) voit son
**etat et ses decisions** extraits dans une classe pure et testable,
`NavigationHealthTracker`.

- **Sort de MainWindow** (etat + logique, zero UI, zero WebView2) :
  - les 6 champs de suivi par-onglet : `_navigatingDocumentUris`,
    `_mainDocumentHttpErrors`, `_pendingUnknownFailures`, `_explicitNavigationUris`,
    `_adContinueRoots`, `_responseTraceBudget` ;
  - les decisions pures : suivi du document principal, classification des
    reponses 5xx / 301-308 (rendue en verdict typé `MainDocumentSignal`),
    marqueur de navigation explicite, exemptions « Continuer quand meme ».
- **Reste sur MainWindow** (colle UI, inchangee) : les 3 barres, leurs
  textes/boutons, les 7 handlers de clic, `IndicatesSiteDead` (typé WebView2),
  l'extraction des champs WebView2 (statut, en-tete Location) et l'action sur
  le verdict.

Resultat : ~6 champs et ~150 lignes quittent la surface partagee de MainWindow
(132 → 126 champs), et la zone exacte du bug WebView2 de la 0.76 est isolee et
couverte par des tests unitaires.

## Le point clef : ordre des evenements

La classification 5xx conserve le couplage bilateral d'origine : une reponse 5xx
peut arriver avant OU apres `NavigationCompleted(Unknown)`. `ClassifyMainDocumentResponse`
consomme l'echec « arme » et renvoie `TriggerPendingFailure` ; `MainWindow`
verifie l'onglet actif et propose la barre. Comportement identique, mais
desormais testable sans lancer l'appli.

## Protocole anti-regression applique

1. Branche dediee `refactor/navigation-health-0-78-1`.
2. Tests du tracker ecrits pour **verrouiller** le comportement
   (`NavigationHealthTrackerTests`, 18 cas : suivi, redirection qui conserve la
   chaine, oubli d'onglet, classification 301/308/5xx, echec arme dans les deux
   ordres, navigation explicite consommee une fois, « Continuer »).
3. Reecriture purement mecanique (delegation), aucune logique modifiee.
4. `dotnet test` : **383/383 verts** (365 + 18).
5. Build WinUI OK.
6. **Verification live** (UIA + clic natif, mode invite) — scenarios identiques
   a la 0.76/0.77, tous conformes :
   - `zone-telechargement.win` → barre « erreur 522 » ;
   - « Rechercher ce site » → `q=zone-telechargement` ;
   - apprentissage reel `twitter.com -> x.com`, puis « Aller sur x.com » →
     `https://x.com/lumora-test` ;
   - popunder automatique bloque + clic vers `doubleclick.net` bloque, aucun
     onglet parasite.
7. Commit sur la branche uniquement.

## Suite possible

Si le resultat convient, on repete le patron sur d'autres clusters (favicon,
Wallet...), toujours un a la fois avec verification live. On ne touche PAS
`Navigation.cs` (2242 lignes) tant que le patron n'est pas rode.

Details : `logs/2026-07-14-refactor-navigation-health-0-78-1.md`.
