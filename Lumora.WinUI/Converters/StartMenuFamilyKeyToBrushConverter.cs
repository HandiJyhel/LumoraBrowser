using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Lumora.WinUI.Converters;

// Separe le modele pur (StartMenuTileViewModel.FamilyKey, une simple chaine -
// voir Models/StartMenuTiles.cs) du rendu WinUI (Brush) : ce fichier vit
// volontairement hors de Models/, qui est compile aussi dans Lumora.Tests
// (projet sans reference a Microsoft.UI.Xaml) - y ajouter un type WinUI
// casserait la compilation des tests.
//
// Les 3 brushes de famille sont en Application.Resources (un convertisseur
// XAML n'a pas de reference a l'instance de fenetre). Le cas neutre
// ("Lumora et profil", FamilyKey null) ne fait PAS pareil : il doit rester le
// degrade NovaIdentityMarkBrush qui vit dans RootShell.Resources et que
// SetIdentityGradient (MainWindow.SettingsTheme.cs) mute en direct pour le
// theme/contraste eleve/couleur de mode - inaccessible depuis
// Application.Current.Resources. Passe donc via ConverterParameter, fourni
// par le binding XAML lui-meme (qui, lui, resout {StaticResource} normalement
// via RootShell).
public sealed class StartMenuFamilyKeyToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var key = value as string;
        var resourceKey = key switch
        {
            "content" => "NovaTileFamilyContentBrush",
            "protection" => "NovaTileFamilyProtectionBrush",
            "tools" => "NovaTileFamilyToolsBrush",
            "identity" => "NovaTileFamilyIdentityBrush",
            _ => null,
        };
        return resourceKey is null
            ? (Brush)parameter
            : (Brush)Application.Current.Resources[resourceKey];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException("StartMenuFamilyKeyToBrushConverter est a sens unique.");
}
