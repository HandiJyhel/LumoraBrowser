# Pulse Browser - Build propre de test et authenticite

## Objectif

Produire un executable testable en conditions proches d'une distribution reelle, sans embarquer le profil utilisateur du developpeur et sans pretendre disposer d'un certificat Windows officiel.

## Commande

```powershell
.\build-clean-test-artifact.cmd
```

Le build est cree dans:

```text
artifacts/clean-test/
```

Chaque artifact contient:

- `app/PulseBrowser.WinUI.exe` et ses dependances;
- `app/VERIFICATION.txt`;
- `run-clean-profile.cmd`.

Le lanceur `run-clean-profile.cmd` force `PULSE_BROWSER_PROFILE_DIR` vers un dossier `_clean-profile` voisin de l'artifact. Le build ne copie donc pas le profil Pulse Browser normal et ne touche pas au profil utilisateur courant pendant le test.

## Message de confiance

Pendant le developpement, Pulse Browser peut rester non signe Authenticode. Windows peut donc afficher `Editeur inconnu`.

La reponse du projet n'est pas de masquer ce fait, mais de fournir:

- une empreinte SHA256;
- un fichier `VERIFICATION.txt`;
- une section `Authenticite du build` dans `A propos`;
- une piste future de signature open source Sigstore/cosign;
- une piste future Authenticode si le projet justifie un certificat ou un service de signature payant.

## Limites

- Ce build propre n'est pas une release publique.
- Il ne supprime pas les avertissements Windows SmartScreen.
- Il ne remplace pas une signature Authenticode reconnue.
- Il sert a tester Pulse Browser avec un profil vierge et une provenance documentee.
