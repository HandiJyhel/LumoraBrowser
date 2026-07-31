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
    public void Redirection_ConserveLeGesteUtilisateurDeDepart()
    {
        // Cas recherche Google : clic utilisateur vers /url, puis redirection
        // technique vers le résultat final. Le dernier saut doit rester légitime.
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(3, "https://www.google.com/url?q=https%3A%2F%2Ffr.wikipedia.org%2Fwiki%2FTintin", isRedirect: false, isUserInitiated: true);
        t.TrackNavigationStart(3, "https://fr.wikipedia.org/wiki/Tintin", isRedirect: true);

        Assert.True(t.IsUserInitiatedNavigationChain(3));
    }

    [Fact]
    public void NouvelleNavigationAutomatique_OublieLeGesteUtilisateurPrecedent()
    {
        var t = new NavigationHealthTracker();
        t.TrackNavigationStart(3, "https://www.google.com/url?q=https%3A%2F%2Ffr.wikipedia.org%2Fwiki%2FTintin", isRedirect: false, isUserInitiated: true);

        t.TrackNavigationStart(3, "https://boutique-douteuse.example/", isRedirect: false, isUserInitiated: false);

        Assert.False(t.IsUserInitiatedNavigationChain(3));
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

    // ── Popups ouvertes : plafond par geste et tab-under (0.78.2) ───────────

    [Fact]
    public void PopupOuverte_CompteDansLaFenetreDuGeste()
    {
        var t = new NavigationHealthTracker();
        var now = DateTimeOffset.UtcNow;

        Assert.Equal(0, t.CountPopupsInGestureWindow(5, now));
        t.RegisterPopupOpened(5, now);
        Assert.Equal(1, t.CountPopupsInGestureWindow(5, now.AddMilliseconds(300)));
        Assert.Equal(0, t.CountPopupsInGestureWindow(6, now.AddMilliseconds(300)));
    }

    [Fact]
    public void PopupOuverte_SortDeLaFenetreDuGeste_ApresUneSeconde()
    {
        // Deux clics espacés de plus d'une seconde = deux gestes distincts :
        // le second a droit à sa propre popup.
        var t = new NavigationHealthTracker();
        var now = DateTimeOffset.UtcNow;
        t.RegisterPopupOpened(5, now);

        Assert.Equal(0, t.CountPopupsInGestureWindow(5, now.AddSeconds(2)));
    }

    [Fact]
    public void TabUnder_PopupRecente_DetecteePendantTroisSecondes()
    {
        var t = new NavigationHealthTracker();
        var now = DateTimeOffset.UtcNow;
        t.RegisterPopupOpened(5, now);

        Assert.True(t.HadRecentPopup(5, now.AddSeconds(2)));
        Assert.False(t.HadRecentPopup(5, now.AddSeconds(4)));
        Assert.False(t.HadRecentPopup(6, now.AddSeconds(2)));
    }

    [Fact]
    public void ForgetTab_OublieAussiLesPopups()
    {
        var t = new NavigationHealthTracker();
        var now = DateTimeOffset.UtcNow;
        t.RegisterPopupOpened(5, now);
        t.ForgetTab(5);

        Assert.False(t.HadRecentPopup(5, now));
        Assert.Equal(0, t.CountPopupsInGestureWindow(5, now));
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

    // ── Navigation bloquée : signal de pression pour les popups (0.84.0.4) ──

    [Fact]
    public void NavigationBloquee_MemoriseePourLOnglet()
    {
        var t = new NavigationHealthTracker();

        Assert.False(t.HadBlockedNavigation(9));
        t.RecordBlockedNavigation(9);

        Assert.True(t.HadBlockedNavigation(9));
        Assert.False(t.HadBlockedNavigation(10));
    }

    [Fact]
    public void NavigationBloquee_OublieeALaProchaineNavigationFraiche()
    {
        // Une nouvelle navigation (pas un saut de redirection) tourne la page :
        // le signal de pression ne doit pas coller à un onglet qui a depuis
        // navigué ailleurs.
        var t = new NavigationHealthTracker();
        t.RecordBlockedNavigation(9);

        t.TrackNavigationStart(9, "https://autre-site.example/", isRedirect: false);

        Assert.False(t.HadBlockedNavigation(9));
    }

    [Fact]
    public void NavigationBloquee_SurvitAUnSautDeRedirection()
    {
        var t = new NavigationHealthTracker();
        t.RecordBlockedNavigation(9);

        t.TrackNavigationStart(9, "https://autre-site.example/", isRedirect: true);

        Assert.True(t.HadBlockedNavigation(9));
    }

    [Fact]
    public void ForgetTab_OublieAussiLaNavigationBloquee()
    {
        var t = new NavigationHealthTracker();
        t.RecordBlockedNavigation(9);

        t.ForgetTab(9);

        Assert.False(t.HadBlockedNavigation(9));
    }

    // ── Total de popups par onglet : cible le site qui rouvre un onglet à
    // chaque clic séparé (0.84.0.5) ─────────────────────────────────────────

    [Fact]
    public void PremierPopup_NEstPasEncoreUnPopupPrecedent()
    {
        var t = new NavigationHealthTracker();
        var now = DateTimeOffset.UtcNow;

        Assert.False(t.HasOpenedPopupBefore(5));
        t.RegisterPopupOpened(5, now);

        // Le popup qui vient d'être enregistré ne compte pas comme "avant lui" :
        // c'est la valeur AVANT le deuxième popup qui doit basculer.
        Assert.True(t.HasOpenedPopupBefore(5));
    }

    [Fact]
    public void DeuxiemePopup_MemeLongtempsApres_EstDetecteCommeRepete()
    {
        // Contrairement à CountPopupsInGestureWindow (fenêtre d'1 seconde) et
        // HadRecentPopup (3 secondes), ce compteur ne s'élague jamais par le
        // temps : deux clics séparés de plusieurs minutes comptent quand même.
        var t = new NavigationHealthTracker();
        var now = DateTimeOffset.UtcNow;
        t.RegisterPopupOpened(5, now);

        Assert.True(t.HasOpenedPopupBefore(5));
        Assert.Equal(0, t.CountPopupsInGestureWindow(5, now.AddMinutes(5)));
        Assert.False(t.HadRecentPopup(5, now.AddMinutes(5)));
        // ... mais HasOpenedPopupBefore reste vrai, lui, bien après les deux fenêtres ci-dessus.
        Assert.True(t.HasOpenedPopupBefore(5));
    }

    [Fact]
    public void TotalPopups_OublieALaProchaineNavigationFraiche()
    {
        var t = new NavigationHealthTracker();
        t.RegisterPopupOpened(5, DateTimeOffset.UtcNow);

        t.TrackNavigationStart(5, "https://autre-site.example/", isRedirect: false);

        Assert.False(t.HasOpenedPopupBefore(5));
    }

    [Fact]
    public void ForgetTab_OublieAussiLeTotalDePopups()
    {
        var t = new NavigationHealthTracker();
        t.RegisterPopupOpened(5, DateTimeOffset.UtcNow);

        t.ForgetTab(5);

        Assert.False(t.HasOpenedPopupBefore(5));
    }

    // ── Popups en attente d'un choix explicite (0.84.0.6) ────────────────────

    [Fact]
    public void PopupEnAttente_MemoriseePourLOnglet()
    {
        var t = new NavigationHealthTracker();

        Assert.Empty(t.PendingPopups(5));
        t.RecordPendingPopup(5, "https://exemple.example/");

        Assert.Equal(["https://exemple.example/"], t.PendingPopups(5));
        Assert.Empty(t.PendingPopups(6));
    }

    [Fact]
    public void PopupEnAttente_PlusieursSurLeMemeOnglet_ToutesConservees()
    {
        var t = new NavigationHealthTracker();
        t.RecordPendingPopup(5, "https://un.example/");
        t.RecordPendingPopup(5, "https://deux.example/");

        Assert.Equal(["https://un.example/", "https://deux.example/"], t.PendingPopups(5));
    }

    [Fact]
    public void PopupEnAttente_RetraitDUneSeule_GardeLesAutres()
    {
        var t = new NavigationHealthTracker();
        t.RecordPendingPopup(5, "https://un.example/");
        t.RecordPendingPopup(5, "https://deux.example/");

        t.RemovePendingPopup(5, "https://un.example/");

        Assert.Equal(["https://deux.example/"], t.PendingPopups(5));
    }

    [Fact]
    public void PopupEnAttente_ClearPendingPopups_LesRetireToutes()
    {
        var t = new NavigationHealthTracker();
        t.RecordPendingPopup(5, "https://un.example/");
        t.RecordPendingPopup(5, "https://deux.example/");

        t.ClearPendingPopups(5);

        Assert.Empty(t.PendingPopups(5));
    }

    [Fact]
    public void PopupEnAttente_OublieeALaProchaineNavigationFraiche()
    {
        var t = new NavigationHealthTracker();
        t.RecordPendingPopup(5, "https://exemple.example/");

        t.TrackNavigationStart(5, "https://autre-site.example/", isRedirect: false);

        Assert.Empty(t.PendingPopups(5));
    }

    [Fact]
    public void ForgetTab_OublieAussiLesPopupsEnAttente()
    {
        var t = new NavigationHealthTracker();
        t.RecordPendingPopup(5, "https://exemple.example/");

        t.ForgetTab(5);

        Assert.Empty(t.PendingPopups(5));
    }

}
