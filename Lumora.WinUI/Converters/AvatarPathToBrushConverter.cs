using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Lumora.WinUI.Converters;

// Convertit un chemin d'avatar (string?, LumoraProfileEntry.AvatarPath /
// ProfilePickerItem.AvatarPath - Models/Profiles.cs) en ImageBrush pour le
// Fill d'une Ellipse de profil (selecteur de profil). Meme patron que
// StartMenuFamilyKeyToBrushConverter : le brush de repli (aucun avatar) vit
// dans RootShell.Resources, hors de portee d'un convertisseur XAML - fourni
// par l'appelant via ConverterParameter.
public sealed class AvatarPathToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var path = value as string;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return parameter;

        try
        {
            return new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(path, UriKind.Absolute)),
                Stretch = Stretch.UniformToFill
            };
        }
        catch
        {
            return parameter;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException("AvatarPathToBrushConverter est a sens unique.");
}
