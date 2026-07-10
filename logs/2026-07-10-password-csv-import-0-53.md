# 2026-07-10 - Import CSV des mots de passe 0.53.0-dev

## Objectif

Ajouter une base fiable pour importer des comptes depuis un fichier CSV, notamment pour les utilisateurs de Proton Pass qui ne peuvent pas s'appuyer sur une extension navigateur dans Pulse Browser.

## Changements

- Passage de la version source a `0.53.0-dev`.
- Renforcement de `CredentialCsv` :
  - prise en charge des en-tetes Proton Pass courants (`type`, `name`, `url`, `username`, `password`) ;
  - conservation du libelle (`name`) ;
  - conservation de l'URL de connexion quand elle est disponible ;
  - prise en charge d'en-tetes proches Chrome, Firefox, Bitwarden et 1Password ;
  - exclusion des lignes non-login quand une colonne `type` est presente.
- Le coffre `vault.pulse` peut maintenant fusionner un import enrichi avec libelle et URL de connexion.
- Le gestionnaire de mots de passe demande confirmation apres lecture du CSV et avant ecriture dans le coffre.
- Le bouton d'import annonce explicitement Proton Pass dans son infobulle.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 155/155 tests verts.
- Aucun build d'installateur ni artifact executable n'a ete regenere pendant cette etape.
