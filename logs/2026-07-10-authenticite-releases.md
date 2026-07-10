# 2026-07-10 - Authenticite des builds et signatures open source

## Changements

- Ajout de `docs/AUTHENTICITE_RELEASES.md` pour cadrer les niveaux de confiance: SHA256, Sigstore, Authenticode Windows et badges OpenSSF.
- Ajout de `scripts/generate-release-checksums.ps1` pour produire un manifeste SHA256 d'un fichier ou d'un dossier d'artifacts.
- Ajout du dossier versionne `artifacts/signatures/` via `.gitkeep`.
- Ajustement de `.gitignore` pour continuer a ignorer les artifacts generes tout en conservant le dossier de signatures.

## Decisions

- Pas de bump de version: il s'agit d'une brique de gouvernance de distribution, pas d'une fonctionnalite visible dans `PulseBrowser.WinUI`.
- Sigstore/cosign est retenu comme piste open source principale pour la provenance des artifacts.
- Authenticode reste une piste future necessaire pour l'integration Windows et SmartScreen, a traiter plus pres d'une release candidate.

## Verification

- Verification prevue: execution du script SHA256 sur un artifact de test ou un futur build packagé.
