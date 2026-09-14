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
    // Jeu d'icones petit et fixe (StartMenuGlyphs/VaultPanelGlyphs/
    // BookmarkGlyphs, quelques dizaines de constantes au total) - mise en
    // cache par chaine plutot que reparser le mini-langage et reallouer un
    // PathGeometry a CHAQUE evaluation de Binding (nettoyage+perf, trouve en
    // audit 2026-09-14 : ce convertisseur est lie par element dans des listes
    // virtualisees - Menu Demarrer, favoris - donc reparse/realloue a chaque
    // recyclage de conteneur pour les memes chaines repetees). Un
    // PathGeometry partage entre plusieurs Path.Data est un usage WinUI
    // normal (lecture seule ici, jamais mute apres construction, comme un
    // Brush partage). Le Binding XAML s'evalue toujours sur le thread UI :
    // pas de verrou necessaire sur ce dictionnaire.
    private static readonly Dictionary<string, Geometry> Cache = new();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string data || string.IsNullOrWhiteSpace(data)) return null;
        if (Cache.TryGetValue(data, out var cached)) return cached;
        var built = PathGeometryBuilder.Build(data);
        Cache[data] = built;
        return built;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
