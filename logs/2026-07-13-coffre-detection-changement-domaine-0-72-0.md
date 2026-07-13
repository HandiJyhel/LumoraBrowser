# Coffre : detection d'un changement de domaine au login - 0.72.0-dev

## Contexte

Diagnostic holy.com (captures utilisateur) : le compte etait enregistre sous
`fr.weareholy.com` mais le site vit maintenant sur `fr.holy.com` (HOLY a change
de nom de domaine). Domaines racines differents (`weareholy.com` vs `holy.com`)
-> aucune correspondance -> aucune offre de remplissage. L'utilisateur demande
que le coffre « detecte automatiquement les nouveaux noms de domaine ».

## Decision de conception

Une detection 100% automatique et sure en local est impossible :
- les solutions « automatiques » du marche reposent sur une base cloud
  (affiliations Google) ou une liste maintenue (equivalences Bitwarden) —
  incompatibles avec le « rien ne sort de la machine » de Lumora ;
- deviner sur une ressemblance de nom serait un vecteur de PHISHING (un pirate
  enregistre un domaine sosie et recupere le mot de passe).

Solution retenue, sure et locale : detecter au moment ou l'utilisateur PROUVE
lui-meme que c'est le meme compte, c'est-a-dire quand il se connecte avec succes
sur le nouveau domaine avec un identifiant + mot de passe deja presents au coffre
sous un autre domaine. On propose alors de rattacher le nouveau domaine. Aucune
devinette, aucun reseau.

## Changements

- `PasswordManagerService.FindSameLoginOnOtherDomain(origin, username, password)` :
  cherche un compte au coffre avec MEME identifiant ET MEME mot de passe sous un
  domaine racine DIFFERENT. Correspondance stricte (jamais sur une ressemblance).
- `PasswordManagerInteractionService.BuildSaveOffer` : quand aucun compte n'existe
  pour le domaine courant mais qu'un compte identique existe ailleurs, l'offre
  porte `LinkedFromDomain` (le domaine ou le compte vit deja) et reprend son nom
  personnalise pour la nouvelle entree.
- `PasswordManagerSaveOffer.LinkedFromDomain` (nouveau champ).
- `MainWindow` (barre d'enregistrement) : message dedie
  « Ce compte est deja enregistre pour {domaine}. Ajouter aussi {nouveau} ? » ;
  `_pendingCredential` porte desormais le nom personnalise, repris a
  l'enregistrement. Sur acceptation, une entree pour le nouveau domaine est creee
  (memes identifiant/mot de passe/nom) -> le remplissage marche des la prochaine
  visite. L'ancienne entree est conservee (l'utilisateur la supprime s'il veut).

## Points connus / suite

- Fonction « semi-automatique » : elle se declenche a la 1re connexion reussie
  sur le nouveau domaine (l'utilisateur clique « Ajouter »). Pas de rattachement
  a l'aveugle sur un domaine jamais visite (choix de securite assume).
- Cree une seconde entree (nouveau domaine) plutot que de fusionner les deux en
  un seul « compte multi-domaines » : plus simple et sans risque pour cette
  version. Un vrai multi-domaines par entree pourrait venir plus tard.
- PROCHAINE VERSION prevue : edition complete d'une entree du coffre (site, URL
  de connexion, identifiant, nom) — le filet manuel pour les cas non captes.

## Verification

- Build Debug via MSBuild.exe (vswhere) : OK (app + tests).
- `dotnet test` : 289/289 verts, dont 6 nouveaux
  (`CrossDomainLoginDetectionTests`) : reconnaissance meme compte / autre domaine,
  rejet si mot de passe different (anti-phishing), pas de declenchement meme
  domaine, offre de rattachement avec reprise du nom, offre normale sinon,
  remplissage disponible sur le nouveau domaine apres enregistrement.
- Artefact propre Release :
  `artifacts\clean-test\Lumora-0.72.0-dev-win-x64-clean-20260713-204704`,
  SHA256 exe `f0c9650846afc02ade39fc705c8070e2c05933a56ae71f96b989fdfa5e2a996a`.
- Installateur : `artifacts\installer\LumoraSetup-0.72.0-dev-win-x64.exe`,
  SHA256 `ca62a2ddcf3069874d3dbc2387689d39ee05b53ca27a909fe70d172403411284`.
- Pas de validation manuelle interactive : a verifier par l'utilisateur sur
  holy.com (se connecter sur fr.holy.com -> proposition de rattacher au compte
  weareholy.com -> remplissage disponible ensuite).

**Version :** `0.72.0-dev`.
