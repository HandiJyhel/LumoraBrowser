using PulseBrowser.WinUI;
using Xunit;

namespace PulseBrowser.Tests;

public sealed class WebAppUrlPolicyTests
{
    [Fact]
    public void WithinScope_ForSameRootDomain()
    {
        Assert.True(WebAppUrlPolicy.IsWithinAppScope("google.com", "https://mail.google.com/inbox"));
    }

    [Fact]
    public void OutOfScope_ForDifferentRootDomain()
    {
        Assert.False(WebAppUrlPolicy.IsWithinAppScope("google.com", "https://example.com/page"));
    }

    [Fact]
    public void WithinScope_ForInternalScheme()
    {
        Assert.True(WebAppUrlPolicy.IsWithinAppScope("google.com", "pulse://accueil"));
    }

    [Fact]
    public void WithinScope_WhenAppRootDomainMissing()
    {
        Assert.True(WebAppUrlPolicy.IsWithinAppScope("", "https://example.com"));
    }

    [Fact]
    public void OutOfScope_IsCaseInsensitive()
    {
        Assert.True(WebAppUrlPolicy.IsWithinAppScope("GOOGLE.com", "https://accounts.google.com/signin"));
    }
}
