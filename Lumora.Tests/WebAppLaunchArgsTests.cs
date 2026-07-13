using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public sealed class WebAppLaunchArgsTests
{
    [Fact]
    public void ParsesAppId_WhenPresent()
    {
        var id = WebAppLaunchArgs.TryParseAppId(new[] { "Lumora.WinUI.exe", "--app=abc123" });
        Assert.Equal("abc123", id);
    }

    [Fact]
    public void ReturnsNull_WhenAbsent()
    {
        var id = WebAppLaunchArgs.TryParseAppId(new[] { "Lumora.WinUI.exe" });
        Assert.Null(id);
    }

    [Fact]
    public void ReturnsNull_WhenEmptyValue()
    {
        var id = WebAppLaunchArgs.TryParseAppId(new[] { "--app=" });
        Assert.Null(id);
    }

    [Fact]
    public void IsCaseInsensitiveOnPrefix()
    {
        var id = WebAppLaunchArgs.TryParseAppId(new[] { "--APP=xyz" });
        Assert.Equal("xyz", id);
    }

    [Fact]
    public void TrimsQuotes()
    {
        var id = WebAppLaunchArgs.TryParseAppId(new[] { "--app=\"abc\"" });
        Assert.Equal("abc", id);
    }
}
