# Onglets 0.7

La version `0.7.0-dev` marque le premier palier d'onglets reels dans Pulse Browser.

## Objectif

Le modele d'onglets pose en `0.5.0-dev` ne devait pas rester une structure interne sans effet visible. Cette etape raccorde ce modele a des vues CEF distinctes afin que Pulse Browser puisse ouvrir, activer et fermer plusieurs pages dans la meme fenetre.

## Fonctionnement

- `src/tabs.rs` conserve la liste des onglets, l'onglet actif, le titre, l'URL et l'etat de chargement.
- `src/ui_tabs.rs` affiche une barre d'onglets Win32 provisoire, avec un bouton `+`.
- `src/cef_runtime.rs` cree une instance CEF par onglet, masque les onglets inactifs et restaure l'onglet actif lors d'un changement.
- `src/main.rs` raccorde la creation, l'activation, la fermeture et l'ouverture depuis le menu contextuel des favoris.
- `src/settings.rs` conserve la preference de disposition des onglets: horizontale ou verticale.

## Comportements disponibles

- Ouverture d'un nouvel onglet depuis le bouton `+`.
- Ouverture d'un favori dans un nouvel onglet depuis le menu.
- Changement d'onglet par clic sur la barre.
- Fermeture d'onglet par clic droit sur un bouton d'onglet.
- Reouverture automatique d'un onglet d'accueil si le dernier onglet est ferme.
- Mise a jour du titre et de l'adresse de l'onglet actif via les retours CEF.

## Limites

- La barre d'onglets reste une implementation Win32 provisoire avant la future interface WinUI 3.
- Les onglets ne sont pas encore persistants entre deux lancements.
- Le deplacement/reordonnancement des onglets n'est pas encore disponible.
- Le rendu est limite a vingt boutons d'onglets visibles dans cette coque provisoire.
- Cette etape a ete validee par tests unitaires, formatage et compilation; elle doit encore faire l'objet d'une verification visuelle longue en usage reel.
