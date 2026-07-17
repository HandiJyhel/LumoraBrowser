# 2026-07-16 - Options d'installateur plus lisibles (code prepare)

Correction demandee apres capture de l'installateur : les cases cochees etaient
trop peu visibles dans le theme sombre.

- `scripts/installer/Program.cs.template` utilise maintenant
  `LumoraOptionCheckBox`, un controle WinForms dessine maison qui herite de
  `CheckBox`.
- La logique d'installation reste identique : le code lit toujours les memes
  proprietes `.Checked` pour WebView2, installation propre, raccourcis et
  lancement apres installation.
- Le panneau `Options` est legerement agrandi et les lignes d'options ont une
  zone cliquable plus large.
- Etat coche : carre arrondi plus grand, fond accent menthe/orange et coche
  sombre tres lisible.
- Etat non coche : carre vide contraste, contour plus clair et surface interne
  visible.
- Etats hover/focus clavier ajoutes pour rendre les options plus evidentes.

Verification :

- Compilation temporaire du projet setup genere depuis les templates :
  `dotnet build tmp-installer-build\src\Lumora.Setup.csproj --configuration Release`
  reussie avec 0 avertissement et 0 erreur.
- La generation dans `artifacts\installer` n'a pas pu remplacer
  `LumoraSetup-0.78.3.4.14-dev-win-x64.exe` car Windows a renvoye
  `Access denied`, puis a refuse la creation de `staging-dotnet`.
- Generation finale relancee dans `artifacts\installer-options` apres
  autorisation reseau pour la restauration NuGet.
- Installateur produit :
  `artifacts\installer-options\LumoraSetup-0.78.3.4.15-dev-win-x64.exe`.
- SHA256 :
  `36e9be25017703cedfd4b10b68870d659345953780e282aafb19e4604455587d`.

Version livree pour ce micro-correctif d'installateur : `0.78.3.4.15-dev`.
