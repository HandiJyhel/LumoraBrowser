# Modes compagnons Lumie - 0.83.9-dev

Cette etape corrige la direction des modes compagnons apres retour visuel :
Lumora ne doit pas seulement afficher des boutons de mode, il doit donner
l'impression qu'un petit compagnon local reste disponible pendant la navigation.

## Ce qui change

- Le bouton compagnon devient un bouton lisible `Lumie`, avec une identite
  separee du bouton de changement de mode.
- Le changement de mode redevient explicite avec un libelle visible du type
  `Mode Focus`, au lieu d'une simple icone difficile a comprendre.
- Le flyout Lumie contient maintenant une memoire propre au mode actif :
  objectif Focus, note de lecture, post-it Creation, piste Recherche, rappel
  Nuit ou memo Equilibre.
- Le champ present sur `lumora://accueil` et le champ du compagnon utilisent la
  meme memoire locale. Enregistrer un objectif ou un post-it ne force donc plus
  l'utilisateur a ouvrir le module Notes pour le retrouver.
- Les boutons d'action du compagnon restent contextuels selon le mode actif.
- L'accueil a ete encore espace sur desktop : colonne marque/recherche a
  gauche, colonne mode/compagnon a droite, largeur plus grande et ecarts plus
  respirants.

## Vie privee locale

Les donnees de Lumie sont stockees dans les preferences locales du profil
Lumora (`UiSettings`). Aucun service distant, aucune synchronisation serveur et
aucune telemetrie ne sont ajoutes.

## Limites volontaires

- Lumie n'est pas encore un assistant conversationnel.
- Lumie ne suit pas encore chaque onglet avec une animation libre dans la page :
  cette version pose la base propre et persistante du compagnon.
- Aucun installateur ni executable de release n'a ete genere pendant cette
  etape.
- Aucun lancement automatique de Lumora n'a ete effectue.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 512/512 tests
  reussis.
- Restore MSBuild WinUI hors sandbox apres blocage NuGet/ACL attendu dans le
  sandbox : 0 avertissement, 0 erreur.
- Build WinUI Debug vers
  `artifacts\build-verify\winui-0.83.9-debug\` : 0 avertissement, 0 erreur.
