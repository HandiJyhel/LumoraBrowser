using Lumora.Privacy;
using Xunit;

namespace Lumora.Tests;

public class PrivacyEngineTests
{
    [Fact]
    public void ShouldBlock_enregistre_un_blocage_recent_sans_parametres_url()
    {
        var engine = new PrivacyEngine();
        engine.Register(new TestPrivacyModule());

        var blocked = engine.ShouldBlock(
            "https://accounts.example.com/oauth/authorize?token=secret",
            "https://example.com/login");

        Assert.True(blocked);
        var entry = Assert.Single(engine.RecentPageBlocks);
        Assert.Equal("test-module", entry.ModuleId);
        Assert.Equal("accounts.example.com", entry.RequestHost);
        Assert.Equal("/oauth/authorize", entry.RequestPath);
        Assert.DoesNotContain("secret", entry.RequestPath);
    }

    [Fact]
    public void ResetPageBlockedCount_vide_uniquement_la_page_courante()
    {
        var engine = new PrivacyEngine();
        engine.Register(new TestPrivacyModule());

        engine.ShouldBlock("https://tracker.example.com/pixel", "https://example.com/");

        engine.ResetPageBlockedCount();

        Assert.Equal(0, engine.PageBlockedCount);
        Assert.Empty(engine.RecentPageBlocks);
        Assert.Single(engine.RecentBlocks);
    }

    private sealed class TestPrivacyModule : IPrivacyModule
    {
        public string Id => "test-module";
        public string DisplayName => "Module test";
        public bool IsEnabled { get; set; } = true;

        public bool ShouldBlock(string requestUri, string pageUri) => true;
    }
}
