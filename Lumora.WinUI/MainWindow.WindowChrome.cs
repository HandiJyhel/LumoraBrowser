using System.Collections.ObjectModel;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;
using Lumora.Privacy;
using Lumora.Privacy.NetworkBlocker;
using Lumora.Privacy.TelemetryBlocker;
using Lumora.Privacy.ParameterCleaner;
using Lumora.Privacy.HttpsEnforcer;
using Lumora.Privacy.CnameUncloaker;
using Lumora.Privacy.CosmeticFilter;
using Lumora.Privacy.ConsentManager;
using Lumora.WinUI.Credentials;
using Lumora.WinUI.PasswordManager;

namespace Lumora.WinUI;

// Chrome de fenetre : couleurs de la barre de titre, icone, zone de
// securite, region de glisser-deplacer. Extrait de MainWindow.xaml.cs
// (god file) : ne depend que de _appWindow/ressources XAML, aucun
// changement de comportement.
public sealed partial class MainWindow : Window
{
    private void ApplyWindowTitleBarColors()
    {
        if (_appWindow is null) return;

        var translucent = IsTranslucentChromeEnabled();
        var titleBarBackgroundAlpha = translucent ? (byte)226 : (byte)255;
        var background = BrushColor("NovaChromeSurfaceBrush", UiColor(20, 32, 42, titleBarBackgroundAlpha));
        var inactiveBackground = BrushColor("NovaChromeSurfaceAltBrush", background);
        var foreground = BrushColor("NovaAddressForegroundBrush", UiColor(255, 248, 234));
        var inactiveForeground = BrushColor("NovaTextMutedBrush", UiColor(195, 185, 165));
        var hoverBackground = BrushColor("NovaChromeSurfaceRaisedBrush", UiColor(34, 49, 58, translucent ? (byte)238 : (byte)255));
        var pressedBackground = BrushColor("NovaChromeStrokeBrush", UiColor(50, 69, 76, translucent ? (byte)244 : (byte)255));

        var titleBar = _appWindow.TitleBar;
        titleBar.BackgroundColor = WithAlpha(background, titleBarBackgroundAlpha);
        titleBar.InactiveBackgroundColor = WithAlpha(inactiveBackground, titleBarBackgroundAlpha);
        titleBar.ForegroundColor = foreground;
        titleBar.InactiveForegroundColor = inactiveForeground;
        titleBar.ButtonBackgroundColor = WithAlpha(background, titleBarBackgroundAlpha);
        titleBar.ButtonInactiveBackgroundColor = WithAlpha(inactiveBackground, titleBarBackgroundAlpha);
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = inactiveForeground;
        titleBar.ButtonHoverBackgroundColor = hoverBackground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedBackgroundColor = pressedBackground;
        titleBar.ButtonPressedForegroundColor = foreground;
    }

    private static Windows.UI.Color UiColor(byte r, byte g, byte b, byte a = 255) =>
        new() { A = a, R = r, G = g, B = b };

    private Windows.UI.Color BrushColor(string key, Windows.UI.Color fallback) =>
        RootShell.Resources[key] is SolidColorBrush brush ? brush.Color : fallback;

    private static Windows.UI.Color WithAlpha(Windows.UI.Color color, byte alpha) =>
        new() { A = alpha, R = color.R, G = color.G, B = color.B };

    private void ApplyNovaControlAccessibility(Control control, string? automationName = null)
    {
        control.FocusVisualPrimaryBrush = RootShell.Resources["NovaFocusBrush"] as Brush
            ?? new SolidColorBrush(UiColor(255, 230, 104));
        control.FocusVisualSecondaryBrush = RootShell.Resources["NovaFocusInnerBrush"] as Brush
            ?? new SolidColorBrush(UiColor(13, 24, 34));
        control.UseSystemFocusVisuals = true;

        if (!string.IsNullOrWhiteSpace(automationName))
        {
            AutomationProperties.SetName(control, automationName);
        }
    }

    private double AccessibilityBodyFontSize() => _uiSettings.AccessibilityLargeText ? 16 : 14;

    private double AccessibilitySecondaryFontSize() => _uiSettings.AccessibilityLargeText ? 14 : 12;

    private void ApplyAppIcon()
    {
        if (_appWindow is null) return;

        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "LumoraApp.ico");
            if (File.Exists(iconPath))
            {
                _appWindow.SetIcon(iconPath);
            }
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"App icon apply failed: {ex.Message}");
        }
    }

    private void ApplyTitleBarSafeArea()
    {
        var rightInset = _appWindow?.TitleBar.RightInset ?? 138;
        var safeRight = Math.Max(120, rightInset + 12);
        BrowserTabs.Margin = new Thickness(0, 0, safeRight, 0);
        _titleBarSafeRight = safeRight;
        UpdateTitleBarDragRegion();
    }

    private double _titleBarSafeRight = 138;

    // Zone de "drag" du titre : dynamique, calée sur l'espace vide de la barre
    // d'onglets (après le dernier onglet + un peu de marge pour le bouton "+"),
    // jusqu'aux boutons système. Sans ça, seule une mince bande fixe était
    // "draggable" et le double-clic pour maximiser ne marchait presque nulle part
    // dans la barre d'onglets — contrairement à Chrome/Edge où tout l'espace vide
    // de la barre d'onglets maximise au double-clic.
    private void UpdateTitleBarDragRegion()
    {
        if (_appWindow?.TitleBar is null || !ExtendsContentIntoTitleBar) return;

        try
        {
            var scale = Content?.XamlRoot?.RasterizationScale ?? 1.0;
            var rowHeight = TopTabsRow.ActualHeight > 0 ? TopTabsRow.ActualHeight : 38;
            var windowWidth = RootShell.ActualWidth;
            if (windowWidth <= 0) return;

            double leftEdge = 0;
            var lastTab = BrowserTabs.TabItems.OfType<TabViewItem>().LastOrDefault();
            if (lastTab is not null && lastTab.ActualWidth > 0)
            {
                var bounds = lastTab.TransformToVisual(RootShell)
                    .TransformBounds(new Windows.Foundation.Rect(0, 0, lastTab.ActualWidth, lastTab.ActualHeight));
                leftEdge = bounds.Right;
            }

            const double addTabButtonReserve = 56; // bouton "+" : jamais recouvert par la zone de drag
            var dragLeft = Math.Min(leftEdge + addTabButtonReserve, windowWidth - _titleBarSafeRight);
            var dragRight = Math.Max(dragLeft, windowWidth - _titleBarSafeRight);
            if (dragRight - dragLeft < 8) return;

            var rect = new RectInt32
            {
                X = (int)Math.Round(dragLeft * scale),
                Y = 0,
                Width = (int)Math.Round((dragRight - dragLeft) * scale),
                Height = (int)Math.Round(rowHeight * scale)
            };
            _appWindow.TitleBar.SetDragRectangles(new[] { rect });
        }
        catch { }
    }

}
