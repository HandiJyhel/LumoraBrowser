using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public sealed class WebAppIdentityTests
{
    [Fact]
    public void AppUserModelId_EmbedsAppId()
    {
        Assert.Equal("Lumora.WebApp.abc123", WebAppIdentity.AppUserModelId("abc123"));
    }

    [Fact]
    public void AppUserModelId_DiffersPerApp()
    {
        var first = WebAppIdentity.AppUserModelId("aaa");
        var second = WebAppIdentity.AppUserModelId("bbb");
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void AppUserModelId_NeverCollidesWithBrowser()
    {
        // Le préfixe distinct garantit qu'aucun identifiant d'application web ne
        // peut jamais reproduire l'identifiant du navigateur principal, quelle
        // que soit la valeur (même vide ou pathologique) de l'id d'application.
        Assert.NotEqual(WebAppIdentity.BrowserAppUserModelId, WebAppIdentity.AppUserModelId(""));
        Assert.NotEqual(WebAppIdentity.BrowserAppUserModelId, WebAppIdentity.AppUserModelId("Browser"));
    }
}
