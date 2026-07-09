# Anti-télémétrie — 0.44.0-dev

## Objectif

Empêcher la fuite silencieuse de données d'usage, sur deux plans distincts :

1. **La télémétrie du moteur web lui-même** (WebView2/Chromium vers Microsoft).
2. **La télémétrie des sites visités** (analytics, session replay, rapports de crash, métriques produit).

## 1. Télémétrie du moteur (permanente, non réglable)

Coupée à la racine dans le constructeur de `MainWindow`, avant toute création de
WebView2, via la variable d'environnement officielle
`WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` :

| Argument | Effet |
|---|---|
| `--disable-crash-reporter` / `--disable-breakpad` | Les rapports de crash ne partent plus vers Microsoft (crashpad désactivé). |
| `--disable-domain-reliability` | Plus de rapports de fiabilité réseau (échecs de chargement envoyés à l'éditeur). |
| `--no-pings` | Plus d'audit de liens `<a ping="...">` (beacon envoyé au clic sur certains liens). |

Ce volet n'est **pas un réglage** : c'est la règle n°1 du projet (aucune donnée
ne part vers un serveur sans décision de l'utilisateur).

### SmartScreen (réglable, désactivé par défaut)

SmartScreen vérifie la réputation des sites en envoyant **chaque URL visitée à
Microsoft**. Il est désactivé par défaut (`CoreWebView2Settings.IsReputationCheckingRequired = false`),
appliqué à chaque initialisation de moteur et immédiatement sur tous les moteurs
vivants au changement du réglage. Un toggle `Protection SmartScreen` dans
`Paramètres > Confidentialité` permet de le réactiver pour qui préfère la
protection anti-phishing à la confidentialité des adresses (compromis expliqué
dans le texte du réglage). Persisté dans `UiSettings.SmartScreenEnabled`.

## 2. Télémétrie des sites — `TelemetryBlockerModule`

Nouveau module `Privacy/TelemetryBlocker/` branché sur le `PrivacyEngine`
existant (même mécanique que le bloqueur de pubs) :

- **Seed intégrée** (`TelemetrySeedList`) : ~100 endpoints **dédiés** à la
  collecte — mesure d'audience (Google Analytics, Adobe Analytics, Yandex
  Metrica…), session replay (Hotjar, FullStory, LogRocket, Clarity…), rapports
  de crash (Sentry, Bugsnag, Datadog, New Relic…), métriques produit (Segment,
  Mixpanel, Amplitude…), télémétrie éditeurs (events.data.microsoft.com,
  analytics.tiktok.com…).
- **Principe conservateur** : uniquement des domaines dédiés à la collecte,
  jamais un domaine mixte qui sert aussi du contenu fonctionnel, et aucune
  heuristique de chemin d'URL (leçon du bug `$domain=` : on ne sur-bloque pas).
  Les sites corporate (sentry.io, mixpanel.com…) restent accessibles.
- **Whitelist utilisateur partagée** avec le bloqueur réseau (`PrivacyWhitelist`,
  bouclier et Paramètres).
- **Compteurs propres** (global + par page) : le module est enregistré AVANT le
  bloqueur réseau pour que les domaines présents dans les deux seeds soient
  attribués à la télémétrie.

### UI

- Toggle `Bloquer la telemetrie` dans `Paramètres > Confidentialité`
  (`UiSettings.TelemetryBlockerEnabled`, actif par défaut).
- Le compteur de la page Confidentialité affiche `N requêtes bloquées · dont X télémétrie`.
- Le flyout bouclier affiche la part télémétrie du compteur par page.

## Tests

`PulseBrowser.Tests/TelemetryBlockerTests.cs` (10 tests) : blocage des endpoints
seed et de leurs sous-domaines, aucun faux positif sur domaines légitimes ou
suffixes ressemblants, whitelist (exacte et par sous-domaine), module désactivé,
compteurs global/page, URI invalides, normalisation de la seed.

## Limites connues

- La seed est statique (pas de liste télémétrie téléchargée pour l'instant) ;
  les listes EasyPrivacy du bloqueur réseau couvrent déjà une large part du reste.
- Les collecteurs auto-hébergés sur le domaine du site (first-party, ex. Matomo
  self-hosted) ne sont pas détectables par domaine — hors périmètre volontaire.
