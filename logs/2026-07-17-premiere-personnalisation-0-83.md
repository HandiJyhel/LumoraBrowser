# 2026-07-17 - Premiere personnalisation de profil (0.83.1-dev)

Suite au retour utilisateur indiquant qu'un compte neuf devrait demarrer sans
modules visibles imposes, Lumora adopte une logique de premiere personnalisation.

- Version passee a `0.83.1-dev`.
- Correction de numerotation : cette etape prolonge la personnalisation deja
  lancee et doit rester une mise a jour mineure `0.83.1-dev`, pas `0.83.0-dev`.
- Les nouveaux profils ont `PinnedModuleIds` vide par defaut.
- Les anciens profils sans cle `PinnedModuleIds` sont migres vers les modules
  historiques pour eviter de casser leur interface.
- Le wizard premier lancement passe de 3 a 4 etapes.
- Nouvelle etape `Votre Lumora` : choix du mode d'usage et des modules visibles.
- Tous les modules du wizard sont decoches par defaut pour un profil neuf.
- `lumora://accueil` affiche une invitation `Construisez votre Lumora` quand le
  profil n'a aucun module epingle ni raccourci visible.
- Les boutons de cette invitation ouvrent directement `Mon Lumora` ou
  `Modules Lumora`.
- Aucun installateur ni executable de release genere.

**Verification** :
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 507/507 tests verts.
- `powershell -ExecutionPolicy Bypass -File scripts\build-winui.ps1` : build WinUI reussi hors sandbox apres blocage NuGet/obj attendu dans le sandbox, 0 avertissement, 0 erreur.
- `run-winui.cmd` : restore/build reussis, fenetre lancee avec titre
  `Lumora 0.83.1-dev` et handle principal non nul.

**Version :** `0.83.1-dev`.
