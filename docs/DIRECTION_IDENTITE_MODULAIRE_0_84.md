# Direction identite modulaire Lumora - cadrage 0.84.0.20-dev

## Intention

Lumora ne doit pas devenir "un Chrome sombre" ni "un Edge clair recolore".
Le navigateur doit porter une identite propre, liee a son nom : lumiere,
clarte, guidance, respiration visuelle, confiance locale.

L'idee retenue est la suivante :

- `Lumora` = l'identite visuelle de base ;
- `clair` et `sombre` = deux ambiances de luminosite de cette meme identite ;
- `modulaire` = une personnalisation de structure encadree, pas un chaos libre.

Autrement dit, le theme Lumora ne change pas selon le mode clair ou sombre :
il garde les memes codes, les memes contrastes d'intention, les memes accents
et la meme sensation produit. Seule la quantite de lumiere change.

## Position produit

Lumora peut se differencier des navigateurs existants par deux leviers qui se
renforcent mutuellement :

- une identite graphique lisible et memorable ;
- une mise en page adaptable aux habitudes de l'utilisateur.

En revanche, il faut eviter un piege : laisser tout bouger partout. Un
navigateur totalement libre devient vite moins coherent, moins beau et moins
maintenable. Lumora doit donc viser une modularite volontairement bornee :
l'utilisateur agence des zones fortes, mais dans un systeme de composition
stable.

## Langage visuel Lumora

Le langage Lumora doit evoquer la lumiere sans tomber dans le clinquant.
L'inspiration n'est pas un neon agressif ; c'est une lumiere utile, douce,
directionnelle.

Principes visuels :

- surfaces profondes mais nettes, jamais boueuses ;
- lisibilite prioritaire sur l'effet ;
- accents lumineux rares mais francs ;
- contours doux, comme des lignes d'horizon ou des halos contenus ;
- contraste propre entre chrome, contenu, outils et etats actifs ;
- impression de guidance plus que de decoration.

Motifs graphiques a reutiliser :

- ligne lumineuse fine pour signaler une zone active ;
- halo discret autour des actions importantes ;
- separation par strates lumineuses plutot que par gros blocs lourds ;
- accents chauds et froids combines, pour rappeler a la fois la lumiere et la
  navigation.

## Palette recommandee

La palette Lumora doit rester reconnaissable en clair comme en sombre.

Couleurs d'intention :

- nuit encree : bleu tres profond pour la base sombre ;
- ivoire mineral : fond clair doux, jamais blanc clinique ;
- or de guidance : accent chaud principal pour les actions, reperes,
  selections utiles ;
- cyan d'orientation : accent secondaire pour la navigation, les outils et les
  etats d'information ;
- graphite doux : texte et contours en theme clair ;
- sable froid : surfaces secondaires en theme clair ;
- perle chaude : texte principal en theme sombre.

Regle produit :

- l'or ne doit pas peindre toute l'interface ;
- le cyan ne doit pas voler la vedette a l'accent principal ;
- les neutres doivent porter la majeure partie de l'interface ;
- les couleurs d'accent servent a guider, pas a remplir.

## Clair et sombre

Le mode clair et le mode sombre ne sont pas deux identites differentes.
Ce sont deux expositions de la meme scene Lumora.

Lumora clair :

- sensation de feuille lumineuse, propre et respirante ;
- ivoire, sable, graphite et liseres or/cyan ;
- ombres courtes et bordures visibles ;
- pas de blanc pur omnipresent.

Lumora sombre :

- sensation d'observatoire nocturne ;
- bleus profonds, surfaces relevees et texte perle ;
- accents lumineux plus visibles mais mieux doses ;
- pas de gris ternes ni de masses noires bouchees.

Invariants dans les deux modes :

- meme hierarchie visuelle ;
- meme logique d'accent ;
- meme forme des composants ;
- meme sentiment de calme et de precision.

## Modularite encadree

L'objectif n'est pas de transformer Lumora en editeur d'interface. L'objectif
est de laisser l'utilisateur organiser ses reperes quotidiens.

Principe :

- les zones structurantes sont deplacables ;
- les controles elementaires ne le sont pas ;
- Lumora propose des placements utiles et coherents ;
- l'utilisateur choisit une composition, pas une reconstruction totale.

## Zones modulables recommandees

### Onglets

Recommandation produit :

- phase 1 : `haut` ou `gauche` ;
- phase 2 eventuelle : `droite` seulement si un vrai besoin d'usage apparait ;
- `bas` non prioritaire, car moins naturel pour la navigation quotidienne.

Raison :

- les onglets sont la colonne vertebrale du navigateur ;
- trop de positions casserait les reperes, les raccourcis et la lisibilite ;
- `haut` et `gauche` couvrent deja la grande majorite des usages reels.

### Favoris

Recommandation produit :

- `haut` ;
- `gauche` ;
- `droite` ;
- `bas` ;
- `masques`.

Raison :

- les favoris sont des reperes plus souples que les onglets ;
- ils se pretent bien a des variantes de presentation ;
- ils peuvent devenir une barre, un rail ou un dock sans casser la logique de
  navigation.

### Actions rapides et modules

Recommandation produit :

- garder une zone stable pres de la barre d'adresse ;
- autoriser l'epinglage, l'ordre et la visibilite ;
- ne pas les rendre flottants partout dans la fenetre.

Raison :

- ces actions servent la rapidite ;
- si elles migrent trop librement, le navigateur perd sa memoire musculaire.

## Presets recommandes

Pour rendre la personnalisation simple, Lumora doit proposer des presets avant
les reglages fins.

Presets de depart recommandes :

- `Classique lumineux` : onglets en haut, favoris en haut ;
- `Atelier` : onglets a gauche, favoris a droite ;
- `Focus` : onglets a gauche compacts, favoris masques ;
- `Panorama` : onglets en haut, favoris en bas.

Ces presets donnent une vraie personnalite au navigateur sans demander a
l'utilisateur de construire toute la grille a la main.

## Consequence UX

La personnalisation ne doit pas etre presentee comme un gadget de theme.
Elle doit vivre dans un espace dedie du Centre Lumora, par exemple sous
`Mon Lumora` ou `Espace de travail`, avec :

- un apercu visuel ;
- 3 ou 4 presets explicites ;
- un mode `Personnalise` ;
- une remise a la disposition recommandee par defaut.

Important :

- `clair` et `sombre` restent des preferences de luminosite ;
- la disposition fait partie de l'identite d'usage ;
- le nom "theme Lumora" doit designer la signature visuelle globale, pas juste
  la noirceur ou la blancheur du fond.

## Ancrage technique dans l'existant

La base WinUI actuelle permet deja ce chantier sans refonte totale.

Points d'appui reels :

- `Lumora.WinUI/MainWindow.xaml` contient deja une vraie ligne de favoris
  separee (`BookmarksBarRow`) ;
- le meme fichier contient deja un rail d'onglets verticaux
  (`VerticalTabsRail`) ;
- `Lumora.WinUI/Models/UiSettings.cs` persiste deja plusieurs choix d'espace
  de travail (`VerticalTabsEnabled`, `VerticalTabsCompact`,
  `VerticalTabsWidth`, `BookmarksBarVisible`) ;
- `Lumora.WinUI/MainWindow.SettingsTheme.cs` dispose deja d'un moteur de
  brosses `Nova*` assez riche pour porter une identite Lumora plus forte dans
  les deux modes.

Conclusion technique :

- la modularite est faisable ;
- il faut d'abord reorganiser la coquille en "slots" ;
- ensuite seulement exposer les nouvelles options a l'utilisateur.

## Reglages cibles recommandes

Proprietes futures conseillees dans `UiSettings` :

- `TabsPlacement`: `top | left` ;
- `BookmarksPlacement`: `top | left | right | bottom | hidden` ;
- `WorkspacePreset`: `classic | atelier | focus | panorama | custom` ;
- `ChromeDensity`: `comfortable | compact` ;
- `LumoraVisualTone`: `signature | soft | vivid` si une variation est un jour
  utile, sans casser la signature principale.

Point de vigilance :

- ne pas confondre "preset d'espace de travail" et "theme clair/sombre" ;
- ne pas exposer 20 curseurs avant d'avoir 4 compositions solides.

## Feuille de route recommandee

### Etape 1 - Signature visuelle

- unifier les tokens chrome, cartes, panneaux et barres autour du langage
  lumineux Lumora ;
- confirmer la palette principale or/cyan sur neutres profonds ou ivoire ;
- verifier la lisibilite reelle avant tout embellissement.

### Etape 2 - Grille modulaire

- transformer la coque principale en emplacements stables :
  `top`, `left`, `right`, `bottom`, `center` ;
- rattacher favoris et onglets a ces slots ;
- garder une seule source de verite de layout.

### Etape 3 - Presets utilisateur

- livrer 3 ou 4 presets de qualite ;
- ajouter un mode personnalise si la structure est deja robuste ;
- memoriser le choix dans le profil local.

### Etape 4 - Raffinement

- animations legeres de transition de layout ;
- apercu visuel dans les reglages ;
- eventuelles variantes futures selon les retours reels.

## Decision recommandee

La direction produit recommandee pour Lumora est :

- une identite de lumiere utile, nette et calme ;
- un theme Lumora unique declinable en clair et sombre ;
- une modularite selective concentree sur les onglets, favoris et reperes
  d'espace de travail ;
- des presets forts avant les reglages avances ;
- une implementation progressive centree d'abord sur `haut` et `gauche` pour
  les onglets, puis sur les differentes positions des favoris.

Cette direction permet a Lumora d'etre plus personnel sans devenir confus, et
plus distinct sans sacrifier la lisibilite.
