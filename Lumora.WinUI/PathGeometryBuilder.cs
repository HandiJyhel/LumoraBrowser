using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Lumora.WinUI;

// Adaptateur mince (dependance WinUI) au-dessus de PathMiniLanguage (logique
// pure, testable). Construit un vrai PathGeometry via PathFigure/
// LineSegment/BezierSegment/ArcSegment - pas de Geometry.Parse (absent de
// WinUI3, contrairement a WPF, deja etabli sur BookmarkIconElement).
public static class PathGeometryBuilder
{
    public static PathGeometry Build(string data)
    {
        var geometry = new PathGeometry();
        foreach (var figureSpec in PathMiniLanguage.Parse(data))
        {
            var figure = new PathFigure
            {
                StartPoint = new Point(figureSpec.StartPoint.X, figureSpec.StartPoint.Y),
                IsClosed = figureSpec.IsClosed
            };

            foreach (var segment in figureSpec.Segments)
            {
                figure.Segments.Add(segment.Kind switch
                {
                    PathSegmentKind.Line => new LineSegment
                    {
                        Point = new Point(segment.EndPoint.X, segment.EndPoint.Y)
                    },
                    PathSegmentKind.CubicBezier => new BezierSegment
                    {
                        Point1 = new Point(segment.Control1.X, segment.Control1.Y),
                        Point2 = new Point(segment.Control2.X, segment.Control2.Y),
                        Point3 = new Point(segment.EndPoint.X, segment.EndPoint.Y)
                    },
                    PathSegmentKind.Arc => new ArcSegment
                    {
                        Size = new Windows.Foundation.Size(segment.RadiusX, segment.RadiusY),
                        IsLargeArc = segment.IsLargeArc,
                        SweepDirection = segment.IsClockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
                        Point = new Point(segment.EndPoint.X, segment.EndPoint.Y)
                    },
                    _ => throw new NotSupportedException($"Segment non supporte : {segment.Kind}")
                });
            }

            geometry.Figures.Add(figure);
        }

        return geometry;
    }
}
