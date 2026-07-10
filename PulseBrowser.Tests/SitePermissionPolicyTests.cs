using PulseBrowser.WinUI;
using Xunit;

namespace PulseBrowser.Tests;

public sealed class SitePermissionPolicyTests
{
    [Theory]
    [InlineData("Camera", "camera")]
    [InlineData("Microphone", "microphone")]
    [InlineData("Geolocation", "geolocation")]
    [InlineData("ClipboardRead", "clipboard-read")]
    [InlineData("MultipleAutomaticDownloads", "multiple-automatic-downloads")]
    [InlineData("FileReadWrite", "file-read-write")]
    public void NormalizeKind_MapsWebView2Names(string input, string expected)
    {
        Assert.Equal(expected, SitePermissionPolicy.NormalizeKind(input));
    }

    [Fact]
    public void SetState_ReplacesExistingRule()
    {
        var rules = new List<SitePermissionRule>();

        SitePermissionPolicy.SetState(rules, "Example.COM", "Camera", SitePermissionPolicy.Allow);
        SitePermissionPolicy.SetState(rules, "example.com", "camera", SitePermissionPolicy.Block);

        Assert.Single(rules);
        Assert.Equal("example.com", rules[0].RootDomain);
        Assert.Equal("camera", rules[0].Kind);
        Assert.Equal(SitePermissionPolicy.Block, rules[0].State);
        Assert.Equal(SitePermissionPolicy.Block, SitePermissionPolicy.StateFor(rules, "EXAMPLE.com", "Camera"));
    }

    [Fact]
    public void SetState_AskRemovesStoredRule()
    {
        var rules = new List<SitePermissionRule>();

        SitePermissionPolicy.SetState(rules, "example.com", "microphone", SitePermissionPolicy.Allow);
        SitePermissionPolicy.SetState(rules, "example.com", "Microphone", SitePermissionPolicy.Ask);

        Assert.Empty(rules);
        Assert.Equal(SitePermissionPolicy.Ask, SitePermissionPolicy.StateFor(rules, "example.com", "microphone"));
    }

    [Fact]
    public void StateFor_DefaultsToAskForUnknownSite()
    {
        var rules = new List<SitePermissionRule>
        {
            new() { RootDomain = "example.com", Kind = "notifications", State = SitePermissionPolicy.Block }
        };

        Assert.Equal(SitePermissionPolicy.Ask, SitePermissionPolicy.StateFor(rules, "openai.com", "notifications"));
    }
}
