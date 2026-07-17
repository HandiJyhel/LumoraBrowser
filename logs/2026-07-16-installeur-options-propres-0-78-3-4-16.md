# 2026-07-16 - Options d'installateur propres (0.78.3.4.16-dev)

Correction immediate apres retour utilisateur sur `0.78.3.4.15-dev` : les
cases etaient visibles mais le rendu etait casse, avec superposition de textes
dans le panneau `Options`.

Cause : `LumoraOptionCheckBox` heritait encore de `CheckBox`. Le rendu maison
et le comportement natif WinForms se marchaient dessus.

- `LumoraOptionCheckBox` herite maintenant de `Control`, pas de `CheckBox`.
- La propriete `Checked` est conservee pour garder une logique d'installation
  simple et identique.
- Le controle gere lui-meme le clic, Espace et Entree.
- Le dessin efface d'abord sa surface, puis dessine un seul etat visuel :
  hover/focus, carre coche ou decoche, et libelle.
- Les colonnes du panneau `Options` ont ete recalees pour eviter la troncature
  du libelle `Installation propre`.

Verification :

- Projet setup temporaire compile en Release : 0 avertissement, 0 erreur.
- Capture visuelle generee avant build final :
  `artifacts\installer-options\LumoraSetup-0.78.3.4.16-dev-window.png`.
- Aucun chevauchement visible sur la capture, libelles lisibles, cases cochees
  visibles sans envahir le panneau.
- Installateur construit :
  `artifacts\installer-options\LumoraSetup-0.78.3.4.16-dev-win-x64.exe`.
- SHA256 :
  `173705aa9ea1aaa7b2b802ae7e1b5888e89620fd2c815557010c108e0b34417d`.

Note : `artifacts\installer` reste refuse/verrouille dans cette session ; la
sortie valide est donc dans `artifacts\installer-options`.
