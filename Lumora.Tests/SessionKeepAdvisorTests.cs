using Lumora.WinUI.Sessions;
using Xunit;

namespace Lumora.Tests;

public sealed class SessionKeepAdvisorTests
{
    [Fact]
    public void DoesNotOffer_WhenPurgeDisabled()
    {
        var result = SessionKeepAdvisor.ShouldOfferKeepSession(
            sessionPurgeEnabled: false,
            trustedSites: new List<string>(),
            declinedSites: new List<string>(),
            rootDomain: "google.com");

        Assert.False(result);
    }

    [Fact]
    public void Offers_WhenPurgeEnabledAndSiteUnknown()
    {
        var result = SessionKeepAdvisor.ShouldOfferKeepSession(
            sessionPurgeEnabled: true,
            trustedSites: new List<string>(),
            declinedSites: new List<string>(),
            rootDomain: "google.com");

        Assert.True(result);
    }

    [Fact]
    public void DoesNotOffer_WhenSiteAlreadyTrusted()
    {
        var result = SessionKeepAdvisor.ShouldOfferKeepSession(
            sessionPurgeEnabled: true,
            trustedSites: new List<string> { "google.com" },
            declinedSites: new List<string>(),
            rootDomain: "GOOGLE.com");

        Assert.False(result);
    }

    [Fact]
    public void DoesNotOffer_WhenUserAlreadyDeclined()
    {
        var result = SessionKeepAdvisor.ShouldOfferKeepSession(
            sessionPurgeEnabled: true,
            trustedSites: new List<string>(),
            declinedSites: new List<string> { "google.com" },
            rootDomain: "google.com");

        Assert.False(result);
    }

    [Fact]
    public void DoesNotOffer_WhenRootDomainMissing()
    {
        var result = SessionKeepAdvisor.ShouldOfferKeepSession(
            sessionPurgeEnabled: true,
            trustedSites: new List<string>(),
            declinedSites: new List<string>(),
            rootDomain: "");

        Assert.False(result);
    }
}
