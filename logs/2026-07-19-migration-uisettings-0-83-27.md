# 2026-07-19 - Versionnement de schema pour UiSettings (0.83.27-dev)

Point 4 de l'audit initial. Choisi avant le point 3 (restructuration des
god files de `MainWindow`, dont `Settings.cs`) a la demande de
l'utilisateur : avoir un filet de securite sur les migrations avant de
toucher a la structure du code qui gere ces reglages, pour qu'un
renommage pendant la restructuration ne casse pas silencieusement une
desserialisation existante.

`UiSettings.cs` (~65-70 proprietes) n'avait aucune version de schema.
Les migrations existantes (`ApplyLegacyBrandingMigration`,
`ApplyPinnedModulesMigration`, `RemoveLegacyDefaultShortcuts`)
fonctionnent par sniffing au cas par cas du JSON brut - efficace mais
sans registre centralise. Le vrai risque identifie : une propriete
renommee/retypee de facon incompatible peut faire lever une exception a
`JsonSerializer.Deserialize<UiSettings>`, remontant au `catch (Exception)`
global de `Load()` - qui reinitialise ALORS TOUT l'objet a `Default()`,
pas seulement la propriete concernee.

- Version passee a `0.83.27-dev`.
- `Lumora.WinUI/Models/UiSettings.cs` : nouvelle propriete `SchemaVersion`
  (+ `const int CurrentSchemaVersion = 1`, version de reference qui
  introduit ce mecanisme - aucun renommage a migrer aujourd'hui).
- Table `MigrationSteps` (cle = version source, valeur = action sur le
  `JsonObject` brut) et boucle generique `ApplyMigrations` (internal,
  testable directement avec une table synthetique). Vide aujourd'hui :
  prete a recevoir une entree le jour ou une propriete sera renommee.
- `MigrateJson` (parse le JSON en `JsonObject`, lit `SchemaVersion` -
  absent = version 0 -, applique `ApplyMigrations`, reserialise) inseree
  dans `Load()` juste avant chaque `JsonSerializer.Deserialize`, dans les
  deux branches (fichier courant et fallback `.json` legacy). Les trois
  migrations existantes continuent de s'executer apres, inchangees.
- `Save()` stampe desormais `SchemaVersion = CurrentSchemaVersion` avant
  de serialiser - ecriture en memoire, gratuite, coherente avec les ~18
  points d'appel de `Save` sans debounce (pas de detection de changement
  de version necessaire, on ecrit toujours la version courante).
- Nettoyage prealable indispensable (pas une simple preference de
  style) : `UiSettings.cs` importait `System.Net`,
  `System.Security.Cryptography`, `System.Text`, `Microsoft.UI.Xaml`,
  `Microsoft.Web.WebView2.Core` sans qu'aucun ne soit utilise dans le
  fichier (verifie par recherche - zero reference reelle). Ces `using`
  auraient bloque la compilation du fichier dans `Lumora.Tests` (qui ne
  reference pas WindowsAppSDK/WebView2) : supprimes.
- `UiSettings.cs` ajoute au `Compile Include` de `Lumora.Tests.csproj` -
  premiers tests dedies a cette classe (`UsageModeVisualIdentityTests.cs`
  ne fait que grep le texte source, aucune couverture comportementale
  n'existait). Toutes ses dependances (`WinUiRuntimeTrace.cs`,
  `LumoraFile.cs`, `SitePermissionPolicy.cs`, `BrandingText.cs`) etaient
  deja compilees dans ce projet.
- Nouveaux tests `Lumora.Tests/UiSettingsMigrationTests.cs` : version de
  schema par defaut, aller-retour Save/Load, migration d'un JSON legacy
  sans `SchemaVersion` sans perte de donnees, chainage multi-etapes de
  `ApplyMigrations` avec une table synthetique (renommage successif de
  deux cles), stampe de version meme sans etape disponible, JSON illisible
  retombe sur `Default()` sans exception.
- Mise a jour du test de garde-fou d'alignement de version vers
  `0.83.27-dev` (`Lumora.Tests/UsageModeVisualIdentityTests.cs`,
  `AGENTS.md`, `scripts/build-clean-test-artifact.ps1`,
  `scripts/build-installer.ps1`).
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora en dehors de la verification.

**Verification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 534/534
  tests reussis (528 precedents + 6 nouveaux pour la migration de
  `UiSettings`).
- Build WinUI MSBuild x64 (Debug) : 0 avertissement, 0 erreur.
- Verification en conditions reelles, sur une COPIE du profil reel de
  l'utilisateur (dossier `navigation/` + `profile.pulse`/`vault.pulse` ;
  `webview2/` volontairement exclu de la copie, ~650 Mo sans rapport avec
  ce test) :
  - Lancement de `MainWindow` sur la copie via `LUMORA_PROFILE_DIR` :
    ecran de verrouillage par code PIN affiche ("Bonjour, H.J.") - attendu,
    mais bloquant pour une verification automatisee sans les identifiants
    de l'utilisateur. Fenetre fermee, verification poursuivie directement
    au niveau du code (`UiSettings.Load`/`Save`), plus precis pour ce test
    specifique et sans besoin d'identifiants.
  - Test temporaire (non commite, supprime apres usage) chargeant le vrai
    fichier `ui-settings.pulse` copie via `UiSettings.Load` : valeurs
    reelles et personnalisees confirmees (`ThemeMode=system`,
    `SearchEngine=google`, `StartupMode=home`, `WindowTransparency=77`,
    `SessionTimeoutMinutes=0`, `PinnedModuleIds` avec modules personnalises
    dont `detachVideo`) - preuve que la migration s'applique bien sur des
    donnees reelles, pas sur des valeurs par defaut. `SchemaVersion == 1`
    apres chargement.
  - Modification de `WindowTransparency` (77 -> 55), `Save`, rechargement :
    nouvelle valeur persistee, `SchemaVersion` toujours a jour, et TOUTES
    les autres valeurs reelles (theme, moteur de recherche, mode de
    demarrage, mode d'usage, modules epingles, nombre de raccourcis)
    identiques avant/apres - aucune perte de donnees sur un vrai profil a
    ~65 proprietes.
  - Nettoyage : fichier de test temporaire et copie de profil supprimes
    apres verification ; `dotnet test` complet re-execute pour confirmer
    534/534 apres suppression.
  - Cas `Default()` (profil neuf) et migration depuis `ui-settings.json`
    legacy deja couverts de facon deterministe par les tests automatises
    ci-dessus, pas re-testes manuellement en plus.

**Version :** `0.83.27-dev`.
