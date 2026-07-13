# 2026-07-13 - Bilan de sante des mots de passe 0.69.0-dev

## Changements

- Ajout de `PasswordManager/PasswordHealthAnalyzer.cs` (classe pure) : rapport `PasswordHealthReport` avec groupes reutilises (meme mot de passe exact sur >= 2 origines distinctes ; deux comptes d'un meme site ne sont pas signales), mots de passe faibles (`EvaluateStrength` : longueur < 8, classe de caracteres unique, caractere repete, liste embarquee de mots de passe courants FR/EN) et anciens (non modifies depuis 2 ans, entrees sans date ignorees).
- Ajout de `MainWindow.PasswordHealth.cs` : bouton `Bilan de sante` (glyphe Diagnostic) dans l'en-tete du panneau coffre (colonne ajoutee dans `MainWindow.xaml`), rapport en `ContentDialog` avec explications par section ; les mots de passe ne sont jamais affiches.
- Entree palette de commandes `Bilan de sante des mots de passe`.
- Analyse 100% locale ; la verification type Have I Been Pwned est explicitement exclue (regle projet : aucune requete sortante non validee), note dans le code et la doc.

## Verification

- `dotnet test Lumora.Tests` : 276/276 tests verts (dont 12 nouveaux `PasswordHealthAnalyzerTests` : force, reutilisation inter/intra-site, anciennete avec horloge injectee, rapport sain, signaux independants).
- `build-winui.cmd` : 0 avertissement, 0 erreur.
- Lancement court de `Lumora.WinUI.exe` reussi apres l'ensemble des trois fonctions.
- Test manuel restant : ouvrir le coffre sur le profil reel et verifier la pertinence du rapport (volume, lisibilite du dialogue).
