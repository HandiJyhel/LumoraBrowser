# 2026-07-16 - Installeur connecté WebView2 (0.78.3.4.4-dev)

## Demande

Préparer une petite mise à jour de développement pour clarifier le modèle
d'installation de Lumora :

- embarquer l'application et ses ressources utiles dans l'artefact principal ;
- détecter WebView2 au moment de l'installation ;
- télécharger le runtime WebView2 officiel Microsoft si le poste ne l'a pas ;
- garder Lumora local après installation ;
- passer la version à `0.78.3.4.4-dev`.

## Changements

- Version applicative passée à `0.78.3.4.4-dev`.
- Version courante du projet mise a jour dans `AGENTS.md`.
- L'installeur propose maintenant une option cochée par défaut :
  `Télécharger et installer WebView2 si le runtime manque`.
- Le setup détecte WebView2 via les clés registre EdgeUpdate.
- Si WebView2 manque, le setup télécharge le bootstrapper officiel Microsoft :
  `https://go.microsoft.com/fwlink/p/?LinkId=2124703`.
- Le fichier téléchargé est refusé s'il n'est pas signé par Microsoft.
- Le journal `INSTALLATION.txt` écrit par l'installeur indique si WebView2 est
  détecté après installation.
- Retouche demandée après retour utilisateur : interface d'installation agrandie,
  présentation plus lisible, textes visibles corrigés, section `Contenu installé`
  ajoutée pour distinguer les modules Lumora inclus des données utilisateur non
  embarquées.
- Nettoyage de l'ancien dossier technique `artifacts\installer\staging-netfx`,
  qui provenait d'un ancien essai et pouvait créer une confusion avec les
  artefacts Lumora actuels.
- Correction de l'encodage du script d'installeur en UTF-8 avec BOM, afin que
  Windows PowerShell génère correctement les accents dans l'interface et dans le
  fichier `.VERIFICATION.txt`.

## Vérification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` :
  442/442 tests verts.
- `cmd /c .\build-winui.cmd` :
  build WinUI OK, 0 avertissement, 0 erreur.
- Artefact propre Release autonome :
  `artifacts\clean-test\Lumora-0.78.3.4.4-dev-win-x64-clean-20260716-011304`.
- SHA256 exécutable :
  `e94f299f13279fb76de4f892e136a1152d93c37d10e4621eac7616fbd3bce67f`.
- Installeur construit :
  `artifacts\installer\LumoraSetup-0.78.3.4.4-dev-win-x64.exe`.
- SHA256 installeur :
  `fc87e9364c72fc547ff440ea87a01a4da2b4c0e5807b0be97686bc6a25e15748`.
- Taille installeur corrigé :
  `77 964 906` octets.
- Dossier technique ancien :
  `artifacts\installer\staging-netfx` supprimé.

## Note

Cette version reste une version de développement. Elle ne remplace pas le
chantier de signature ni les futures décisions de release.
