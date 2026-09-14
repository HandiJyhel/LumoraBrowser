using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Lumora.WinUI.Converters;

// Convertit une chaine Path.Data (mini-langage, voir PathMiniLanguage.cs) en
// Geometry pour un Binding XAML - utilise a la place d'une conversion
// implicite string -> Geometry non garantie sur un Binding classique (aucune
// documentation WinUI3 ne certifie ce comportement, contrairement au meme
// mini-langage pose en attribut XAML litteral, qui lui est bien pris en
// charge par le compilateur XAML). Meme esprit que
// StartMenuFamilyKeyToBrushConverter (fichier voisin).
public sealed class PathDataConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language) =>
        value is string data && !string.IsNullOrWhiteSpace(data) ? PathGeometryBuilder.Build(data) : null;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
