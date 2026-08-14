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

        var highContrast = _uiSettings.AccessibilityHighContrast;
        var translucent = IsTranslucentChromeEnabled();
        var titleBarBackgroundAlpha = translucent ? (byte)226 : (byte)255;
        var background = BrushColor("NovaChromeSurfaceBrush", UiColor(20, 32, 42, titleBarBackgroundAlpha));
        var inactiveBackground = BrushColor("NovaChromeSurfaceAltBrush", background);
        // NovaChromeButtonForegroundBrush (pas NovaAddressForegroundBrush) :
        // c'est deja le jeton utilise par les AUTRES boutons-icones de la
        // chrome (styles NovaChromeIconButtonStyle, MainWindow.xaml:259-280),
        // et surtout le seul des deux a rester clair en contraste eleve.
        // NovaAddressForegroundBrush y est NOIR (pense pour texte sur fond
        // blanc de la barre d'adresse) - utilise ici, il rendait les icones
        // minimiser/agrandir/fermer entierement invisibles au repos (texte
        // noir sur fond NovaChromeSurfaceBrush lui-meme noir en contraste
        // eleve), pas seulement au survol. Trouve par capture d'ecran reelle
        // en verifiant le correctif de survol ci-dessous.
        var foreground = BrushColor("NovaChromeButtonForegroundBrush", UiColor(255, 248, 234));
        var inactiveForeground = BrushColor("NovaTextMutedBrush", UiColor(195, 185, 165));
        var hoverBackground = BrushColor("NovaChromeSurfaceRaisedBrush", UiColor(34, 49, 58, translucent ? (byte)238 : (byte)255));
        var pressedBackground = BrushColor("NovaChromeStrokeBrush", UiColor(50, 69, 76, translucent ? (byte)244 : (byte)255));
        var hoverForeground = foreground;
        var pressedForeground = foreground;

        // Contraste eleve : le fond (NovaChromeSurfaceRaisedBrush) et le fond
        // "survol" sont tous les deux noirs (UiColor(0,0,0) dans
        // ApplyAccessibilitySettings) - le survol des boutons systeme etait
        // donc invisible en plus du probleme de premier plan ci-dessus. On
        // reprend les deux couleurs deja utilisees ailleurs comme langage
        // contraste-eleve de l'app (accent jaune ambre de
        // NovaModuleHubAccentBrush, jaune pur de NovaFocusBrush/Focus) pour
        // donner 3 etats vraiment distincts : fond noir/texte blanc, survol
        // jaune ambre/texte noir, appui jaune pur/texte noir.
        if (highContrast)
        {
            hoverBackground = UiColor(255, 213, 0);
            hoverForeground = UiColor(0, 0, 0);
            pressedBackground = UiColor(255, 255, 0);
            pressedForeground = UiColor(0, 0, 0);
        }

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
        titleBar.ButtonHoverForegroundColor = hoverForeground;
        titleBar.ButtonPressedBackgroundColor = pressedBackground;
        titleBar.ButtonPressedForegroundColor = pressedForeground;

        // Palette C du Contraste renforce (2026-08-08) : meme gris de repos
        // que MainWindow.SettingsTheme.cs (hcRestBorder, 190/190/190) - le
        // lisere exterieur de la fenetre suit la meme regle que toutes les
        // autres bordures au repos, plus de blanc pur ici non plus.
        ApplyWindowBorderColor(highContrast ? UiColor(190, 190, 190) : pressedBackground);
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
            var windowWidth = RootShell.ActualWidth;
            if (windowWidth <= 0) return;

            // Bande de titre aplatie (2026-08-09) : TopTabsRow.ActualHeight tombe a 0
            // quand la ligne est vraiment supprimee (Classique + onglets verticaux,
            // voir ApplyVerticalTabsLayout) - a distinguer du cas horizontal, ou le
            // calcul ci-dessous (base sur le dernier onglet de BrowserTabs) reste
            // valable tel quel. En mode aplati, NavigationRow n'a PAS d'espace vide :
            // elle est pleine de vrais boutons (adresse, bouclier, favoris, modules...)
            // jusqu'a la marge de securite - calquer le calcul sur cette meme ligne
            // aurait recouvert ces boutons (cause du tout premier echec historique).
            // Seule la marge de securite elle-meme (deja hors de portee des boutons
            // reels, meme reserve que les boutons systeme) est une zone de drag sure ;
            // si elle est trop fine, aucune zone n'est posee plutot que d'empieter -
            // consequence assumee : impossible de deplacer la fenetre en cliquant la
            // ligne d'adresse dans ce mode precis, seule la reserve systeme le permet
            // encore (double-clic sur la barre de titre au sens Windows classique).
            var flattened = TopTabsRow.ActualHeight <= 0;
            var rowHeight = flattened
                ? NavigationRow.ActualHeight
                : (TopTabsRow.ActualHeight > 0 ? TopTabsRow.ActualHeight : 52);

            double dragLeft;
            double dragRight;
            if (flattened)
            {
                dragRight = windowWidth;
                dragLeft = windowWidth - _titleBarSafeRight;
            }
            else
            {
                double leftEdge = 0;
                var lastTab = BrowserTabs.TabItems.OfType<TabViewItem>().LastOrDefault();
                if (lastTab is not null && lastTab.ActualWidth > 0)
                {
                    var bounds = lastTab.TransformToVisual(RootShell)
                        .TransformBounds(new Windows.Foundation.Rect(0, 0, lastTab.ActualWidth, lastTab.ActualHeight));
                    leftEdge = bounds.Right;
                }

                const double addTabButtonReserve = 56; // bouton "+" : jamais recouvert par la zone de drag
                dragLeft = Math.Min(leftEdge + addTabButtonReserve, windowWidth - _titleBarSafeRight);
                dragRight = Math.Max(dragLeft, windowWidth - _titleBarSafeRight);
            }

            // Exclusion de ModulesQuickBar (2026-08-14, bug reel confirme par
            // trace de diagnostic - voir MEMORY.md) : en layout aplati
            // (theme Classique + onglets verticaux, TopTabsRow.ActualHeight=0),
            // NavigationRow demarre a Y=0, la MEME rangee que cette zone de
            // drag - et ModulesQuickBar (barre d'outils, colonnes 14-19 de
            // NavigationToolbar, jusqu'au bord droit reel) tombait alors
            // DANS la reserve _titleBarSafeRight. Consequence observee :
            // Windows captait le pointeur pour deplacer la fenetre des qu'un
            // glissement de bouton depassait quelques pixels (le seuil de
            // deplacement de fenetre de Windows, independant du notre),
            // rendant tout glisser/reorganisation de la barre d'outils
            // impossible - PointerCaptureLost se declenchait silencieusement
            // en quelques millisecondes a chaque tentative, meme avec la
            // capture posee sur le panneau (correctif precedent). Inoffensif
            // en layout normal (la zone de drag reste dans TopTabsRow, une
            // autre rangee que ModulesQuickBar) : le calcul ci-dessous ne
            // clampe que si les deux zones se chevauchent reellement.
            if (ModulesQuickBar.ActualWidth > 0 && ModulesQuickBar.ActualHeight > 0)
            {
                var modulesBounds = ModulesQuickBar.TransformToVisual(RootShell)
                    .TransformBounds(new Windows.Foundation.Rect(0, 0, ModulesQuickBar.ActualWidth, ModulesQuickBar.ActualHeight));
                if (modulesBounds.Top < rowHeight)
                {
                    dragLeft = Math.Max(dragLeft, modulesBounds.Right);
                }
            }

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

    // ── Molette : routage natif vers le vrai HWND enfant sous le curseur ─────
    // Le diagnostic du 2026-07-25 (ci-dessus) confirme que WM_MOUSEWHEEL/
    // WM_POINTERWHEEL atteignent bien la fenetre Lumora, mais Windows les
    // route vers le HWND qui a le focus CLAVIER natif - pas vers celui
    // survole par la souris. Tous les correctifs tentes jusqu'ici
    // (FocusState.Pointer, hit-test XAML sur ScrollViewer...) essayaient de
    // faire suivre ce focus natif jusqu'au HWND enfant reel de WebView2
    // (controle "windowed" - vrai HWND enfant, contrairement au reste de
    // l'UI XAML qui vit dans un seul HWND via DirectComposition) sans jamais
    // le controler directement : XAML expose Focus(), pas
    // CoreWebView2Controller.MoveFocus (verifie absent de ce SDK, voir
    // BrowserHost_PointerEntered dans MainWindow.xaml.cs). Plutot que de
    // continuer a esperer que le focus natif suive, on route explicitement
    // le message recu par la fenetre principale vers le HWND enfant
    // reellement sous le curseur au moment du geste (ChildWindowFromPointEx,
    // hit-test frais a chaque evenement) - independant du focus, meme
    // principe que le "scroll sous la souris" des navigateurs et de
    // l'Explorateur de fichiers. Go utilisateur du 2026-08-07 : "prendre tout
    // ce qui a deja ete fait et s'arranger... meme si tu dois creer quelque
    // chose de specifique" apres un historique de correctifs XAML jamais
    // rectifie - ce module s'attaque a une couche jamais touchee jusqu'ici
    // (le HWND natif cible), independamment de tout ce qui a ete tente cote
    // XAML (conserve intact, aucune regression attendue sur les panneaux
    // natifs qui, eux, n'ont pas de HWND propre et ne sont donc jamais
    // redirige par ce module).
    private const uint CwpSkipinvisible = 0x0001;
    private const uint CwpSkipdisabled = 0x0002;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Win32Point
    {
        public int X;
        public int Y;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint ChildWindowFromPointEx(nint hWndParent, Win32Point point, uint uFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ScreenToClient(nint hWnd, ref Win32Point lpPoint);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    // Garde-fou anti-reentrance - CRASH REEL rencontre en verification (2026-08-07,
    // STATUS_STACK_OVERFLOW 0xc00000fd, confirme par l'Observateur d'evenements) :
    // quand WebView2/Chromium ne traite pas le message qu'on lui envoie (ex. page
    // sans contenu scrollable a cet endroit precis), il applique la convention
    // Win32 documentee par Microsoft pour WM_MOUSEWHEEL - le renvoyer via
    // SendMessage a SA fenetre PARENTE (nous) si lui-meme ne le traite pas. Sans
    // garde, ce retour reentrait dans ce meme WndProc, qui refaisait le meme
    // hit-test (meme point ecran, meme resultat) et renvoyait de nouveau au meme
    // enfant - boucle synchrone (SendMessage bloque) qui a fait deborder la pile
    // en ~600 allers-retours. Le drapeau coupe le hit-test/forward des la
    // reentrance : le message "rebondi" repart alors simplement vers
    // CallWindowProc (comportement par defaut), sans nouveau forward.
    private bool _forwardingWheelMessage;

    // ChildWindowFromPointEx ne regarde qu'un seul niveau d'enfants DIRECTS :
    // WebView2 peut imbriquer plusieurs HWND (widget host, puis la fenetre de
    // rendu Chromium elle-meme), donc on redescend tant qu'un enfant plus
    // profond existe sous le meme point ecran. Limite de profondeur en filet
    // de securite (jamais cense boucler, mais un HWND qui se retournerait
    // lui-meme ne doit pas figer la fenetre).
    private static nint FindDeepestChildAtScreenPoint(nint root, int screenX, int screenY)
    {
        var current = root;
        for (var depth = 0; depth < 8; depth++)
        {
            var clientPoint = new Win32Point { X = screenX, Y = screenY };
            if (!ScreenToClient(current, ref clientPoint))
            {
                break;
            }

            var child = ChildWindowFromPointEx(current, clientPoint, CwpSkipinvisible | CwpSkipdisabled);
            if (child == nint.Zero || child == current)
            {
                break;
            }

            current = child;
        }

        return current;
    }

    private nint RawWheelDiagnosticsWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg is WmMousewheel or WmPointerwheel)
        {
            var messageName = msg == WmMousewheel ? "WM_MOUSEWHEEL" : "WM_POINTERWHEEL";
            var delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
            WinUiRuntimeTrace.Write($"Diagnostic bas niveau molette : {messageName} brut recu par la fenetre (delta={delta}).");

            if (_forwardingWheelMessage)
            {
                WinUiRuntimeTrace.Write($"Molette : {messageName} rebondi depuis un enfant natif (non traite) - pas de nouveau forward.");
            }
            else
            {
                // lParam porte les coordonnees ECRAN pour WM_MOUSEWHEEL comme pour
                // WM_POINTERWHEEL (documentation Win32) : aucune conversion a
                // faire avant de descendre l'arbre des HWND enfants.
                var screenX = (short)(lParam.ToInt64() & 0xFFFF);
                var screenY = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                var target = FindDeepestChildAtScreenPoint(hWnd, screenX, screenY);
                if (target != nint.Zero && target != hWnd)
                {
                    WinUiRuntimeTrace.Write($"Molette : {messageName} redirige vers le HWND enfant natif sous le curseur ({target}).");
                    _forwardingWheelMessage = true;
                    try
                    {
                        SendMessage(target, msg, wParam, lParam);
                    }
                    finally
                    {
                        _forwardingWheelMessage = false;
                    }
                }
            }
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
