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
        Assert.Equal("example.com", entry.PageRootDomain);
        Assert.Equal(PrivacyBlockCategory.Other, entry.Category);
        Assert.Equal(1, engine.PageCounters.Other);
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
        Assert.Equal(0, engine.PageCounters.Total);
        Assert.Empty(engine.RecentPageBlocks);
        Assert.Single(engine.RecentBlocks);

        var stats = engine.SiteStatsFor("example.com");
        Assert.Equal(1, stats.Total);
        Assert.Equal(1, stats.Trackers);
    }

    [Fact]
    public void Compteurs_distinguent_pubs_trackers_et_site()
    {
        var engine = new PrivacyEngine();
        engine.Register(new TestPrivacyModule("network-blocker", "Bloqueur réseau"));
        engine.Register(new TestPrivacyModule("telemetry-blocker", "Anti-télémétrie"));

        engine.ShouldBlock("https://ads.doubleclick.net/ad.js", "https://www.exemple.fr/article");
        engine.ShouldBlock("https://api.mixpanel.com/track", "https://exemple.fr/article");

        Assert.Equal(new PrivacyBlockCounters(2, 1, 1, 0), engine.PageCounters);
        Assert.Equal(new PrivacyBlockCounters(2, 1, 1, 0), engine.GlobalCounters);

        var stats = engine.SiteStatsFor("https://www.exemple.fr/");
        Assert.Equal("exemple.fr", stats.RootDomain);
        Assert.Equal(2, stats.Total);
        Assert.Equal(1, stats.Ads);
        Assert.Equal(1, stats.Trackers);
        Assert.Equal(2, stats.RecentBlocks.Count);
    }

    private sealed class TestPrivacyModule : IPrivacyModule
    {
        public TestPrivacyModule(string id = "test-module", string displayName = "Module test")
        {
            Id = id;
            DisplayName = displayName;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public bool IsEnabled { get; set; } = true;

        public bool ShouldBlock(string requestUri, string pageUri) => true;
    }
}
