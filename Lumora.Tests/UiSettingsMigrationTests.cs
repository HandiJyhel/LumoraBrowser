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
    public void ModeAccentColors_et_ChromeLayoutStyle_absents_du_json_conservent_le_defaut_sans_migration_dediee()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora"); // n'existe pas encore
        var legacyPath = Path.Combine(dir, "ui-settings.json");
        // JSON d'un profil anterieur a cette fonctionnalite : aucun des
        // nouveaux champs n'existe du tout.
        File.WriteAllText(legacyPath, """{ "SearchEngine": "duckduckgo" }""");

        var loaded = UiSettings.Load(path, legacyPath);

        Assert.Equal("classic", loaded.ChromeLayoutStyle);
        Assert.Equal(string.Empty, loaded.ModeAccentColorNeutral);
        Assert.Equal(string.Empty, loaded.ModeAccentColorFocus);
        Assert.Equal(string.Empty, loaded.ModeAccentColorReading);
        Assert.Equal(string.Empty, loaded.ModeAccentColorCreative);
        Assert.Equal(string.Empty, loaded.ModeAccentColorResearch);
        Assert.Equal(string.Empty, loaded.ModeAccentColorNight);
    }

    [Fact]
    public void ModeAccentColorFocus_et_ChromeLayoutStyle_survivent_a_un_aller_retour_SaveLoad()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora");
        var original = UiSettings.Default();
        original.ModeAccentColorFocus = "#3A8FD1";
        original.ChromeLayoutStyle = "identitySpine";

        original.Save(path);
        var loaded = UiSettings.Load(path);

        Assert.Equal("#3A8FD1", loaded.ModeAccentColorFocus);
        Assert.Equal("identitySpine", loaded.ChromeLayoutStyle);
        // Les modes non touches restent au defaut Nova (vide).
        Assert.Equal(string.Empty, loaded.ModeAccentColorNight);
    }

    [Fact]
    public void IdentitySpineAutoHide_absent_du_json_reste_desactive_par_defaut()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora");
        var legacyPath = Path.Combine(dir, "ui-settings.json");
        File.WriteAllText(legacyPath, """{ "SearchEngine": "duckduckgo" }""");

        var loaded = UiSettings.Load(path, legacyPath);

        Assert.False(loaded.IdentitySpineAutoHide);
    }

    [Fact]
    public void IdentitySpineAutoHide_survit_a_un_aller_retour_SaveLoad()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "ui-settings.lumora");
        var original = UiSettings.Default();
        original.IdentitySpineAutoHide = true;

        original.Save(path);
        var loaded = UiSettings.Load(path);

        Assert.True(loaded.IdentitySpineAutoHide);
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
}
