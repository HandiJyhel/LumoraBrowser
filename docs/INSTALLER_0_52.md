# Pulse Browser - Installateur propre 0.52.0-dev

## Objectif

Produire un installateur Windows testable localement, sans certificat payant et sans profil utilisateur embarque.

## Commande

```powershell
.\build-installer.cmd
```

Le script utilise le dernier build propre disponible dans:

```text
artifacts/clean-test/
```

Puis genere:

```text
artifacts/installer/PulseBrowserSetup-0.52.0-dev-win-x64.exe
```

## Comportement de l'installation

- dossier d'installation visible avant installation;
- choix de modifier le dossier avec `Parcourir...`;
- installation par defaut par utilisateur, sans privilege administrateur;
- copie de l'application par defaut dans `%LOCALAPPDATA%\Programs\PulseBrowser`;
- choix possible d'un autre dossier, par exemple `Program Files`, si l'installateur est lance avec les droits necessaires;
- choix de creer ou non un raccourci Bureau;
- choix de creer ou non un raccourci Menu Demarrer;
- option d'installation propre pour supprimer le profil installe precedent;
- creation d'une entree de desinstallation Windows sous le registre utilisateur;
- conservation de `VERIFICATION.txt` dans le dossier installe;
- aucun profil utilisateur n'est embarque ou copie.

Le raccourci installe lance Pulse Browser avec:

```text
PULSE_BROWSER_PROFILE_DIR=%LOCALAPPDATA%\PulseBrowser\installed-profile
```

Le dossier de profil n'est pas cree pendant la fabrication de l'installateur. Il sera cree au premier lancement, ce qui permet de tester une installation vierge sans reprendre les profils de developpement.

## Artifact genere

```text
artifacts/installer/PulseBrowserSetup-0.52.0-dev-win-x64.exe
```

SHA256:

```text
f8c41c4eef38fa2af123995f41f8eb69ab224893130c6b075c9bcba3e35ae1b5
```

Manifeste:

```text
artifacts/signatures/PulseBrowserSetup-0.52.0-dev-20260710-174414.sha256
```

## Options visibles

L'installateur propose:

- `Installation propre : supprimer le profil installe precedent`;
- `Dossier d'installation` avec chemin visible et bouton `Parcourir...`;
- `Creer un raccourci sur le Bureau`;
- `Creer un raccourci dans le menu Demarrer`;
- `Lancer Pulse Browser apres l'installation`.

L'option d'installation propre ne touche pas aux profils de developpement ni a un ancien dossier `Desktop\bob`; elle cible seulement le profil dedie a l'installation propre.

## Limites

- L'installateur n'est pas signe Authenticode.
- Windows peut afficher `Editeur inconnu`.
- La confiance repose pour ce palier sur `VERIFICATION.txt`, le SHA256 et la transparence du statut de signature.
