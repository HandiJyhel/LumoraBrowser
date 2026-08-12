using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Lumora.WinUI;

// Couleur personnalisee par mode d'usage (ColorPicker + pastille dans
// Parametres > Mon Lumora > Disposition). Extrait de MainWindow.IdentitySpine.cs
// (2026-08-10, suppression du Style Lumora - demande explicite utilisateur,
// "trop difficile a gerer" apres plusieurs jours de correctifs concentres
// sur ce style) : cette fonctionnalite n'a jamais ete exclusive a la colonne
// identitaire, elle s'applique aux deux styles via ApplyUsageModeChrome
// (MainWindow.SettingsTheme.cs) - deplacee ici plutot que supprimee.
public sealed partial class MainWindow
{
    private static readonly string[] ModeAccentColorKeys =
    {
        "neutral", "focus", "reading", "creative", "research", "night"
    };

    private readonly Dictionary<string, string> _pendingModeAccentColors = new(StringComparer.OrdinalIgnoreCase);

    // ── Couleur personnalisee par mode d'usage ───────────────────────────────

    private string GetModeAccentColorHex(string mode) => mode switch
    {
        "neutral" => _uiSettings.ModeAccentColorNeutral,
        "focus" => _uiSettings.ModeAccentColorFocus,
        "reading" => _uiSettings.ModeAccentColorReading,
        "creative" => _uiSettings.ModeAccentColorCreative,
        "research" => _uiSettings.ModeAccentColorResearch,
        "night" => _uiSettings.ModeAccentColorNight,
        _ => string.Empty
    };

    private void SetModeAccentColorHex(string mode, string hex)
    {
        switch (mode)
        {
            case "neutral": _uiSettings.ModeAccentColorNeutral = hex; break;
            case "focus": _uiSettings.ModeAccentColorFocus = hex; break;
            case "reading": _uiSettings.ModeAccentColorReading = hex; break;
            case "creative": _uiSettings.ModeAccentColorCreative = hex; break;
            case "research": _uiSettings.ModeAccentColorResearch = hex; break;
            case "night": _uiSettings.ModeAccentColorNight = hex; break;
        }
    }

    private ColorPicker? ModeAccentColorPicker(string mode) => mode switch
    {
        "neutral" => ModeColorPickerNeutral,
        "focus" => ModeColorPickerFocus,
        "reading" => ModeColorPickerReading,
        "creative" => ModeColorPickerCreative,
        "research" => ModeColorPickerResearch,
        "night" => ModeColorPickerNight,
        _ => null
    };

    private Button? ModeAccentColorSwatchButton(string mode) => mode switch
    {
        "neutral" => ModeColorSwatchNeutralButton,
        "focus" => ModeColorSwatchFocusButton,
        "reading" => ModeColorSwatchReadingButton,
        "creative" => ModeColorSwatchCreativeButton,
        "research" => ModeColorSwatchResearchButton,
        "night" => ModeColorSwatchNightButton,
        _ => null
    };

    private Windows.UI.Color ResolveDefaultModeAccentColor(string mode) =>
        ResolveModeChromePalette(mode, LumoraTheme.ResolveIsDarkTheme(_uiSettings), 255).Accent;

    // Initialise chaque ColorPicker/pastille depuis _uiSettings au moment ou
    // les Reglages sont (re)affiches - meme moment que les autres
    // SelectComboByTag(...) de ApplyUiSettings(), sous _suppressUiSettingsSave.
    private void InitializeModeAccentColorPickers()
    {
        _pendingModeAccentColors.Clear();

        foreach (var mode in ModeAccentColorKeys)
        {
            var hex = GetModeAccentColorHex(mode);
            var color = !string.IsNullOrWhiteSpace(hex) && TryParseHexColor(hex, out var custom)
                ? custom
                : ResolveDefaultModeAccentColor(mode);

            var picker = ModeAccentColorPicker(mode);
            if (picker is not null)
            {
                picker.Color = color;
            }

            var swatch = ModeAccentColorSwatchButton(mode);
            if (swatch is not null)
            {
                swatch.Background = new SolidColorBrush(color);
            }
        }
    }

    private void ModeColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_suppressUiSettingsSave || sender.Tag is not string mode)
        {
            return;
        }

        _pendingModeAccentColors[mode] = ToHexColor(args.NewColor);
        var swatch = ModeAccentColorSwatchButton(mode);
        if (swatch is not null)
        {
            swatch.Background = new SolidColorBrush(args.NewColor);
        }

        MarkAppearanceOptionsPending();
    }

    private void ModeColorResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string mode })
        {
            return;
        }

        _pendingModeAccentColors[mode] = string.Empty;
        var defaultColor = ResolveDefaultModeAccentColor(mode);

        var picker = ModeAccentColorPicker(mode);
        if (picker is not null)
        {
            picker.Color = defaultColor;
        }

        var swatch = ModeAccentColorSwatchButton(mode);
        if (swatch is not null)
        {
            swatch.Background = new SolidColorBrush(defaultColor);
        }

        MarkAppearanceOptionsPending();
    }

    // Appele depuis ApplySettingsChangesButton_Click, comme
    // ApplyPendingAvatarChange()/ApplyPendingWallpaperChange() : les couleurs
    // choisies ne sont commises dans _uiSettings (et sauvegardees) qu'a la
    // validation explicite, jamais en direct pendant le glisser du picker
    // (ColorChanged se declenche a tres haute frequence - un re-theme complet
    // a chaque tick serait couteux, en plus de rompre la coherence "rien ne
    // change avant Appliquer" du reste de cette section Personnalisation).
    private bool ApplyPendingModeAccentColorChanges()
    {
        if (_pendingModeAccentColors.Count == 0)
        {
            return false;
        }

        foreach (var (mode, hex) in _pendingModeAccentColors)
        {
            SetModeAccentColorHex(mode, hex);
        }

        _pendingModeAccentColors.Clear();
        _uiSettings.Save(_profile.UiSettingsFile);
        return true;
    }

    private void ResetPendingModeAccentColorChanges() => _pendingModeAccentColors.Clear();

    // ── Derivation automatique du degrade (couleur unique choisie par mode) ─

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
