using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Verifie le parseur pur (PathMiniLanguage.cs) qui remplace la conversion
// implicite string -> Geometry d'un Binding XAML (non garantie/non
// verifiable en WinUI3, contrairement au meme mini-langage pose en attribut
// XAML litteral). Couvre a la fois la grammaire elle-meme et - surtout -
// que TOUTES les geometries d'icones "Encre chaude" ecrites a la main dans
// ce depot (BookmarkGlyphs, StartMenuGlyphs) sont syntaxiquement valides :
// un mauvais compte d'arguments sur une commande (M/L/C/A) serait detecte
// ici, sans avoir besoin d'un rendu visuel.
public class PathMiniLanguageTests
{
    [Fact]
    public void Parse_uneSeuleFigureMLZ_renvoieUneFigureFermee()
    {
        var figures = PathMiniLanguage.Parse("M4,7 L10,7 L12,9 Z");

        var figure = Assert.Single(figures);
        Assert.Equal(new PathPoint(4, 7), figure.StartPoint);
        Assert.True(figure.IsClosed);
        Assert.Equal(2, figure.Segments.Count);
        Assert.Equal(PathSegmentKind.Line, figure.Segments[0].Kind);
        Assert.Equal(new PathPoint(10, 7), figure.Segments[0].EndPoint);
        Assert.Equal(new PathPoint(12, 9), figure.Segments[1].EndPoint);
    }

    [Fact]
    public void Parse_plusieursSousFigures_renvoieChaqueFigureSeparement()
    {
        var figures = PathMiniLanguage.Parse("M1,1 L2,2 Z M5,5 L6,6 Z M9,9 L10,10 Z");

        Assert.Equal(3, figures.Count);
        Assert.Equal(new PathPoint(1, 1), figures[0].StartPoint);
        Assert.Equal(new PathPoint(5, 5), figures[1].StartPoint);
        Assert.Equal(new PathPoint(9, 9), figures[2].StartPoint);
        Assert.All(figures, f => Assert.True(f.IsClosed));
    }

    [Fact]
    public void Parse_figureNonFermee_IsClosedResteFaux()
    {
        var figures = PathMiniLanguage.Parse("M1,1 L2,2");

        var figure = Assert.Single(figures);
        Assert.False(figure.IsClosed);
    }

    [Fact]
    public void Parse_commandeArc_liLes5ParametresPlusLePoint()
    {
        var figures = PathMiniLanguage.Parse("M12,12 A5,5 0 1,1 2,12 Z");

        var figure = Assert.Single(figures);
        var arc = Assert.Single(figure.Segments);
        Assert.Equal(PathSegmentKind.Arc, arc.Kind);
        Assert.Equal(5, arc.RadiusX);
        Assert.Equal(5, arc.RadiusY);
        Assert.True(arc.IsLargeArc);
        Assert.True(arc.IsClockwise);
        Assert.Equal(new PathPoint(2, 12), arc.EndPoint);
    }

    [Fact]
    public void Parse_commandeCubique_liLes2PointsDeControlePlusLePoint()
    {
        var figures = PathMiniLanguage.Parse("M12,3.2 C18.6,5.9 18.6,11 18.6,15 Z");

        var figure = Assert.Single(figures);
        var bezier = Assert.Single(figure.Segments);
        Assert.Equal(PathSegmentKind.CubicBezier, bezier.Kind);
        Assert.Equal(new PathPoint(18.6, 5.9), bezier.Control1);
        Assert.Equal(new PathPoint(18.6, 11), bezier.Control2);
        Assert.Equal(new PathPoint(18.6, 15), bezier.EndPoint);
    }

    [Fact]
    public void Parse_chaineVideOuBlanche_neRenvoieAucuneFigure()
    {
        Assert.Empty(PathMiniLanguage.Parse(""));
        Assert.Empty(PathMiniLanguage.Parse("   "));
    }

    // Toutes les geometries "Encre chaude" ecrites a la main pour cette
    // refonte, une par une : chacune doit parser sans exception et produire
    // au moins une figure. Ne prouve pas le rendu visuel exact (impossible a
    // verifier dans cet environnement), mais attrape une erreur de syntaxe
    // ou un mauvais compte d'arguments avant l'execution reelle.
    public static IEnumerable<object[]> ToutesLesGeometriesEncreChaude()
    {
        // BookmarkGlyphs (Models/Bookmarks.cs) n'est pas compile dans ce
        // projet de tests (le fichier depend de types WinUI ailleurs) -
        // valeurs recopiees ici en litteral, comme pour ShieldButton/
        // VaultQuickAccessButton plus bas.
        yield return new object[]
        {
            "BookmarkGlyphs.Folder",
            "M4,7 L10,7 L12,9 L20,9 L20,18 L4,18 Z"
        };
        yield return new object[]
        {
            "BookmarkGlyphs.Link",
            "M6,3 L14,3 L19,8 L19,21 L6,21 Z M9,12 L16,12 L16,13.4 L9,13.4 Z M9,15.5 L16,15.5 L16,16.9 L9,16.9 Z"
        };
        yield return new object[] { nameof(StartMenuGlyphs.Modules), StartMenuGlyphs.Modules };
        yield return new object[] { nameof(StartMenuGlyphs.ReaderMode), StartMenuGlyphs.ReaderMode };
        yield return new object[] { nameof(StartMenuGlyphs.Notes), StartMenuGlyphs.Notes };
        yield return new object[] { nameof(StartMenuGlyphs.Translate), StartMenuGlyphs.Translate };
        yield return new object[] { nameof(StartMenuGlyphs.ReadingLens), StartMenuGlyphs.ReadingLens };
        yield return new object[] { nameof(StartMenuGlyphs.Padlock), StartMenuGlyphs.Padlock };
        yield return new object[] { nameof(StartMenuGlyphs.Shield), StartMenuGlyphs.Shield };
        yield return new object[] { nameof(StartMenuGlyphs.Key), StartMenuGlyphs.Key };
        yield return new object[] { nameof(StartMenuGlyphs.Sessions), StartMenuGlyphs.Sessions };
        yield return new object[] { nameof(StartMenuGlyphs.Wallet), StartMenuGlyphs.Wallet };
        yield return new object[] { nameof(StartMenuGlyphs.Eye), StartMenuGlyphs.Eye };
        yield return new object[] { nameof(StartMenuGlyphs.Target), StartMenuGlyphs.Target };
        yield return new object[] { nameof(StartMenuGlyphs.NewWindow), StartMenuGlyphs.NewWindow };
        yield return new object[] { nameof(StartMenuGlyphs.Star), StartMenuGlyphs.Star };
        yield return new object[] { nameof(StartMenuGlyphs.Clock), StartMenuGlyphs.Clock };
        yield return new object[] { nameof(StartMenuGlyphs.Download), StartMenuGlyphs.Download };
        yield return new object[] { nameof(StartMenuGlyphs.Pin), StartMenuGlyphs.Pin };
        yield return new object[] { nameof(StartMenuGlyphs.Restore), StartMenuGlyphs.Restore };
        yield return new object[] { nameof(StartMenuGlyphs.TabStack), StartMenuGlyphs.TabStack };
        yield return new object[] { nameof(StartMenuGlyphs.Sliders), StartMenuGlyphs.Sliders };
        yield return new object[] { nameof(StartMenuGlyphs.Person), StartMenuGlyphs.Person };
        yield return new object[] { nameof(StartMenuGlyphs.Info), StartMenuGlyphs.Info };
        yield return new object[] { nameof(StartMenuGlyphs.Layout), StartMenuGlyphs.Layout };
        yield return new object[] { nameof(StartMenuGlyphs.Rss), StartMenuGlyphs.Rss };

        // Geometries posees en Data litteral (pas des constantes C#, donc
        // recopiees ici) dans MainWindow.xaml pour ShieldButton et
        // VaultQuickAccessButton (Toolbar) - meme raisonnement, meme garde.
        yield return new object[]
        {
            "ShieldButton (Toolbar, bouclier)",
            "M12,3.2 L18.6,5.9 L18.6,11 C18.6,15.6 15.9,19.1 12,20.8 C8.1,19.1 5.4,15.6 5.4,11 L5.4,5.9 Z"
        };
        yield return new object[]
        {
            "VaultQuickAccessButton (Toolbar, cle)",
            "M12,12 A5,5 0 1,1 2,12 A5,5 0 1,1 12,12 Z M9.3,12 A2.3,2.3 0 1,1 4.7,12 A2.3,2.3 0 1,1 9.3,12 Z " +
            "M12,10.8 L20,10.8 L20,13.2 L12,13.2 Z M15.4,13.2 L16.6,13.2 L16.6,15.6 L15.4,15.6 Z " +
            "M18.2,13.2 L19.6,13.2 L19.6,16.8 L18.2,16.8 Z"
        };

        // VaultPanelGlyphs (MainWindow.VaultPanel.cs, "liste/detail du Coffre").
        yield return new object[] { nameof(VaultPanelGlyphs.Copy), VaultPanelGlyphs.Copy };
        yield return new object[] { nameof(VaultPanelGlyphs.Forward), VaultPanelGlyphs.Forward };
        yield return new object[] { nameof(VaultPanelGlyphs.Pencil), VaultPanelGlyphs.Pencil };
        yield return new object[] { nameof(VaultPanelGlyphs.Tag), VaultPanelGlyphs.Tag };
        yield return new object[] { nameof(VaultPanelGlyphs.Trash), VaultPanelGlyphs.Trash };
        yield return new object[] { nameof(VaultPanelGlyphs.Person) + " (VaultPanelGlyphs)", VaultPanelGlyphs.Person };
        yield return new object[] { nameof(VaultPanelGlyphs.Plus), VaultPanelGlyphs.Plus };
    }

    [Theory]
    [MemberData(nameof(ToutesLesGeometriesEncreChaude))]
    public void Parse_chaqueGeometrieEncreChaude_parseSansExceptionEtProduitAuMoinsUneFigure(string nom, string data)
    {
        var figures = PathMiniLanguage.Parse(data);

        Assert.True(figures.Count > 0, $"{nom} n'a produit aucune figure.");
        Assert.All(figures, f => Assert.True(f.Segments.Count > 0, $"{nom} contient une figure sans segment."));
    }
}
