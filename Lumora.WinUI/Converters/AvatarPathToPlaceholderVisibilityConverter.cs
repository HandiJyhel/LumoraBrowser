using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Lumora.WinUI.Converters;

// Affiche l'icone de repli (silhouette neutre) exactement quand
// AvatarPathToBrushConverter serait lui-meme retombe sur son brush de repli -
// meme condition de validite (chemin present ET fichier toujours sur disque),
// pour ne jamais afficher l'icone PAR-DESSUS une vraie photo ni la cacher
// quand il n'y a pas de photo.
public sealed class AvatarPathToPlaceholderVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var path = value as string;
        var hasAvatar = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        return hasAvatar ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException("AvatarPathToPlaceholderVisibilityConverter est a sens unique.");
}
