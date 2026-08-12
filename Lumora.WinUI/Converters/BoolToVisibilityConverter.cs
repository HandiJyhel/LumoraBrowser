using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Lumora.WinUI.Converters;

// Convertisseur generique bool -> Visibility (true => Visible). Utilise
// ConverterParameter="Invert" pour inverser (false => Visible) sans dupliquer
// un second convertisseur.
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is bool b && b;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException("BoolToVisibilityConverter est a sens unique.");
}
