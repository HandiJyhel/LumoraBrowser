using PulseBrowser.WinUI;
using Xunit;

namespace PulseBrowser.Tests;

public class BuildAuthenticityTests
{
    [Fact]
    public void Load_retourne_des_valeurs_honnetes_sans_fichier()
    {
        var dir = Path.Combine(Path.GetTempPath(), "PulseBrowserTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var info = BuildAuthenticity.Load(dir, "0.52.0-dev");

        Assert.Equal("0.52.0-dev", info.Version);
        Assert.Equal(BuildAuthenticity.MissingValue, info.Sha256);
        Assert.Equal(BuildAuthenticity.UnsignedSigstoreValue, info.Sigstore);
        Assert.Equal(BuildAuthenticity.UnsignedWindowsValue, info.WindowsSignature);
    }

    [Fact]
    public void Load_lit_le_fichier_verification()
    {
        var dir = Path.Combine(Path.GetTempPath(), "PulseBrowserTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, BuildAuthenticity.VerificationFileName), """
Version: 0.52.0-dev
Canal: Build propre de test
SHA256: abc123
Signature Sigstore: Non signee pour ce build
Signature Windows: Non signee Authenticode
Profil: Profil vierge isole
""");

        var info = BuildAuthenticity.Load(dir, "fallback");

        Assert.Equal("0.52.0-dev", info.Version);
        Assert.Equal("Build propre de test", info.Channel);
        Assert.Equal("abc123", info.Sha256);
        Assert.Equal("Profil vierge isole", info.ProfileMode);
    }
}
