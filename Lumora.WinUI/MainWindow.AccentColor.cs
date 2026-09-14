using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Lumora.WinUI;

// Couleur d'accentuation globale (ColorPicker + pastille dans Parametres >
// Mon Lumora > Theme et couleurs). Anciennement "MainWindow.ModeAccentColor.cs"
// (2026-08-10 -> 2026-09-13) : un reglage PAR mode d'usage (6 teintes),
// n'affectant que le chrome du mode actif. Remplace le 2026-09-14 (session
// "3.5") par UN SEUL reglage global, facon Windows - retour utilisateur
// explicite : "je veux une veritable couleur d'accentuation ... [mais] je
// veux pas que ca soit agressif non plus" et, sur les favoris precisement,
// "pas de couleurs de fond ... que l'icone du dossier prenne la couleur".
// S'applique desormais quel que soit le Mode d'usage actif, via 2 points
// d'injection - ApplyAccessibilitySettings (palette generique : bouton
// principal, dossiers de favoris, pastille d'onglet active) et
// ApplyUsageModeChrome (chrome du mode : boutons de la ligne d'outils,
// verre du compagnon, halo...) - voir MainWindow.SettingsTheme.cs.
public sealed partial class MainWindow
{
    private string? _pendingAccentColorHex;
    private bool _pendingAccentColorReset;

    // ── Couleur d'accentuation globale ────────────────────────────────────

    private bool TryResolveGlobalAccentOverride(out Windows.UI.Color color)
    {
        color = default;
        var hex = _uiSettings.AccentColor;
        return !string.IsNullOrWhiteSpace(hex) && TryParseHexColor(hex, out color);
    }

    private Windows.UI.Color ResolveDefaultAccentColor() =>
        ResolveModeChromePalette((_uiSettings.UsageMode ?? "neutral").ToLowerInvariant(),
            LumoraTheme.ResolveIsDarkTheme(_uiSettings), 255).Accent;

    // Initialise le ColorPicker/la pastille depuis _uiSettings au moment ou
    // les Reglages sont (re)affiches - meme moment que les autres
    // SelectComboByTag(...) de ApplyUiSettings(), sous _suppressUiSettingsSave.
    private void InitializeModeAccentColorPickers()
    {
        _pendingAccentColorHex = null;
        _pendingAccentColorReset = false;

        var hex = _uiSettings.AccentColor;
        var color = !string.IsNullOrWhiteSpace(hex) && TryParseHexColor(hex, out var custom)
            ? custom
            : ResolveDefaultAccentColor();

        AccentColorPicker.Color = color;
        AccentColorSwatchButton.Background = new SolidColorBrush(color);
    }

    private void AccentColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_suppressUiSettingsSave)
        {
            return;
        }

        _pendingAccentColorHex = ToHexColor(args.NewColor);
        _pendingAccentColorReset = false;
        AccentColorSwatchButton.Background = new SolidColorBrush(args.NewColor);
        MarkAppearanceOptionsPending();
    }

    private void AccentColorResetButton_Click(object sender, RoutedEventArgs e)
    {
        _pendingAccentColorHex = null;
        _pendingAccentColorReset = true;

        var defaultColor = ResolveDefaultAccentColor();
        AccentColorPicker.Color = defaultColor;
        AccentColorSwatchButton.Background = new SolidColorBrush(defaultColor);
        MarkAppearanceOptionsPending();
    }

    // Appele depuis ApplySettingsChangesButton_Click, comme
    // ApplyPendingAvatarChange()/ApplyPendingWallpaperChange() : la couleur
    // choisie n'est commise dans _uiSettings (et sauvegardee) qu'a la
    // validation explicite, jamais en direct pendant le glisser du picker
    // (ColorChanged se declenche a tres haute frequence - un re-theme complet
    // a chaque tick serait couteux, en plus de rompre la coherence "rien ne
    // change avant Appliquer" du reste de cette section Personnalisation).
    private bool ApplyPendingModeAccentColorChanges()
    {
        if (_pendingAccentColorHex is null && !_pendingAccentColorReset)
        {
            return false;
        }

        _uiSettings.AccentColor = _pendingAccentColorReset ? string.Empty : _pendingAccentColorHex ?? string.Empty;
        _pendingAccentColorHex = null;
        _pendingAccentColorReset = false;
        _uiSettings.Save(_profile.UiSettingsFile);
        return true;
    }

    private void ResetPendingModeAccentColorChanges()
    {
        _pendingAccentColorHex = null;
        _pendingAccentColorReset = false;
    }

    // ── Derivation automatique du degrade (couleur unique choisie) ──────────

    // Point d'injection appele depuis ApplyUsageModeChrome (MainWindow.SettingsTheme.cs),
    // STRICTEMENT apres son court-circuit "if (highContrast) { ...; return; }" :
    // le contraste eleve continue donc d'ecraser toute couleur personnalisee
    // exactement comme avant cette fonctionnalite.
    private static ModeChromePalette ApplyCustomModeAccent(ModeChromePalette basePalette, Windows.UI.Color accent, bool isDark)
    {
        var (cool, warm) = DeriveModeAccentTones(accent, isDark);
        return basePalette with
        {
            Accent = accent,
            AccentSoft = WithAlpha(accent, isDark ? (byte)32 : (byte)38),
            CoolAccent = cool,
            CoolAccentSoft = WithAlpha(cool, isDark ? (byte)28 : (byte)30),
            WarmAccent = warm,
            Focus = DeriveFocusTone(accent, isDark)
        };
    }

    // Meme derivation que ci-dessus, pour la palette GENERIQUE (non liee au
    // Mode d'usage) resolue par ResolveAccentPalette dans
    // ApplyAccessibilitySettings - bouton principal, pastille d'onglet
    // active, dossiers de favoris.
    private static (Windows.UI.Color Accent, Windows.UI.Color AccentSoft, Windows.UI.Color CoolAccent, Windows.UI.Color CoolAccentSoft, Windows.UI.Color Focus)
        ApplyCustomAccentToPalette(
            (Windows.UI.Color Accent, Windows.UI.Color AccentSoft, Windows.UI.Color CoolAccent, Windows.UI.Color CoolAccentSoft, Windows.UI.Color Focus) basePalette,
            Windows.UI.Color accent, bool isDark)
    {
        var (cool, warm) = DeriveModeAccentTones(accent, isDark);
        _ = warm; // pas de canal "chaud" distinct dans cette palette (5 champs, pas 7)
        return (
            accent,
            WithAlpha(accent, isDark ? (byte)32 : (byte)38),
            cool,
            WithAlpha(cool, isDark ? (byte)28 : (byte)30),
            DeriveFocusTone(accent, isDark));
    }

    private static (Windows.UI.Color Cool, Windows.UI.Color Warm) DeriveModeAccentTones(Windows.UI.Color accent, bool isDark)
    {
        RgbToHsl(accent, out var h, out var s, out var l);
        var cool = HslToRgb(h - 32, s, Math.Clamp(l + (isDark ? 0.12 : -0.08), 0, 1));
        var warm = HslToRgb(h + 32, Math.Clamp(s + 0.05, 0, 1), Math.Clamp(l + (isDark ? -0.05 : 0.05), 0, 1));
        return (cool, warm);
    }

    private static Windows.UI.Color DeriveFocusTone(Windows.UI.Color accent, bool isDark)
    {
        RgbToHsl(accent, out var h, out var s, out _);
        return HslToRgb(h, Math.Clamp(s * 0.65, 0, 1), isDark ? 0.86 : 0.30);
    }

    private static void RgbToHsl(Windows.UI.Color color, out double h, out double s, out double l)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2.0;

        if (max - min < 0.0001)
        {
            h = 0;
            s = 0;
            return;
        }

        var d = max - min;
        s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

        if (max == r)
            h = (g - b) / d + (g < b ? 6 : 0);
        else if (max == g)
            h = (b - r) / d + 2;
        else
            h = (r - g) / d + 4;

        h *= 60;
    }

    private static Windows.UI.Color HslToRgb(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        l = Math.Clamp(l, 0, 1);

        if (s <= 0.0001)
        {
            var gray = (byte)Math.Round(l * 255);
            return Windows.UI.Color.FromArgb(255, gray, gray, gray);
        }

        static double Channel(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        var hk = h / 360.0;

        return Windows.UI.Color.FromArgb(255,
            (byte)Math.Round(Math.Clamp(Channel(p, q, hk + 1.0 / 3), 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(Channel(p, q, hk), 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(Channel(p, q, hk - 1.0 / 3), 0, 1) * 255));
    }

    private static bool TryParseHexColor(string? hex, out Windows.UI.Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var value = hex.Trim().TrimStart('#');
        if (value.Length != 6
            || !byte.TryParse(value[..2], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(value[2..4], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(value[4..6], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        color = Windows.UI.Color.FromArgb(255, r, g, b);
        return true;
    }

    private static string ToHexColor(Windows.UI.Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
