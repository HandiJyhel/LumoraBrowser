using Xunit;

namespace Lumora.Tests;

// Verrouille deux correctifs de focus clavier du 2026-07-22, trouves lors du
// tout premier test reel : il fallait cliquer dans la fenetre avant de
// pouvoir taper son code PIN (LoginOverlay est un Grid, pas un Control -
// Focus() dessus ne fait rien), et la molette de la souris restait muette
// apres la connexion (rien ne rendait le focus au WebView2 actif).
public sealed class KeyboardFocusRegressionTests
{
    [Fact]
    public void Chaque_panneau_de_connexion_recoit_le_focus_sur_un_vrai_control()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");

        Assert.Contains("private void FocusActiveLoginPanel(string mode)", code, StringComparison.Ordinal);
        Assert.Contains("FocusActiveLoginPanel(mode);", code, StringComparison.Ordinal);
        Assert.Contains("case \"create\":    ProfileNameBox.Focus(FocusState.Programmatic); break;", code, StringComparison.Ordinal);
        Assert.Contains("case \"password\":  LoginPasswordBox.Focus(FocusState.Programmatic); break;", code, StringComparison.Ordinal);
        Assert.Contains("case \"pin\":       PinDigit1Button.Focus(FocusState.Programmatic); break;", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Boutons_cibles_du_focus_ont_bien_un_nom_dans_le_xaml()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.Contains("x:Name=\"PinDigit1Button\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ChooseProfileLocationButton\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Connexion_reussie_rend_le_focus_a_l_onglet_actif()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");
        var dismiss = ExtractMethod(code, "private void DismissLoginOverlay()");

        Assert.Contains("CurrentTab()?.View?.Focus(FocusState.Programmatic);", dismiss, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_molette_raccorde_directement_les_scrollviewers_reels()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        Assert.Contains("private readonly HashSet<ScrollViewer> _manualWheelHookedScrollViewers", code, StringComparison.Ordinal);
        Assert.Contains("new PointerEventHandler(ScrollViewer_PointerWheelChanged)", code, StringComparison.Ordinal);
        Assert.Contains("handledEventsToo: true);", code, StringComparison.Ordinal);
        Assert.Contains("private void ScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_molette_raccorde_aussi_les_popups_et_flyouts()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");
        var bookmarks = ReadRepoFile("Lumora.WinUI", "MainWindow.BookmarksFlyouts.cs");
        var history = ReadRepoFile("Lumora.WinUI", "MainWindow.History.cs");
        var tabGroups = ReadRepoFile("Lumora.WinUI", "MainWindow.TabGroups.cs");

        Assert.Contains("VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot)", code, StringComparison.Ordinal);
        Assert.Contains("popup.Opened += PopupPointerSupport_Opened;", code, StringComparison.Ordinal);
        Assert.Contains("flyout.Opened += FlyoutPointerSupport_Opened;", code, StringComparison.Ordinal);
        Assert.Contains("HookFlyoutPointerSupport(flyout);", bookmarks, StringComparison.Ordinal);
        Assert.Contains("HookFlyoutPointerSupport(flyout);", history, StringComparison.Ordinal);
        Assert.Contains("HookFlyoutPointerSupport(flyout);", tabGroups, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_molette_rebranche_les_scrollviewers_quand_un_panneau_devient_visible()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        Assert.Contains("AttachScrollViewerPointerSupport(root);", code, StringComparison.Ordinal);
        Assert.Contains("AttachScrollViewerPointerSupport(visiblePanel);", code, StringComparison.Ordinal);
        Assert.Contains("private void AttachScrollViewerPointerSupport(DependencyObject root, ScrollViewer? activeWheelOwner = null)", code, StringComparison.Ordinal);
        Assert.Contains("_manualWheelSourceOwners", code, StringComparison.Ordinal);
        Assert.Contains("ScrollViewerDescendant_PointerWheelChanged", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_molette_utilise_le_helper_pur_pour_calculer_le_pas()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");
        var helper = ReadRepoFile("Lumora.WinUI", "WheelScrollMath.cs");

        Assert.Contains("WheelScrollMath.TryComputeNextVerticalOffset", code, StringComparison.Ordinal);
        Assert.Contains("private const double DefaultScrollStep = 56d;", helper, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(previousOffset - (Math.Sign(wheelDelta) * scrollStep), 0, scrollableHeight)", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void Parametres_ont_un_scrollviewer_explicitement_raccorde()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        Assert.Contains("x:Name=\"SettingsContentScrollViewer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollMode=\"Enabled\"", xaml, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollMode=\"Disabled\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AttachScrollViewerPointerSupport(visiblePanel);", code, StringComparison.Ordinal);
        Assert.Contains("viewer.PointerEntered += ScrollViewer_PointerEntered;", code, StringComparison.Ordinal);
        Assert.Contains("if (_manualWheelHookedScrollViewers.Add(hookedViewer))", code, StringComparison.Ordinal);
        Assert.Contains("HookScrollViewerWheelSource(wheelSource, activeWheelOwner);", code, StringComparison.Ordinal);
        Assert.Contains("TryApplyScrollViewerWheel(viewer, e, wheelSource.GetType().Name);", code, StringComparison.Ordinal);
        Assert.Contains("ResetSettingsScrollPosition();", code, StringComparison.Ordinal);
        Assert.Contains("private void ResetSettingsScrollPosition()", code, StringComparison.Ordinal);
        Assert.Contains("SettingsContentScrollViewer.ChangeView(null, 0, null, true);", code, StringComparison.Ordinal);
        Assert.Contains("SettingsContentScrollViewer.Focus(FocusState.Pointer);", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnostic_bas_niveau_molette_sous_classe_le_wndproc_sans_changer_le_comportement()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");
        var chrome = ReadRepoFile("Lumora.WinUI", "MainWindow.WindowChrome.cs");

        Assert.Contains("HookRawMouseWheelDiagnostics(hwnd);", code, StringComparison.Ordinal);
        Assert.Contains("private const uint WmMousewheel = 0x020A;", chrome, StringComparison.Ordinal);
        Assert.Contains("private const uint WmPointerwheel = 0x024E;", chrome, StringComparison.Ordinal);
        Assert.Contains("private void HookRawMouseWheelDiagnostics(nint hwnd)", chrome, StringComparison.Ordinal);
        Assert.Contains("_originalWndProc = SetWindowLongPtr(hwnd, GwlpWndproc, newProc);", chrome, StringComparison.Ordinal);
        Assert.Contains("return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);", chrome, StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnostic_racine_du_panneau_parametres_trace_la_molette_sans_changer_le_comportement()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        Assert.Contains("private void SettingsPanel_RootWheelDiagnostics(object sender, PointerRoutedEventArgs e)", code, StringComparison.Ordinal);
        Assert.Contains("SettingsPanel.AddHandler(", code, StringComparison.Ordinal);
        Assert.Contains("new PointerEventHandler(SettingsPanel_RootWheelDiagnostics)", code, StringComparison.Ordinal);
        Assert.Contains("browserPanelVisibility={BrowserPanel.Visibility}", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnostic_racine_parametres_trace_aussi_l_etat_du_focus()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        Assert.Contains("var focusedElement = FocusManager.GetFocusedElement(SettingsPanel.XamlRoot) as FrameworkElement;", code, StringComparison.Ordinal);
        Assert.Contains("scrollViewerFocusState={SettingsContentScrollViewer.FocusState}", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnostic_distingue_le_focus_win32_du_focus_xaml()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");
        var chrome = ReadRepoFile("Lumora.WinUI", "MainWindow.WindowChrome.cs");

        Assert.Contains("TraceWin32FocusState($\"survol ScrollViewer {viewer.Name}\");", code, StringComparison.Ordinal);
        Assert.Contains("TraceWin32FocusState(\"signal molette recu par le panneau Parametres\");", code, StringComparison.Ordinal);
        Assert.Contains("private static extern nint GetForegroundWindow();", chrome, StringComparison.Ordinal);
        Assert.Contains("private static extern nint GetFocus();", chrome, StringComparison.Ordinal);
        Assert.Contains("private void TraceWin32FocusState(string reason)", chrome, StringComparison.Ordinal);
    }

    [Fact]
    public void Molette_parametres_applique_directement_au_scrollviewer_quand_non_traitee()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        Assert.Contains("if (!e.Handled)", code, StringComparison.Ordinal);
        Assert.Contains("TryApplyScrollViewerWheel(SettingsContentScrollViewer, e, \"root-fallback-viewer-direct\");", code, StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Signature introuvable : {signature}");

        var braceOpen = source.IndexOf('{', start);
        var depth = 0;
        var i = braceOpen;
        for (; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0) break;
            }
        }

        return source[start..(i + 1)];
    }

    private static string ReadRepoFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Fichier projet introuvable: {string.Join(Path.DirectorySeparatorChar, segments)}");
    }
}
