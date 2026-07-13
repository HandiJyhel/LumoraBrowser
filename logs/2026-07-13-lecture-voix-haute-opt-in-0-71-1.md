# Lecture a voix haute en opt-in - 0.71.1-dev

## Contexte

Retour utilisateur : certaines fonctions « accessoires » s'affichent a cote de
la barre d'adresse par defaut. Demande : que l'utilisateur active lui-meme ces
options, SAUF les protections (bloqueur de pub, anti-traceur) qui restent
actives quoi qu'il arrive.

## Diagnostic

Etat reel des boutons de fonction pilotes par les reglages :
- Lecture a voix haute (`ReadAloudEnabled`) : **true par defaut -> visible**.
- Micro / dictee (`AccessibilityVoiceDictationEnabled`) : deja false (opt-in).
- Assistant IA de recherche (`SearchAssistEnabled`) : deja false (opt-in).

Seule la lecture a voix haute etait donc allumee par defaut parmi les fonctions
de confort. La traduction (`TranslationEnabled = true`) reste proactive mais ne
place aucun bouton permanent (barre contextuelle sur pages etrangeres
uniquement) : conservee telle quelle apres arbitrage utilisateur.

Protections inchangees, toujours actives par defaut : bloqueur reseau,
anti-telemetrie, forcage HTTPS, nettoyage de parametres, anti-CNAME, filtre
cosmetique, gestion du consentement cookies, purge de session.

## Changement

- `UiSettings.ReadAloudEnabled` : defaut passe de `true` a `false`. Sur un
  profil neuf, le bouton de lecture n'apparait plus ; l'utilisateur l'active
  dans Reglages > Accessibilite. La logique de visibilite
  (`UpdateReadAloudButtonVisibility`) et le toggle existant n'ont pas bougé.

## Points connus

- N'affecte que les PROFILS NEUFS. Un profil existant conserve la valeur
  enregistree dans son `ui-settings` (le champ y est deja a true) : l'utilisateur
  desactive la lecture a la main dans les Reglages s'il le souhaite. Choix
  volontaire de ne pas ecraser les reglages d'un profil existant.

## Verification

- Build Debug via MSBuild.exe (vswhere) : OK.
- `dotnet test` : 283/283 verts.
- Artefact propre Release :
  `artifacts\clean-test\Lumora-0.71.1-dev-win-x64-clean-20260713-181148`,
  SHA256 exe `43a5a3855225649a7974b5940db230da9f45b492eba5f512a66d941796e887b7`.
- Installateur : `artifacts\installer\LumoraSetup-0.71.1-dev-win-x64.exe`,
  SHA256 `3dc1b96e5002beecad9d06c6565c9c94881b62c8dd348f67717e92432590e01f`.
- Pas de validation manuelle interactive : a verifier sur profil neuf (bouton de
  lecture absent de la barre d'adresse ; l'activer dans Reglages > Accessibilite
  le fait reapparaitre).

**Version :** `0.71.1-dev`.
