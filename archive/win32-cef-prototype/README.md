# Prototype Win32/CEF historique

Ce dossier marque l'ancienne coque Win32/Rust comme archive technique.

## Regle

Ne pas ajouter de nouvelles fonctions produit ici.

La seule interface produit active de Pulse Browser est maintenant:

`PulseBrowser.WinUI`

Le code Rust reste utile comme coeur local et comme reference pour le futur raccord CEF, mais la coque Win32 ne doit plus recevoir de travail d'interface, de favoris, d'onglets, de menus ou de parcours utilisateur.

## Pourquoi

Le projet a maintenant deux surfaces possibles: l'ancienne coque Win32 et la coque WinUI 3. Pour eviter qu'un assistant ou un contributeur modifie la mauvaise interface, les lanceurs ambigus `run-dev.cmd` et `scripts/run-dev.ps1` sont neutralises.

## Lancement legacy explicite

Le script ci-dessous existe uniquement pour diagnostic technique ponctuel:

`archive/win32-cef-prototype/run-legacy-win32-prototype.ps1`

Tout lancement de ce script doit etre volontaire et ne doit pas servir a valider une fonction produit.
