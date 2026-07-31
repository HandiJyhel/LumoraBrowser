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

        ApplyWindowBorderColor(pressedBackground);
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    private const int DwmwaBorderColor = 34;

    // Windows 11 peint par defaut le liseré de fenetre avec la couleur
    // d'accentuation SYSTEME (reglage utilisateur hors de Lumora), pas avec
    // le theme clair/sombre de l'app - sur une machine a accent systeme vif
    // (orange, etc.), ca produit un liseré qui jure avec le chrome Lumora et
    // se lit a tort comme un bug de theme clair. On force ce liseré sur la
    // meme couleur que la bordure du chrome (deja theme-consciente) plutot
    // que de laisser l'OS choisir a la place de l'app.
    private void ApplyWindowBorderColor(Windows.UI.Color color)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var colorRef = (color.B << 16) | (color.G << 8) | color.R;
            DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref colorRef, sizeof(int));
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Window border color skipped: {error.GetType().Name}");
        }
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
            var rowHeight = TopTabsRow.ActualHeight > 0 ? TopTabsRow.ActualHeight : 52;
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
            if (_chromeLayoutStyle == "identitySpine")
            {
                // La colonne identitaire recouvre les premiers pixels de cette
                // ligne (marque Lumora) : sans ce plancher, la zone de drag
                // systeme les recouvre aussi et absorbe les clics destines a
                // la marque.
                dragLeft = Math.Max(dragLeft, IdentitySpineHost.ActualWidth + 4);
            }
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

    // Diagnostic bas niveau temporaire (Go utilisateur du 2026-07-25) : 8
    // correctifs XAML successifs sur la molette (routage ScrollViewer, focus,
    // dedoublonnage...) n'ont jamais ete confirmes par un geste physique reel -
    // seulement par des tests source ou par UI Automation (ScrollPattern), qui
    // prouvent que le ScrollViewer sait defiler mais pas que le message Windows
    // de la molette l'atteint. Avant un 9e patch XAML a l'aveugle, on sous-classe
    // le WndProc de la fenetre en lecture seule (relai systematique a
    // CallWindowProc, aucun comportement change) pour tracer si/quand
    // WM_MOUSEWHEEL (souris classique) ou WM_POINTERWHEEL (pile Pointer,
    // touchpads/peripheriques recents) atteint reellement la fenetre pendant un
    // test physique dans un panneau natif comme Parametres.
    private const int GwlpWndproc = -4;
    private const uint WmMousewheel = 0x020A;
    private const uint WmPointerwheel = 0x024E;

    private delegate nint RawWndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    private nint _originalWndProc;
    private RawWndProcDelegate? _rawWheelDiagnosticsWndProc;
    private nint _diagnosticsMainHwnd;

    // SetWindowLongPtr(nint, int, nint) est deja declare dans
    // MainWindow.SettingsTheme.cs (RemoveWindowLayeredAlpha) - reutilise ici,
    // une classe partielle ne peut pas redeclarer la meme signature.
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);

    // Diagnostic supplementaire (Go utilisateur du 2026-07-25, suite a un
    // test ou AUCUN signal molette n'a ete detecte a aucun niveau, meme pas
    // WM_MOUSEWHEEL/WM_POINTERWHEEL bruts, alors qu'un test precedent en
    // avait bien capte) : distinguer le focus Windows classique (quelle
    // fenetre Win32 est reellement ciblee par les entrees) du focus XAML
    // logique (FocusState) qu'on manipule depuis le debut. Ce sont deux
    // notions liees mais distinctes ; si Windows ne considere pas Lumora
    // comme la fenetre active/focus au moment du geste, la molette ne peut
    // atteindre ni notre hook bas niveau ni le XAML, quel que soit le code
    // applicatif.
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint GetFocus();

    private void HookRawMouseWheelDiagnostics(nint hwnd)
    {
        try
        {
            _diagnosticsMainHwnd = hwnd;
            _rawWheelDiagnosticsWndProc = RawWheelDiagnosticsWndProc;
            var newProc = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_rawWheelDiagnosticsWndProc);
            _originalWndProc = SetWindowLongPtr(hwnd, GwlpWndproc, newProc);
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Diagnostic bas niveau molette : sous-classement WndProc impossible ({error.GetType().Name}).");
        }
    }

    private nint RawWheelDiagnosticsWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg is WmMousewheel or WmPointerwheel)
        {
            var messageName = msg == WmMousewheel ? "WM_MOUSEWHEEL" : "WM_POINTERWHEEL";
            var delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
            WinUiRuntimeTrace.Write($"Diagnostic bas niveau molette : {messageName} brut recu par la fenetre (delta={delta}).");
        }

        return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }

    private void TraceWin32FocusState(string reason)
    {
        var foreground = GetForegroundWindow();
        var focus = GetFocus();
        WinUiRuntimeTrace.Write(
            $"Diagnostic focus Win32 ({reason}) : hwndPrincipalLumora={_diagnosticsMainHwnd} foregroundWindow={foreground} estLumora={foreground == _diagnosticsMainHwnd} focusWin32={focus} estLumora={focus == _diagnosticsMainHwnd}.");
    }
}
