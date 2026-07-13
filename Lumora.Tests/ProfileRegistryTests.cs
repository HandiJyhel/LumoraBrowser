using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

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
}
