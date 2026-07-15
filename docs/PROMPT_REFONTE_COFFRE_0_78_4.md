# Prompt — Refonte du coffre en module autonome (0.78.4-dev)

> Copier-coller le bloc ci-dessous tel quel pour lancer la session de refonte
> du coffre. Il contient le contexte, la carte du code, les contraintes et le
> deroulement attendu.

---

Salut. On attaque la refonte du coffre de mots de passe, troisieme volet des
priorites (0.78.2 = anti-pub + etoile de favori, 0.78.3 = bloc-notes, faits).
**Version cible : `0.78.4-dev`**, branche dediee (ex. `feature/refonte-coffre-0-78-4`).

## Objectif

Faire du coffre un **vrai petit module qui a ses propres fonctions**, revoir
son **ergonomie**, et ajouter **quelques options pratiques** dignes d'un
gestionnaire de mots de passe. Trois axes, dans cet ordre :

1. **Extraction en module** — sans changement de comportement, meme patron que
   le refactor NavigationHealthTracker de la 0.78.1 (etat + decisions dans un
   collaborateur pur et teste, colle UI mince sur MainWindow). Le partiel
   `MainWindow.Vault.cs` (1 351 lignes) melange aujourd'hui : capture
   d'identifiants, autofill, import navigateurs/CSV, export, migration
   Chromium, passkeys, presse-papiers a effacement differe, rendu du panneau,
   dialogues. A separer en composants avec responsabilites nettes.
2. **Ergonomie du panneau** — la liste actuelle est un empilement plat.
   Proposer une vraie organisation (voir « Attentes » ci-dessous).
3. **Options nouvelles** — choisir dans la liste de candidates, proposer avant
   d'implementer.

## Carte du code actuel (~2 900 lignes cote coffre)

- `Lumora.WinUI/MainWindow.Vault.cs` (1 351 l.) : le god-partial a decouper.
- `Lumora.WinUI/VaultStore.cs` (858 l.) : stockage `vault.lumora`
  (AES-256-GCM + Argon2id), deverrouillage mot de passe/PIN, cle de secours,
  verrouillage a la veille/verrouillage Windows (0.71.0). NE PAS toucher au
  format de fichier ni a la crypto sans raison forte.
- `Lumora.WinUI/VaultCredential.cs` (21 l.) : le modele d'identifiant.
- `Lumora.WinUI/PasswordManager/` : `PasswordManagerService` (299 l., pur,
  teste), `PasswordManagerInteractionService` (184 l.), `PasswordHealthAnalyzer`
  (90 l., bilan de sante existant), decisions/drafts.
- `Lumora.WinUI/Credentials/` : `CredentialService` (444 l.) + script de
  capture JS (512 l.), `PasswordGenerator` (40 l., existant mais fixe),
  `CredentialCsv`, `PublicSuffixService`, `ChromiumCredentialReader`.
- `Lumora.WinUI/MainWindow.PasswordHealth.cs` (132 l.) : panneau bilan de sante.
- `Lumora.WinUI/MainWindow.Wallet.cs` (551 l.) : portefeuille (cartes) — même
  famille, ne le refondre que si ca tombe naturellement.
- Panneau XAML : `VaultPanel` dans `MainWindow.xaml` (~ligne 1872).
- Coffre : verrouillage immediat a la veille (0.71.0), detection de changement
  de domaine au login (0.72.0), sante des mots de passe, fusion des doublons,
  import CSV/navigateurs, export CSV en clair, passkeys, effacement differe du
  presse-papiers. **Tout ca existe deja — ne pas le recreer, le reorganiser.**

## Attentes produit (a affiner AVANT d'implementer)

- Ergonomie : regroupement par site (sous-domaines fusionnes via
  `PublicSuffixService`), tri (recent / alphabetique), favicons dans la liste,
  details en volet plutot qu'empiles, actions par entree (copier, ouvrir,
  modifier, supprimer) accessibles sans fouiller.
- Candidates d'options (en choisir 2-3 realistes pour cette version, proposer
  le choix) : generateur configurable (longueur, symboles, phrases de passe),
  tags/categories, notes par identifiant, TOTP (2FA) local, indicateur de
  force a la saisie, corbeille/annulation de suppression, champ de recherche
  qui filtre aussi les tags.
- Toute nouvelle donnee reste dans `vault.lumora` chiffre. Rien ne sort de
  l'appareil (AGENTS.md).

## Deroulement impose

1. Lis `MEMORY.md` (racine) et `AGENTS.md` d'abord, puis explore les fichiers
   cites.
2. **Donne-moi ton avis et ton plan (decoupage du module, maquette d'ergonomie,
   options retenues) AVANT d'implementer. Attends mon « Go ».**
3. Implemente par etapes commitables : extraction sans regression d'abord
   (tests verts a chaque etape), ergonomie ensuite, options enfin.
4. Verification obligatoire : suite `dotnet test` complete, build via
   `scripts/build-winui.ps1` (0 avertissement), verification live UIA avec le
   skill verify (profil jetable — attention : le coffre exige un profil, le
   mode invite ne suffira peut-etre pas ; documente ce que tu fais).
5. Fin de version : `Version = "0.78.4-dev"` dans MainWindow.xaml.cs +
   AGENTS.md, artefact propre (`scripts/build-clean-test-artifact.ps1`),
   installeur (`scripts/build-installer.ps1`), log dans `logs/`, entree
   MEMORY.md, commit sur la branche dediee.

## Garde-fous

- Zero regression sur : autofill, offre d'enregistrement, migration Chromium,
  import/export, passkeys, verrouillage, sante des mots de passe.
- Ne jamais logguer un mot de passe, un TOTP ou une cle (AGENTS.md).
- Si un choix d'architecture est discutable, dis-le franchement et tranche
  avec moi plutot que d'empiler.
