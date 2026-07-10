using PulseBrowser.WinUI;
using Xunit;

namespace PulseBrowser.Tests;

public class ProfilePathsTests
{
    [Fact]
    public void Default_utilise_le_profil_isole_depuis_l_environnement()
    {
        var previous = Environment.GetEnvironmentVariable(PulseProfilePaths.ProfileDirectoryEnvironmentVariable);
        var dir = Path.Combine(Path.GetTempPath(), "PulseBrowserTests", Guid.NewGuid().ToString("N"), "profile");

        try
        {
            Environment.SetEnvironmentVariable(PulseProfilePaths.ProfileDirectoryEnvironmentVariable, dir);

            var paths = PulseProfilePaths.Default();

            Assert.Equal(Path.GetFullPath(dir), paths.ProfileDir);
            Assert.StartsWith(paths.ProfileDir, paths.BrowserDataDir);
        }
        finally
        {
            Environment.SetEnvironmentVariable(PulseProfilePaths.ProfileDirectoryEnvironmentVariable, previous);
        }
    }
}
