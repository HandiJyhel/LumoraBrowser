# Loupe de lecture - 0.83.17-dev

## Objectif

A la suite d'une reflexion sur les captchas (bypass automatique ecarte :
retire l'humain de la boucle, exigerait soit un moteur de resolution ML fragile
et detectable, soit un service tiers a qui envoyer le contenu de la page - donc
contraire a la philosophie vie privee de Lumora), offrir une aide de confort
alternative : aider l'utilisateur a *lire* un contenu difficile (texte
minuscule, captcha visuel deforme) sans jamais repondre a sa place.

## Comportement

- Nouveau bouton `ReadingLensButton` dans la barre d'outils (glyphe loupe),
  a cote du bouton "Lire a voix haute".
- Opt-in : reglage `AccessibilityReadingLensEnabled` dans Reglages > Confort
  (`ReadingLensEnabledSwitch`), desactive par defaut comme les autres aides de
  confort (dictee, lecture a voix haute).
- Reglage desactive : clic sur le bouton -> message de statut invitant a
  l'activer, rien ne s'ouvre.
- Reglage active sur une page web : clic -> capture d'ecran de la page
  affichee (`CoreWebView2.CapturePreviewAsync`) dans un nouveau panneau
  `ReadingLensPanel`, image affichee dans un `ScrollViewer` zoomable
  (molette + Ctrl, ou pincement, facteur de zoom 0.25x a 6x).
- Bouton "Actualiser la capture" pour reprendre un screenshot a jour (page
  qui a change, captcha regenere...).
- Retour a la page via le bouton de retour habituel des panneaux internes.
- Fonctionne meme sur un contenu d'iframe cross-origin (captcha
  reCAPTCHA/hCaptcha/Turnstile) puisque c'est une image du rendu, pas une
  lecture du DOM : aucune restriction cross-origin a contourner.
- Aucune transcription automatique, aucune resolution du defi a la place de
  l'utilisateur, aucune donnee envoyee a un tiers : une image locale agrandie,
  l'utilisateur lit et agit lui-meme.

## Ecarte de ce lot (idees complementaires, pas retenues ici)

- Reduction de la frequence des challenges anti-bot via un curseur
  compatibilite/vie privee (dependrait du moteur de scoring des fournisseurs
  de captcha, hors de portee directe de Lumora).
- Badge de transparence signalant la presence de reCAPTCHA/hCaptcha/Turnstile
  sur une page (piste dans la lignee du bouclier anti-pub existant).
- Transcription automatique d'un captcha audio : ecartee des le depart, le
  moteur SAPI integre des versions 0.65.x a ete retire pour mauvaise qualite
  de reconnaissance du francais, et rien ne remplace cette brique aujourd'hui.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 517/517 tests
  reussis.
- Build WinUI MSBuild x64 (Debug) : 0 avertissement, 0 erreur.
- Verification en conditions reelles (profil isole, mode invite, pilotage
  UIA) : reglage desactive -> message de garde correct ; activation du
  reglage dans Parametres > Confort persistee ; navigation vers une page web
  puis clic sur la loupe -> panneau affiche avec la capture de la page
  correctement rendue ; bouton "Actualiser la capture" sans effet de bord ;
  retour a la page fonctionnel ; aucune exception dans la trace runtime.

## Notes

- `dotnet build` seul reste inadapte pour WinUI sur cette machine (tache AppX
  `ExpandPriContent` absente du SDK .NET courant) : build via MSBuild Visual
  Studio x64.
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora en dehors de la verification.
