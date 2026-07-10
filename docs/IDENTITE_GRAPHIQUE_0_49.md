# Identite graphique Pulse Browser - 0.49.0-dev

Ce palier donne une premiere identite graphique coherente a la coque WinUI active.

## Objectif

L'icone de l'application etait devenue identifiable, mais l'interface restait encore proche d'une coque WinUI generique. Le travail de `0.49.0-dev` pose une base visuelle Pulse sans toucher au moteur WebView2 ni aux flux sensibles.

## Changements

- Accent global WinUI aligne sur l'orange Pulse dans `App.xaml`, pour les boutons accentues et les etats de selection.
- Palette de chrome enrichie dans `MainWindow.xaml` : fond charcoal plus net, surfaces relevees, accent orange et accent secondaire vert menthe.
- Barre d'identite fine au-dessus des onglets, avec degrade orange vers menthe.
- Barre de navigation et barre de favoris harmonisees avec la nouvelle palette.
- Bouton d'ouverture de l'adresse rendu plus distinctif.
- Accueil `pulse://accueil` retravaille : marque Pulse en CSS inspiree de l'icone, fond plus signe, recherche plus claire, raccourcis moins arrondis et moins generiques.
- Overlays de connexion et d'assistant de premier lancement alignes sur la palette Pulse et affichant l'asset `PulseBrowser.png` au lieu d'une tuile "P" simpliste.
- Fenetre d'application web Pulse : barre de sortie de domaine harmonisee avec la nouvelle identite.
- Page A propos : correction de l'information technique pour refleter la stack reelle actuelle, avec stockage local DPAPI + fichiers `.pulse` et chiffrement AES-256-GCM + Argon2id.

## Limites

Ce palier ne pretend pas finaliser tout le design system. Les panneaux profonds restent majoritairement structures comme avant. La prochaine etape logique serait d'unifier les listes, cartes et panneaux de gestion, notamment Favoris, Historique, Coffre, Portefeuille et Applications.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj` : 129/129 verts.
- `build-winui.cmd` : 0 avertissement, 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` apres build : processus vivant, puis fermeture du processus lance pour verification.

**Version :** `0.49.0-dev`.
