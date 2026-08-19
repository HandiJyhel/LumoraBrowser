using System.Text.Json.Nodes;
using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Premiers tests dedies a UiSettings (aucun n'existait avant : les seuls
// tests touchant ce fichier grepaient le texte source pour verifier
// l'alignement de version, cf. UsageModeVisualIdentityTests). Couvre le
// mecanisme de version de schema (SchemaVersion/MigrationSteps/ApplyMigrations)
// ajoute pour eviter qu'un futur renommage de propriete ne fasse
// silencieusement perdre TOUS les reglages (catch-all de Load()).
public class UiSettingsMigrationTests
{
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "LumoraTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Default_a_la_version_de_schema_courante()
    {
        var settings = UiSettings.Default();

        Assert.Equal(UiSettings.CurrentSchemaVersion, settings.SchemaVersion);
    }

    // Fonctions optionnelles du Coffre (0.93.48) : activées par défaut pour ne
    // rien changer au comportement existant tant que personne n'a rien
    // désactivé explicitement dans Paramètres > Coffre.
    [Fact]
    public void Default_active_les_sections_totp_et_passkey_du_coffre()
    {
        var settings = UiSettings.Default();

        Assert.True(settings.VaultTotpFeatureEnabled);
        Assert.True(settings.VaultPasskeyFeatureEnabled);
    }

    [Fact]
    public void Save_puis_Load_preserve_la_desactivation_des_sections_du_coffre()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora");
        var original = UiSettings.Default();
        original.VaultTotpFeatureEnabled = false;
        original.VaultPasskeyFeatureEnabled = false;
        original.Save(path);

        var reloaded = UiSettings.Load(path);

        Assert.False(reloaded.VaultTotpFeatureEnabled);
        Assert.False(reloaded.VaultPasskeyFeatureEnabled);
    }

    [Fact]
    public void Save_puis_Load_preserve_les_valeurs_et_la_version_de_schema()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora");
        var original = UiSettings.Default();
        original.SearchEngine = "bing";
        original.ThemeMode = "light";
        original.SessionTimeoutMinutes = 42;

        original.Save(path);
        var loaded = UiSettings.Load(path);

        Assert.Equal(UiSettings.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal("bing", loaded.SearchEngine);
        Assert.Equal("light", loaded.ThemeMode);
        Assert.Equal(42, loaded.SessionTimeoutMinutes);
    }

    [Fact]
    public void Un_fichier_legacy_sans_schemaversion_est_migre_sans_perte()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora"); // n'existe pas encore
        var legacyPath = Path.Combine(dir, "ui-settings.json");
        var legacyJson = """
            {
              "SearchEngine": "duckduckgo",
              "ThemeMode": "light",
              "SessionTimeoutMinutes": 30
            }
            """;
        File.WriteAllText(legacyPath, legacyJson);

        var loaded = UiSettings.Load(path, legacyPath);

        Assert.Equal(UiSettings.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal("duckduckgo", loaded.SearchEngine);
        Assert.Equal("light", loaded.ThemeMode);
        Assert.Equal(30, loaded.SessionTimeoutMinutes);
        Assert.False(File.Exists(legacyPath));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ApplyMigrations_enchaine_plusieurs_etapes_dans_lordre()
    {
        var steps = new Dictionary<int, Action<JsonObject>>
        {
            [0] = root => { if (root.Remove("AncienNomA", out var v)) root["NomIntermediaire"] = v; },
            [1] = root => { if (root.Remove("NomIntermediaire", out var v)) root["NomFinal"] = v; },
        };
        var root = new JsonObject { ["AncienNomA"] = "valeur-conservee" };

        var result = UiSettings.ApplyMigrations(root, fromVersion: 0, toVersion: 2, steps);

        Assert.Equal(2, (int)result["SchemaVersion"]!);
        Assert.Equal("valeur-conservee", (string)result["NomFinal"]!);
        Assert.False(result.ContainsKey("AncienNomA"));
        Assert.False(result.ContainsKey("NomIntermediaire"));
    }

    [Fact]
    public void ApplyMigrations_stampe_la_version_cible_meme_sans_etape_disponible()
    {
        var steps = new Dictionary<int, Action<JsonObject>>(); // aucune etape enregistree
        var root = new JsonObject { ["Quelconque"] = "valeur" };

        var result = UiSettings.ApplyMigrations(root, fromVersion: 0, toVersion: 3, steps);

        Assert.Equal(3, (int)result["SchemaVersion"]!);
        Assert.Equal("valeur", (string)result["Quelconque"]!);
    }

    [Fact]
    public void Load_retombe_sur_Default_si_le_fichier_est_illisible()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora");
        File.WriteAllText(path, "ceci n'est pas du JSON valide { { {");

        var loaded = UiSettings.Load(path);

        Assert.Equal(UiSettings.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal(UiSettings.Default().SearchEngine, loaded.SearchEngine);
    }

    [Fact]
    public void Default_seme_les_5_tuiles_epinglees_du_menu_demarrer()
    {
        var settings = UiSettings.Default();

        Assert.Equal(UiSettings.DefaultPinnedStartMenuTileIds, settings.PinnedStartMenuTileIds);
    }

    [Fact]
    public void PinnedStartMenuTileIds_absent_du_json_conserve_le_defaut_sans_migration_dediee()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora"); // n'existe pas encore
        var legacyPath = Path.Combine(dir, "ui-settings.json");
        // JSON d'un profil anterieur a cette fonctionnalite : pas de
        // PinnedStartMenuTileIds du tout.
        File.WriteAllText(legacyPath, """{ "SearchEngine": "duckduckgo" }""");

        var loaded = UiSettings.Load(path, legacyPath);

        Assert.Equal(UiSettings.DefaultPinnedStartMenuTileIds, loaded.PinnedStartMenuTileIds);
    }

    [Fact]
    public void PinnedStartMenuTileIds_explicitement_vide_reste_vide()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora");
        // L'utilisateur a desepingle toutes les tuiles : la liste vide doit
        // survivre a un aller-retour, pas revenir aux 5 valeurs par defaut.
        var original = UiSettings.Default();
        original.PinnedStartMenuTileIds.Clear();

        original.Save(path);
        var loaded = UiSettings.Load(path);

        Assert.Empty(loaded.PinnedStartMenuTileIds);
    }

    [Fact]
    public void StartMenuTileUsage_survit_a_un_aller_retour_SaveLoad()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora");
        var original = UiSettings.Default();
        var now = DateTimeOffset.UtcNow;
        original.StartMenuTileUsage.Add(new StartMenuTileUsage(StartMenuTileIds.History, 3, now));

        original.Save(path);
        var loaded = UiSettings.Load(path);

        var entry = Assert.Single(loaded.StartMenuTileUsage);
        Assert.Equal(StartMenuTileIds.History, entry.TileId);
        Assert.Equal(3, entry.OpenCount);
        Assert.Equal(now, entry.LastOpenedAt);
    }

    [Fact]
    public void ModeAccentColors_absents_du_json_conservent_le_defaut_sans_migration_dediee()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora"); // n'existe pas encore
        var legacyPath = Path.Combine(dir, "ui-settings.json");
        // JSON d'un profil anterieur a cette fonctionnalite : aucun des
        // nouveaux champs n'existe du tout.
        File.WriteAllText(legacyPath, """{ "SearchEngine": "duckduckgo" }""");

        var loaded = UiSettings.Load(path, legacyPath);

        Assert.Equal(string.Empty, loaded.ModeAccentColorNeutral);
        Assert.Equal(string.Empty, loaded.ModeAccentColorFocus);
        Assert.Equal(string.Empty, loaded.ModeAccentColorReading);
        Assert.Equal(string.Empty, loaded.ModeAccentColorCreative);
        Assert.Equal(string.Empty, loaded.ModeAccentColorResearch);
        Assert.Equal(string.Empty, loaded.ModeAccentColorNight);
    }

    [Fact]
    public void ModeAccentColorFocus_survit_a_un_aller_retour_SaveLoad()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora");
        var original = UiSettings.Default();
        original.ModeAccentColorFocus = "#3A8FD1";

        original.Save(path);
        var loaded = UiSettings.Load(path);

        Assert.Equal("#3A8FD1", loaded.ModeAccentColorFocus);
        // Les modes non touches restent au defaut Nova (vide).
        Assert.Equal(string.Empty, loaded.ModeAccentColorNight);
    }

    // Suppression du Style Lumora (2026-08-10, demande explicite utilisateur -
    // "trop difficile a gerer") : ChromeLayoutStyle/IdentitySpineAutoHide ont
    // ete retires de UiSettings. Un profil existant dont le JSON contient
    // encore "ChromeLayoutStyle":"identitySpine" n'a plus besoin de migration
    // dediee - System.Text.Json ignore silencieusement les cles inconnues au
    // chargement, le profil retombe simplement en disposition classique
    // (la seule qui reste).
    [Fact]
    public void ChromeLayoutStyle_residuel_dans_un_fichier_existant_ne_fait_pas_echouer_le_chargement()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora");
        // Meme format que Save() (LumoraFile, pas du JSON en clair) : simule
        // un vrai profil existant sauvegarde avant la suppression du Style
        // Lumora, avec la cle residuelle encore presente.
        LumoraFile.WriteAllText(path, """{ "ChromeLayoutStyle": "identitySpine", "IdentitySpineAutoHide": true, "SearchEngine": "duckduckgo" }""");

        var loaded = UiSettings.Load(path);

        Assert.Equal("duckduckgo", loaded.SearchEngine);
    }

    // Taille de l'interface (UiDensity) : le nouveau defaut "standard" est
    // un choix produit deliberement change pour TOUS les profils, y compris
    // les profils existants (contrairement au reste des reglages
    // "Disposition" qui restent silencieusement sur leur ancienne valeur) -
    // verrouille explicitement ce defaut plutot que de se fier au defaut
    // implicite de Default().
    [Fact]
    public void UiSettings_Default_a_pour_UiDensity_standard()
    {
        Assert.Equal("standard", UiSettings.Default().UiDensity);
    }

    [Fact]
    public void UiDensity_absent_du_json_bascule_sur_standard_par_defaut()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora");
        var legacyPath = Path.Combine(dir, "ui-settings.json");
        File.WriteAllText(legacyPath, """{ "SearchEngine": "duckduckgo" }""");

        var loaded = UiSettings.Load(path, legacyPath);

        Assert.Equal("standard", loaded.UiDensity);
    }

    [Fact]
    public void UiDensity_survit_a_un_aller_retour_SaveLoad()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora");
        var original = UiSettings.Default();
        original.UiDensity = "dense";

        original.Save(path);
        var loaded = UiSettings.Load(path);

        Assert.Equal("dense", loaded.UiDensity);
    }

    // SessionPurgeEnabled bascule de true a false par defaut (2026-08-13,
    // retour utilisateur : "trop compliqué, trop long" pour un utilisateur de
    // base) - les sessions restent connectees par defaut desormais, la purge
    // au demarrage devient une option explicite. Meme principe de
    // verrouillage que UiDensity plus haut.
    [Fact]
    public void UiSettings_Default_a_pour_SessionPurgeEnabled_false()
    {
        Assert.False(UiSettings.Default().SessionPurgeEnabled);
    }

    [Fact]
    public void SessionPurgeEnabled_true_herite_dun_profil_pre_migration_bascule_a_false()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora");
        // Simule un profil enregistre avant ce changement : SchemaVersion 1,
        // SessionPurgeEnabled explicitement true (l'ancien defaut - Save()
        // serialise toutes les proprietes, jamais seulement celles modifiees).
        LumoraFile.WriteAllText(path, """{ "SchemaVersion": 1, "SessionPurgeEnabled": true, "SearchEngine": "duckduckgo" }""");

        var loaded = UiSettings.Load(path);

        Assert.False(loaded.SessionPurgeEnabled);
        Assert.Equal(UiSettings.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal("duckduckgo", loaded.SearchEngine);
    }

    [Fact]
    public void SessionPurgeEnabled_reactive_explicitement_survit_a_un_nouvel_aller_retour()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora");
        var original = UiSettings.Default();
        // L'utilisateur active volontairement la purge (deja a la version de
        // schema courante en enregistrant) : ce choix ne doit plus jamais
        // etre re-bascule par la migration v1->v2 (elle ne s'applique qu'aux
        // fichiers encore a l'ancienne version de schema).
        original.SessionPurgeEnabled = true;

        original.Save(path);
        var loaded = UiSettings.Load(path);

        Assert.True(loaded.SessionPurgeEnabled);
    }
}
