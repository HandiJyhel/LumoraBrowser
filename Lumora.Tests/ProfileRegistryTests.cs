using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

[Collection(ProfileDirEnvironmentCollection.Name)]
public sealed class ProfileRegistryTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "nova_profiles_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void QuarantineProfile_deplace_un_profil_non_actif_sans_detruire_son_contenu()
    {
        var profileDir = Path.Combine(_root, "profiles", "alice");
        Directory.CreateDirectory(profileDir);
        File.WriteAllText(Path.Combine(profileDir, "profile.nova"), "contenu");

        var entry = new LumoraProfileEntry(
            "alice",
            "Alice",
            profileDir,
            IsActive: false,
            IsCustom: false);

        var quarantineDir = LumoraProfileRegistry.QuarantineProfile(entry);

        Assert.False(Directory.Exists(profileDir));
        Assert.True(Directory.Exists(quarantineDir));
        Assert.Equal("contenu", File.ReadAllText(Path.Combine(quarantineDir, "profile.nova")));
        Assert.Contains(".novabrowser-profile-quarantine", quarantineDir);
    }

    [Fact]
    public void QuarantineProfile_refuse_le_profil_actif()
    {
        var profileDir = Path.Combine(_root, "profiles", "active");
        Directory.CreateDirectory(profileDir);

        var entry = new LumoraProfileEntry(
            "active",
            "Active",
            profileDir,
            IsActive: true,
            IsCustom: false);

        Assert.Throws<InvalidOperationException>(() => LumoraProfileRegistry.QuarantineProfile(entry));
        Assert.True(Directory.Exists(profileDir));
    }

    [Fact]
    public void Discover_ne_marque_pas_un_profil_custom_actif_si_le_runtime_utilise_un_autre_dossier()
    {
        var customDir = Path.Combine(_root, "custom-profile");
        var runtimeDir = Path.Combine(_root, "runtime-profile");
        var customPaths = LumoraProfilePaths.FromDirectory(customDir);
        UserProfile.Create("Alice", "mot-de-passe-solide", pin: null).Save(customPaths.ProfileFile);

        var config = new LumoraConfig { CustomProfilePath = customDir };

        var entries = LumoraProfileRegistry.Discover(config, activeProfileDir: runtimeDir);
        var custom = entries.Single(entry => entry.ProfileDir == customDir);

        Assert.False(custom.IsActive);
    }

    [Fact]
    public void Discover_marque_un_profil_custom_actif_quand_le_runtime_utilise_ce_dossier()
    {
        var customDir = Path.Combine(_root, "custom-profile");
        var customPaths = LumoraProfilePaths.FromDirectory(customDir);
        UserProfile.Create("Alice", "mot-de-passe-solide", pin: null).Save(customPaths.ProfileFile);

        var config = new LumoraConfig { CustomProfilePath = customDir };

        var entries = LumoraProfileRegistry.Discover(config, activeProfileDir: customDir);
        var custom = entries.Single(entry => entry.ProfileDir == customDir);

        Assert.True(custom.IsActive);
    }

    // 2026-08-14 : incident reel corrige (voir MEMORY.md) - Discover scannait
    // TOUJOURS le vrai %LOCALAPPDATA%\Lumora\profiles de la machine, meme sous
    // LUMORA_PROFILE_DIR, exposant les vrais profils de l'utilisateur dans un
    // lancement de verification/invite cense en etre isole. Verifie que seul
    // le profil du dossier isole lui-meme est retourne, jamais les vrais.
    [Fact]
    public void Discover_sous_isolation_ne_retourne_que_le_profil_isole_lui_meme()
    {
        var previous = Environment.GetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable);
        var isolatedDir = Path.Combine(_root, "isolated-active-profile");

        try
        {
            Environment.SetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable, isolatedDir);

            var isolatedPaths = LumoraProfilePaths.FromDirectory(isolatedDir);
            UserProfile.Create("Test", "mot-de-passe-isole", pin: null).Save(isolatedPaths.ProfileFile);

            // Un "vrai" profil ailleurs (simule le reste de la machine, HORS
            // du dossier isole) ne doit jamais apparaitre dans la decouverte.
            var elsewhereDir = Path.Combine(_root, "profil-reel-hors-isolation");
            var elsewherePaths = LumoraProfilePaths.FromDirectory(elsewhereDir);
            UserProfile.Create("VraiUtilisateur", "mot-de-passe-reel", pin: null).Save(elsewherePaths.ProfileFile);

            var entries = LumoraProfileRegistry.Discover(new LumoraConfig());

            var entry = Assert.Single(entries);
            Assert.Equal("Test", entry.Name);
            Assert.True(entry.IsActive);
            Assert.DoesNotContain(entries, e => e.Name == "VraiUtilisateur");
        }
        finally
        {
            Environment.SetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable, previous);
        }
    }

    [Fact]
    public void CreateProfileId_sous_isolation_ne_boucle_pas_a_l_infini()
    {
        var previous = Environment.GetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable);
        var isolatedDir = Path.Combine(_root, "isolated-createid");

        try
        {
            Environment.SetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable, isolatedDir);
            // ForProfileId(any) retourne le meme dossier isole a plat sous
            // cette variable (meme convention que Default()) : meme si ce
            // dossier existe deja, CreateProfileId ne doit pas boucler.
            Directory.CreateDirectory(isolatedDir);

            var id = LumoraProfileRegistry.CreateProfileId("Alice");

            Assert.Equal("alice", id);
        }
        finally
        {
            Environment.SetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable, previous);
        }
    }
}
