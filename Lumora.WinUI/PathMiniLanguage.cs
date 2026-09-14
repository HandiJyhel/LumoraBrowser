using System.Globalization;

namespace Lumora.WinUI;

// Refonte organique "Encre chaude" (2026-09-12, suite) : plutot que de
// compter sur la conversion implicite string -> Geometry d'un Binding XAML
// (comportement non garanti/non verifiable dans cet environnement - la seule
// API documentee et certaine, Geometry.Parse, n'existe pas en WinUI3
// contrairement a WPF, deja etabli sur BookmarkIconElement), ce fichier
// analyse lui-meme le mini-langage Path.Data utilise par les geometries de
// cette refonte (BookmarkGlyphs, StartMenuGlyphs...) en une structure pure,
// testable sans dependance WinUI (voir Lumora.Tests). La construction du
// vrai PathGeometry WinUI (PathFigure/LineSegment/...) reste un adaptateur
// mince separe (PathGeometryBuilder.cs), comme BreakReminderService/
// MainWindow.AccessibilityAlerts le font deja pour la logique pure vs le
// collage UI.
//
// Sous-ensemble volontairement restreint (seul ce que ce depot produit lui-
// meme) : commandes M/L/C/A/Z en absolu uniquement, un seul chiffre apres
// la virgule/l'espace comme separateur - pas de grammaire SVG complete.
public enum PathSegmentKind
{
    Line,
    CubicBezier,
    Arc
}

public readonly record struct PathPoint(double X, double Y);

public sealed record PathSegmentSpec(
    PathSegmentKind Kind,
    PathPoint EndPoint,
    PathPoint Control1 = default,
    PathPoint Control2 = default,
    double RadiusX = 0,
    double RadiusY = 0,
    bool IsLargeArc = false,
    bool IsClockwise = false);

public sealed record PathFigureSpec(PathPoint StartPoint, bool IsClosed, IReadOnlyList<PathSegmentSpec> Segments);

public static class PathMiniLanguage
{
    public static IReadOnlyList<PathFigureSpec> Parse(string data)
    {
        var figures = new List<PathFigureSpec>();
        if (string.IsNullOrWhiteSpace(data))
        {
            return figures;
        }

        var tokens = Tokenize(data);
        var i = 0;
        PathPoint current = default;
        PathPoint figureStart = default;
        List<PathSegmentSpec>? segments = null;
        var closed = false;

        void FlushFigure()
        {
            if (segments is not null)
            {
                figures.Add(new PathFigureSpec(figureStart, closed, segments));
            }
        }

        while (i < tokens.Count)
        {
            var command = tokens[i];
            switch (command)
            {
                case "M":
                    FlushFigure();
                    i++;
                    current = ReadPoint(tokens, ref i);
                    figureStart = current;
                    segments = new List<PathSegmentSpec>();
                    closed = false;
                    break;

                case "L":
                    i++;
                    var linePoint = ReadPoint(tokens, ref i);
                    segments!.Add(new PathSegmentSpec(PathSegmentKind.Line, linePoint));
                    current = linePoint;
                    break;

                case "C":
                    i++;
                    var c1 = ReadPoint(tokens, ref i);
                    var c2 = ReadPoint(tokens, ref i);
                    var end = ReadPoint(tokens, ref i);
                    segments!.Add(new PathSegmentSpec(PathSegmentKind.CubicBezier, end, c1, c2));
                    current = end;
                    break;

                case "A":
                    i++;
                    var rx = ReadNumber(tokens, ref i);
                    var ry = ReadNumber(tokens, ref i);
                    _ = ReadNumber(tokens, ref i); // rotation de l'axe X, toujours 0 dans ce depot
                    var largeArc = ReadNumber(tokens, ref i) != 0;
                    var sweep = ReadNumber(tokens, ref i) != 0;
                    var arcEnd = ReadPoint(tokens, ref i);
                    segments!.Add(new PathSegmentSpec(PathSegmentKind.Arc, arcEnd, RadiusX: rx, RadiusY: ry, IsLargeArc: largeArc, IsClockwise: sweep));
                    current = arcEnd;
                    break;

                case "Z":
                    i++;
                    closed = true;
                    current = figureStart;
                    break;

                default:
                    // Jeton non reconnu : on avance pour ne jamais boucler
                    // indefiniment sur une chaine malformee.
                    i++;
                    break;
            }
        }

        FlushFigure();
        return figures;
    }

    private static PathPoint ReadPoint(IReadOnlyList<string> tokens, ref int i)
    {
        var x = ReadNumber(tokens, ref i);
        var y = ReadNumber(tokens, ref i);
        return new PathPoint(x, y);
    }

    private static double ReadNumber(IReadOnlyList<string> tokens, ref int i)
    {
        var value = double.Parse(tokens[i], CultureInfo.InvariantCulture);
        i++;
        return value;
    }

    // Separe la chaine en jetons : chaque lettre de commande (M/L/C/A/Z)
    // devient son propre jeton, chaque nombre (signe/point decimal inclus)
    // aussi - virgules et espaces sont de simples separateurs.
    private static List<string> Tokenize(string data)
    {
        var tokens = new List<string>();
        var current = string.Empty;

        void Flush()
        {
            if (current.Length > 0)
            {
                tokens.Add(current);
                current = string.Empty;
            }
        }

        foreach (var ch in data)
        {
            if (ch is 'M' or 'L' or 'C' or 'A' or 'Z')
            {
                Flush();
                tokens.Add(ch.ToString());
            }
            else if (ch is ',' or ' ' or '\t' or '\n' or '\r')
            {
                Flush();
            }
            else
            {
                current += ch;
            }
        }

        Flush();
        return tokens;
    }
}
