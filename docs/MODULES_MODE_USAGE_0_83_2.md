# Mode d'usage dans Modules Lumora 0.83.2-dev

## Objectif

Rendre le changement de mode d'usage plus accessible en le plaçant aussi dans
le hub `Modules Lumora`, au même endroit que les outils/ extensions locales.

Le réglage reste disponible dans `Mon Lumora`, mais le hub Modules devient un
accès rapide pour changer immédiatement le rythme de navigation :

- Équilibre ;
- Focus ;
- Lecture ;
- Création ;
- Recherche ;
- Nuit.

## Comportement

Le sélecteur du hub Modules applique le mode immédiatement :

- sauvegarde dans `ui-settings.lumora` ;
- synchronisation du sélecteur `Mon Lumora` ;
- rafraîchissement des pages `lumora://accueil` ouvertes.

## Profil test Bob

Le profil actif local `default`, utilisé comme profil de test Bob sur cette
installation, a été remis dans un état d'interface neuve :

- `PinnedModuleIds` vide ;
- raccourcis du nouvel onglet masqués et vidés ;
- mode d'usage revenu à `balanced` ;
- assistant de première personnalisation marqué comme non terminé.

Le reset ne supprime pas les fichiers sensibles du profil ; il ne vise que les
réglages visibles d'expérience et de personnalisation.
