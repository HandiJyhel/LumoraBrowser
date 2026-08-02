using Lumora.WinUI.Sessions;
using Xunit;

namespace Lumora.Tests;

public sealed class SessionDomainFamiliesTests
{
    [Fact]
    public void GoogleTrustExtendsToYoutube()
    {
        var siblings = SessionDomainFamilies.SiblingsOf("google.com");

        Assert.Contains("youtube.com", siblings);
    }

    [Fact]
    public void YoutubeTrustExtendsToGoogle()
    {
        var siblings = SessionDomainFamilies.SiblingsOf("youtube.com");

        Assert.Contains("google.com", siblings);
    }

    [Fact]
    public void UnrelatedDomainHasNoSiblings()
    {
        var siblings = SessionDomainFamilies.SiblingsOf("exemple.fr");

        Assert.Empty(siblings);
    }
}
