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
}
