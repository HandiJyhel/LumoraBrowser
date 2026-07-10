# Accessibilite et coherence graphique - 0.50.0-dev

Ce palier consolide l'identite Pulse posee en `0.49.0-dev` et l'etend aux surfaces de navigation et d'accessibilite les plus visibles.

## Objectif

L'application est suffisamment stable pour que le chrome, les panneaux internes et les controles essentiels cessent de fonctionner comme des morceaux separes. `0.50.0-dev` pose donc un socle de coherence utilisable : focus clavier visible, ressources visuelles partagees, surfaces profondes harmonisees et noms accessibles pour les controles iconiques.

## Changements

- Ajout de ressources globales dans `App.xaml` pour le focus Pulse, les surfaces et les traits de controle.
- Renforcement des styles de boutons du chrome : bordure discrete, focus systeme visible, focus jaune Pulse et variante accentuee pour l'action d'ouverture de l'adresse.
- Ajout de noms `AutomationProperties.Name` sur les boutons iconiques principaux : navigation, accueil, menu, confidentialite, favoris, interface compacte, Picture-in-Picture et palette de commande.
- Barre d'adresse et recherche de palette equipees d'un nom accessible et du focus visuel Pulse.
- Extension de `ApplyAccessibilitySettings()` : les modes contraste renforce, texte plus lisible et focus visible mettent maintenant a jour davantage de ressources, barres contextuelles et controles.
- Ajout d'un helper runtime pour appliquer les conventions de focus et les noms accessibles aux controles generes par code.
- Barre de favoris : les favoris et dossiers generes dynamiquement recoivent un libelle accessible explicite.
- Barres contextuelles sensibles (identifiants, autofill, portefeuille, session conservee) alignees sur les surfaces Pulse.
- Panneau Parametres, barre d'etat, palette `Ctrl+K` et fenetre d'application web harmonises avec les ressources Pulse.
- Barre d'etat marquee comme region live polie pour exposer les changements de statut sans interrompre l'utilisateur.
- Version projet passee a `0.50.0-dev`.

## Limites

Ce palier ne remplace pas un audit complet WCAG avec lecteur d'ecran et navigation clavier exhaustive. Les listes profondes creees par code dans Coffre, Portefeuille, Applications et Historique devront encore etre harmonisees progressivement avec le meme helper et le meme langage de surfaces.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj` : 136/136 verts apres relance avec acces NuGet autorise.
- `build-winui.cmd` : premier essai bloque par le sandbox reseau NuGet (`NU1301`), relance autorisee reussie avec 0 avertissement et 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` : fenetre `Pulse Browser 0.50.0-dev`, processus repondant, fermeture propre du processus de test.

**Version :** `0.50.0-dev`.
