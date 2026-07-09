# Sessions éphémères expliquées — 0.46.0-dev

## Contexte

Depuis `0.10.0-dev`, Pulse Browser purge par défaut les cookies et sessions de
la visite précédente à chaque démarrage (voir `SessionPurgeEnabled` dans
`Models/UiSettings.cs`), sauf pour les domaines racines marqués comme sites de
confiance. C'est un vrai différenciateur produit (vie privée par défaut), mais
la fonction était silencieuse : rien n'expliquait pourquoi une session Google
ou autre disparaissait après fermeture, et rien ne proposait de la conserver
au bon moment. Ce palier rend la fonction compréhensible et pilotable sans
changer son comportement par défaut.

## Ce qui change

### 1. Proposition au moment du login

Quand un login est détecté par le module `Credentials` (formulaire avec mot
de passe soumis) et que les sessions éphémères sont actives, une barre
apparaît : *« Rester connecté à `<domaine>` après la fermeture de Pulse
Browser ? »* avec deux actions :

- **Conserver la session** → ajoute le domaine racine aux sites de confiance
  (`SetTrustedSessionSite(root, trusted: true)`, même mécanisme que le
  panneau *Sites connectés*).
- **Non merci** → le domaine est ajouté à `SessionKeepDeclinedSites` : il ne
  sera plus reproposé à chaque connexion sur ce site. L'utilisateur peut
  toujours changer d'avis plus tard depuis *Outils > Sites connectés*, ce qui
  retire aussi automatiquement le site de la liste des refus.

Logique de décision isolée dans `Sessions/SessionKeepAdvisor.cs` (classe pure,
sans E/S) : ne propose jamais si la purge est désactivée, si le site est déjà
de confiance, ou si l'utilisateur a déjà refusé pour ce domaine.

### 2. Explication à la première purge réelle

Au premier démarrage où une purge a effectivement supprimé des données (soit
en purge globale, soit en supprimant au moins un cookie non fiable), une
`InfoBar` discrète s'affiche une seule fois dans la vie du profil :
*« Sessions de la visite précédente nettoyées »*, avec un bouton d'action
« Gérer les sites connectés ». Le flag `SessionPurgeExplained` (dans
`UiSettings`) empêche toute réapparition automatique ensuite.

### 3. Paramètres

Le texte explicatif sous le toggle *Sessions éphémères* (`Paramètres >
Confidentialité`) mentionne désormais la proposition au login, en plus du
fonctionnement déjà documenté.

## Persistance

Deux champs ajoutés à `Models/UiSettings.cs` :

- `bool SessionPurgeExplained` (défaut `false`).
- `List<string> SessionKeepDeclinedSites` (défaut vide).

Chiffrés comme le reste de `ui-settings.pulse` (DPAPI), rétro-compatibles :
un profil existant sans ces champs les initialise à leurs valeurs par défaut.

## Limite connue (déjà documentée avant ce palier)

Quand la liste de sites de confiance n'est pas vide, la purge au démarrage ne
supprime que les *cookies* des sites non fiables ; leur `localStorage` n'est
pas purgé (WebView2 ne permet pas de cibler le stockage par domaine dans ce
cas). Sans impact sur ce palier, toujours vrai après.

## Tests

`Sessions/SessionKeepAdvisor.cs` couvert par `SessionKeepAdvisorTests.cs` :
purge désactivée, site inconnu, site déjà de confiance, site déjà refusé,
domaine racine vide.
