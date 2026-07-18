# 2026-07-17 - Chrome visuel des modes (0.83.6-dev)

Suite au retour utilisateur, l'identite des modes ne doit pas rester limitee a
la page d'accueil.

- Version passee a `0.83.6-dev`.
- Ajout d'une couche `ApplyUsageModeChrome` qui applique une palette par mode au
  chrome permanent : fond app, surface haute, barre d'adresse, onglets, rail
  vertical, trait d'identite et bouton `Mode d'usage`.
- La title bar Windows lit maintenant les ressources du chrome Lumora au lieu
  d'utiliser des couleurs fixes.
- Le mode contraste renforce garde une palette specifique noir/blanc/accent et
  reste prioritaire sur les ambiances de mode.
- Ajout d'un test de regression pour verifier que le chrome et la title bar
  restent relies aux modes.
- Aucun installateur ni executable de release genere.

**Verification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` hors sandbox
  apres blocage ACL local : 510/510 tests reussis.
- Restore MSBuild WinUI : reussie, 0 avertissement, 0 erreur.
- Build WinUI valide avec sortie alternative
  `artifacts\build-verify\winui-0.83.6-debug\` : 0 avertissement, 0 erreur.
- Aucun lancement visuel automatique effectue, conformement a la regle projet.

**Version :** `0.83.6-dev`.
