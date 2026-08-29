using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public sealed class UrlLaunchArgsTests
{
    [Fact]
    public void ParsesHttpsUrl_WhenPresent()
    {
        var url = UrlLaunchArgs.TryParseUrl(new[] { "Lumora.WinUI.exe", "https://example.com/page" });
        Assert.Equal("https://example.com/page", url);
    }

    [Fact]
    public void ParsesHttpUrl_WhenPresent()
    {
        var url = UrlLaunchArgs.TryParseUrl(new[] { "Lumora.WinUI.exe", "http://example.com/" });
        Assert.Equal("http://example.com/", url);
    }

    [Fact]
    public void ReturnsNull_WhenAbsent()
    {
        var url = UrlLaunchArgs.TryParseUrl(new[] { "Lumora.WinUI.exe", "--guest" });
        Assert.Null(url);
    }

    [Fact]
    public void TrimsQuotes()
    {
        var url = UrlLaunchArgs.TryParseUrl(new[] { "\"https://example.com/\"" });
        Assert.Equal("https://example.com/", url);
    }

    [Fact]
    public void IgnoresNonUrlFlags()
    {
        var url = UrlLaunchArgs.TryParseUrl(new[] { "Lumora.WinUI.exe", "--app=abc123", "https://example.com/x" });
        Assert.Equal("https://example.com/x", url);
    }

    // Variante "chaîne unique" (relance redirigée, args.Data.Arguments) - voir
    // App.xaml.cs/TryExtractLaunchUrl.
    [Fact]
    public void RawArguments_ParsesQuotedUrl()
    {
        var url = UrlLaunchArgs.TryParseUrl("\"https://example.com/lumora-default-browser-test\"");
        Assert.Equal("https://example.com/lumora-default-browser-test", url);
    }

    [Fact]
    public void RawArguments_ReturnsNull_WhenEmpty()
    {
        Assert.Null(UrlLaunchArgs.TryParseUrl((string?)null));
        Assert.Null(UrlLaunchArgs.TryParseUrl(""));
        Assert.Null(UrlLaunchArgs.TryParseUrl("   "));
    }

    [Fact]
    public void RawArguments_ReturnsNull_WhenNoUrlToken()
    {
        var url = UrlLaunchArgs.TryParseUrl("--guest");
        Assert.Null(url);
    }
}
