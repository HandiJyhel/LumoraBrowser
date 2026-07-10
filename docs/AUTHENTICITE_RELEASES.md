# Pulse Browser - Authenticite des builds et futures releases

## Objectif

Mettre en place une chaine simple pour prouver qu'un build Pulse Browser vient bien du projet et n'a pas ete modifie apres generation.

Cette etape ne transforme pas encore `0.x-dev` en release publique. Elle prepare la distribution future sans promettre une maturite de release.

## Niveaux de confiance

### 1. Empreintes SHA256

Chaque artifact distribue doit avoir une empreinte SHA256 publiee avec lui. C'est le premier niveau de verification: l'utilisateur ou le mainteneur peut verifier que le fichier telecharge correspond exactement au fichier produit.

Generation locale:

```powershell
.\scripts\generate-release-checksums.ps1 -ArtifactPath .\artifacts\release\PulseBrowser-0.52.0-dev-win-x64.zip
```

Pour un dossier complet:

```powershell
.\scripts\generate-release-checksums.ps1 -ArtifactPath .\artifacts\release
```

Le manifeste est ecrit dans:

```text
artifacts/signatures/
```

### 2. Signature Sigstore

Sigstore/cosign est le candidat open source naturel pour signer les artifacts et produire une preuve publique de provenance. La documentation officielle indique que `cosign sign-blob` peut signer des fichiers standards et generer un bundle contenant la signature, le certificat et la preuve d'inclusion dans le journal de transparence.

Commande cible, a activer quand `cosign` sera installe ou branche dans la CI:

```powershell
cosign sign-blob .\artifacts\release\PulseBrowser-0.52.0-dev-win-x64.zip --bundle .\artifacts\signatures\PulseBrowser-0.52.0-dev-win-x64.zip.sigstore.json
```

Verification cible:

```powershell
cosign verify-blob .\artifacts\release\PulseBrowser-0.52.0-dev-win-x64.zip --bundle .\artifacts\signatures\PulseBrowser-0.52.0-dev-win-x64.zip.sigstore.json
```

Si la signature est faite depuis GitHub Actions, la verification devra aussi contraindre l'identite OIDC attendue du depot et le workflow autorise.

Reference: https://docs.sigstore.dev/cosign/signing/signing_with_blobs/

### 3. Signature Windows Authenticode

Pour reduire les alertes Windows et donner une identite editeur dans les proprietes du fichier, Pulse Browser aura besoin d'une signature Windows Authenticode pour les executables et installateurs.

Cette brique est differente de Sigstore:

- Sigstore prouve la provenance open source et la transparence de signature.
- Authenticode ameliore l'integration Windows, SmartScreen et l'affichage de l'editeur.

Piste future: Microsoft Artifact Signing / Trusted Signing, ou un certificat de signature de code equivalent. Cette decision doit etre prise plus pres d'une release candidate, car elle engage des couts, de l'identite editeur et une procedure de stockage de cle.

Reference: https://learn.microsoft.com/en-us/azure/artifact-signing/overview

### 4. Badges open source

OpenSSF Best Practices et OpenSSF Scorecard ne signent pas les binaires, mais ils servent de preuve publique que le projet suit des pratiques de securite open source.

Pistes futures:

- publier le depot;
- ajouter `SECURITY.md`;
- documenter le processus de build;
- signer les artifacts;
- viser un badge OpenSSF Best Practices.

References:

- https://www.bestpractices.dev/en
- https://github.com/ossf/scorecard

## Regle projet

Tout artifact partage hors du poste de developpement doit etre accompagne au minimum:

- d'un manifeste SHA256;
- du numero de version exact;
- du commit ou de l'etat source utilise pour construire l'artifact;
- d'une note indiquant si l'artefact est signe Sigstore, signe Authenticode, ou non signe.

Pour les builds `0.x-dev`, il faut eviter le vocabulaire de release publique. Utiliser `build de developpement`, `artifact de verification` ou `preparation release`.
