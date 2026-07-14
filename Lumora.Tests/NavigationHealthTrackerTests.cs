using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Verrouille le comportement du suivi de navigation extrait de MainWindow en
// 0.78.1 : ces tests fixent les décisions AVANT/APRÈS le refactor, pour garantir
// zéro régression du cluster « santé de navigation ».
public class NavigationHealthTrackerTests
{
    // ── Suivi du document principal ─────────────────────────────────────────

    [Fact]
    public void TrackNavigationStart_IdentifieLeDocumentPrincipal()
    {
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(7, "https://site.example/", isRedirect: false);

        Assert.True(t.IsMainDocument("https://site.example/", out var tab));
        Assert.Equal(7, tab);
        Assert.False(t.IsMainDocument("https://autre.example/", out _));
    }

    [Fact]
    public void CleUri_IgnoreLeSlashFinal()
    {
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(1, "https://site.example/", isRedirect: false);

        Assert.True(t.IsMainDocument("https://site.example", out _));
    }

    [Fact]
    public void Redirection_ConserveLesUrisPrecedentes()
    {
        // Un saut de redirection prolonge la navigation : les deux URIs restent
        // reconnues comme document principal (l'ordre des réponses n'est pas garanti).
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(3, "https://old.example/", isRedirect: false);
        t.TrackNavigationStart(3, "https://new.example/", isRedirect: true);

        Assert.True(t.IsMainDocument("https://old.example/", out _));
        Assert.True(t.IsMainDocument("https://new.example/", out _));
    }

    [Fact]
    public void NouvelleNavigation_OublieLancienneUri()
    {
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(3, "https://old.example/", isRedirect: false);
        t.TrackNavigationStart(3, "https://new.example/", isRedirect: false);

        Assert.False(t.IsMainDocument("https://old.example/", out _));
        Assert.True(t.IsMainDocument("https://new.example/", out _));
    }

    [Fact]
    public void ForgetTab_NettoieTout()
    {
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(5, "https://site.example/", isRedirect: false);
        t.ArmPendingUnknownFailure(5, "https://site.example/");

        t.ForgetTab(5);

        Assert.False(t.IsMainDocument("https://site.example/", out _));
        Assert.Null(t.TakeMainDocumentHttpError(5));
    }

    // ── Classification des réponses ─────────────────────────────────────────

    [Fact]
    public void Reponse_HorsDocumentPrincipal_None()
    {
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(1, "https://site.example/", isRedirect: false);

        var signal = t.ClassifyMainDocumentResponse("https://cdn.example/style.css", 200, null);

        Assert.Equal(MainDocumentSignalKind.None, signal.Kind);
    }

    [Fact]
    public void Reponse200_None()
    {
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(1, "https://site.example/", isRedirect: false);

        Assert.Equal(MainDocumentSignalKind.None,
            t.ClassifyMainDocumentResponse("https://site.example/", 200, null).Kind);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(308)]
    public void RedirectionPermanente_ResolueEnAbsolu(int code)
    {
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(1, "https://old.example/", isRedirect: false);

        var signal = t.ClassifyMainDocumentResponse("https://old.example/", code, "https://new.example/");

        Assert.Equal(MainDocumentSignalKind.PermanentRedirect, signal.Kind);
        Assert.Equal("https://old.example/", signal.FromUri);
        Assert.Equal("https://new.example/", signal.ToUri);
    }

    [Fact]
    public void Redirection_LocationRelative_ResolueContreLaRequete()
    {
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(1, "https://site.example/page", isRedirect: false);

        var signal = t.ClassifyMainDocumentResponse("https://site.example/page", 301, "/nouvelle");

        Assert.Equal(MainDocumentSignalKind.PermanentRedirect, signal.Kind);
        Assert.Equal("https://site.example/nouvelle", signal.ToUri);
    }

    [Fact]
    public void Redirection_SansLocation_None()
    {
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(1, "https://old.example/", isRedirect: false);

        Assert.Equal(MainDocumentSignalKind.None,
            t.ClassifyMainDocumentResponse("https://old.example/", 301, null).Kind);
    }

    [Fact]
    public void Erreur5xx_SansEchecArme_SignaleSansDeclencher()
    {
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(2, "https://site.example/", isRedirect: false);

        var signal = t.ClassifyMainDocumentResponse("https://site.example/", 522, null);

        Assert.Equal(MainDocumentSignalKind.HttpError, signal.Kind);
        Assert.Equal(2, signal.TabId);
        Assert.Equal(522, signal.Status);
        Assert.False(signal.TriggerPendingFailure);
        // L'erreur est mémorisée pour NavigationCompleted.
        Assert.Equal(522, t.TakeMainDocumentHttpError(2));
    }

    [Fact]
    public void Erreur5xx_AvecEchecArmeCorrespondant_Declenche()
    {
        // Ordre « échec Unknown avant réponse 5xx » : la réponse doit déclencher.
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(4, "https://site.example/", isRedirect: false);
        t.ArmPendingUnknownFailure(4, "https://site.example/");

        var signal = t.ClassifyMainDocumentResponse("https://site.example/", 503, null);

        Assert.Equal(MainDocumentSignalKind.HttpError, signal.Kind);
        Assert.True(signal.TriggerPendingFailure);
        Assert.Equal("https://site.example/", signal.PendingFailedAddress);
    }

    [Fact]
    public void Erreur5xx_EchecArmePourAutreUri_NeDeclenchePas()
    {
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(4, "https://site.example/", isRedirect: false);
        t.ArmPendingUnknownFailure(4, "https://autre-page.example/");

        var signal = t.ClassifyMainDocumentResponse("https://site.example/", 500, null);

        Assert.Equal(MainDocumentSignalKind.HttpError, signal.Kind);
        Assert.False(signal.TriggerPendingFailure);
    }

    [Fact]
    public void ArmPending_PuisTakeHttpError_OrdreInverse()
    {
        // Ordre « réponse 5xx avant NavigationCompleted » : l'erreur est prise
        // ensuite par NavigationCompleted via TakeMainDocumentHttpError.
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(6, "https://site.example/", isRedirect: false);

        t.ClassifyMainDocumentResponse("https://site.example/", 502, null);

        Assert.Equal(502, t.TakeMainDocumentHttpError(6));
        Assert.Null(t.TakeMainDocumentHttpError(6)); // consommé une seule fois
    }

    // ── Navigations explicites et « Continuer quand même » ──────────────────

    [Fact]
    public void NavigationExplicite_ConsommeeUneSeuleFois()
    {
        var t = new NavigationHealthTracker();
        t.RegisterExplicitNavigation("https://site.example/");

        Assert.True(t.TakeExplicitNavigation("https://site.example/"));
        Assert.False(t.TakeExplicitNavigation("https://site.example/"));
    }

    [Fact]
    public void NavigationExplicite_IgnoreLeSlashFinal()
    {
        var t = new NavigationHealthTracker();
        t.RegisterExplicitNavigation("https://site.example/");

        Assert.True(t.TakeExplicitNavigation("https://site.example"));
    }

    [Fact]
    public void AdContinue_AutoriseParDomaineRacine()
    {
        var t = new NavigationHealthTracker();
        Assert.False(t.IsAdContinueAllowed("pub.example"));

        t.AllowAdContinue("pub.example");

        Assert.True(t.IsAdContinueAllowed("pub.example"));
    }
}
