# Popups en attente + icône de récupération - 0.84.0.6-dev

## Contexte

Après trois durcissements successifs (0.84.0.3 à 0.84.0.5), l'utilisateur a
demandé si le problème était structurellement soluble. Réponse : oui, mais
pas en devinant mieux - en changeant de modèle. Un vrai clic qui ouvre un
popup légitime (partage, connexion, paiement) et un vrai clic détourné vers
une pub sont, techniquement, indiscernables l'un de l'autre pour le moteur.
Aucune heuristique ne peut deviner juste à tous les coups sur le tout premier
popup d'une page.

Recherche effectuée sur des extensions du marché avant d'écrire du code (voir
échange précédent) : aucune technologie récupérable. Les bloqueurs
d'extension type uBlock Origin sont majoritairement inopérants depuis la fin
du support Manifest V2 (juillet 2025), leur code est sous licence GPL
(incompatible avec une simple reprise dans Lumora, projet bientôt public), et
WebView2 ne fournit AUCUN blocage de popup natif à activer - le blocage natif
d'Edge est explicitement désactivé pour les apps WebView2, précisément pour
laisser l'application décider. Décision : construire le modèle
« bloqué par défaut + reprise de contrôle utilisateur », comme Chrome,
Firefox et Edge le font nativement dans leur propre interface (pas via une
extension), directement dans le code de Lumora.

## Changement de comportement

- `PopupPolicy.Decide` : nouveau verdict `BlockPendingUserChoice`. Un vrai
  clic vers un domaine cross-site qui n'est ni whitelisté, ni un fournisseur
  d'identité connu, ni le même site racine, ni sous pression publicitaire
  n'est plus autorisé par défaut - il est retenu en attente d'un choix
  explicite. Toutes les autorisations de confiance (whitelist, IdP connus,
  même site racine, blocages fermes déjà existants) restent inchangées et
  prioritaires, dans le même ordre qu'avant.
- Nouvelle icône de barre d'outils (`PopupRecoveryButton`, dans la même
  rangée de modules que Mode lecture/Notes/Lecture à voix haute) : masquée
  tant qu'aucune popup n'est en attente sur l'onglet courant, avec un badge
  de compte quand il y en a. Son ouverture liste les popups en attente avec
  un bouton "Ouvrir" chacune, et un bouton "Toujours autoriser les popups de
  ce site" qui ajoute le site à la même liste de confiance que le bouclier
  de confidentialité (`ShieldSiteExcludeToggle` / `_uiSettings.PrivacyWhitelist`)
  et ouvre tout ce qui était en attente.
- `NavigationHealthTracker` : nouveau suivi par onglet des popups en attente
  (`RecordPendingPopup` / `PendingPopups` / `RemovePendingPopup` /
  `ClearPendingPopups`), oublié comme les autres états par-onglet à la
  prochaine navigation fraîche ou à la fermeture de l'onglet.

## Fichiers touchés

- `Lumora.WinUI/PopupPolicy.cs`
- `Lumora.WinUI/NavigationHealthTracker.cs`
- `Lumora.WinUI/MainWindow.AdShield.cs`
- `Lumora.WinUI/MainWindow.Navigation.cs`
- `Lumora.WinUI/MainWindow.PopupRecovery.cs` (nouveau)
- `Lumora.WinUI/MainWindow.xaml`
- `Lumora.Tests/PopupPolicyTests.cs`
- `Lumora.Tests/NavigationHealthTrackerTests.cs`
- Fichiers de version → `0.84.0.6-dev`

## Vérification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj` : 635/635 tests verts,
  dont les cas dédiés au nouveau verdict et au suivi des popups en attente.
- Build Debug WinUI (MSBuild) : réussi, 0 erreur, XAML + code-behind compilés
  sans erreur.
- Vérification live (profil jetable, mode invité, fenêtre maximisée) :
  navigation normale (adresse explicite, page Wikipédia riche) toujours
  fonctionnelle, icône de récupération correctement masquée par défaut (aucun
  popup en attente), disposition de la barre d'outils non perturbée.
- Écarté un faux doute : les icônes Mode lecture/Notes/Lecture à voix haute
  n'apparaissaient pas dans les captures, mais c'est un comportement
  préexistant sans rapport (elles ne s'affichent que si l'utilisateur les a
  « épinglées », `MainWindow.UsageMode.cs`) - la nouvelle icône gère sa
  propre visibilité indépendamment de ce système.

## Limite

Comme pour 0.84.0.3/0.84.0.4/0.84.0.5, je n'ai pas pu déclencher un vrai clic
détourné dans l'environnement de test (page non exposée à l'automatisation
UI, injection de clic refusée par le bac à sable) : je n'ai donc pas vu
l'icône apparaître en conditions réelles suite à un clic effectif, seulement
vérifié par le code que le câblage (verdict → mémorisation → rafraîchissement
→ visibilité) est correct et par les tests purs que la décision elle-même
l'est. À confirmer par l'utilisateur en usage réel.

**Version :** `0.84.0.6-dev`.
