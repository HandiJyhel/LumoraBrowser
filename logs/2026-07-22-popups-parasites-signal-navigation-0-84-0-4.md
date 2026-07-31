# Popups parasites après une navigation déjà détournée - 0.84.0.4-dev

## Problème

Après le correctif 0.84.0.3-dev (navigation automatique cross-domaine
bloquée par défaut), l'utilisateur a signalé que des nouveaux onglets
s'ouvraient encore, sans rapport avec l'onglet qui change tout seul (déjà
réglé). Confirmé : deux mécanismes distincts sur le même type de site
(0.78.3.1 le décrivait déjà : « chaque clic téléporte l'onglet, ou un onglet
neuf »), gérés par deux politiques séparées — `NavigationHijackPolicy`
(onglet qui change) et `PopupPolicy` (nouvel onglet). Seule la première avait
été corrigée.

Cause : `PopupPolicy.Decide` autorise un premier popup par geste utilisateur
réel, sauf si le site est déjà « sous pression publicitaire » (mesurée
uniquement par le compteur de requêtes bloquées du bouclier réseau). Quand le
réseau publicitaire est absent des listes de filtres, ce compteur ne monte
jamais, et le premier popup vers un domaine inconnu passe.

## Pourquoi PAS le même correctif que 0.84.0.3

Contrairement à une redirection automatique de l'onglet (jamais légitime),
un popup déclenché par un vrai clic EST un usage web courant et légitime
(connexion OAuth, partage, paiement, chat). Bloquer par défaut tout premier
popup cross-domaine casserait ces usages bien plus souvent qu'il n'arrêterait
de pubs. Décision : ne pas toucher au comportement du tout premier popup sur
une page vierge — limite documentée et assumée.

## Correction

- `NavigationHealthTracker` : nouveau suivi par onglet
  (`RecordBlockedNavigation` / `HadBlockedNavigation`) qui mémorise qu'une
  navigation a déjà été bloquée par `NavigationHijackPolicy` sur cet onglet.
  Oublié à la prochaine navigation fraîche (pas un saut de redirection) et à
  la fermeture de l'onglet, comme les autres états par-onglet.
- `MainWindow.AdShield.cs` : `ClassifyNavigationForAdShield` enregistre ce
  signal dès qu'un verdict de blocage tombe. `DecidePopupVerdict` calcule
  désormais `openerUnderAdPressure` comme
  `PageUnderAdPressure OU navigation déjà bloquée sur cet onglet` — les deux
  boucliers se renforcent l'un l'autre sans dépendre exclusivement des listes
  de filtres.
- Effet concret : une fois qu'UNE tentative de détournement a été bloquée sur
  un onglet (peu importe comment elle a été détectée), les popups cross-domaine
  suivants issus du même onglet sont bloqués net, même vers un domaine encore
  inconnu des listes.
- `PopupPolicy.cs` non modifié : seul le calcul du signal d'entrée change.

## Fichiers touchés

- `Lumora.WinUI/NavigationHealthTracker.cs`
- `Lumora.WinUI/MainWindow.AdShield.cs`
- `Lumora.Tests/NavigationHealthTrackerTests.cs`
- Fichiers de version (`MainWindow.xaml.cs`, `AGENTS.md`, scripts, test de
  cohérence) → `0.84.0.4-dev`

## Vérification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj` : 625/625 tests verts.
- Build Debug WinUI (MSBuild) : réussi, 0 erreur.
- Build propre (Release) + installateur `0.84.0.4-dev` générés avec succès ;
  ancien installateur (`0.84.0.3-dev`) supprimé du dossier
  `artifacts/installer/` à la demande explicite de l'utilisateur.

## Limite assumée

Le tout premier popup sur une page qui n'a encore rien déclenché (aucune
navigation bloquée, pression réseau non mesurée) reste indiscernable d'un
popup légitime déclenché par un vrai clic — non traité par ce correctif, par
choix (cf. section ci-dessus). Comme pour 0.84.0.3, le rejeu automatisé du
clic réel sur vidlox n'a pas été possible dans l'environnement de test (page
non exposée à l'automatisation UI) ; comportement verrouillé par les tests
purs, pas par un rejeu bout en bout.

**Version :** `0.84.0.4-dev`.
