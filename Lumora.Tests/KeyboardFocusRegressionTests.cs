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

    // FocusState.Pointer et non Programmatic depuis le 2026-08-02 : Programmatic
    // reste au niveau de l'enveloppe XAML sans se propager jusqu'a Chromium, la
    // molette restait donc muette juste apres le deverrouillage malgre ce
    // rattrapage.
    [Fact]
    public void Connexion_reussie_rend_le_focus_a_l_onglet_actif()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");
        var dismiss = ExtractMethod(code, "private void DismissLoginOverlay()");

        Assert.Contains("CurrentTab()?.View?.Focus(FocusState.Pointer);", dismiss, StringComparison.Ordinal);
    }

    // Le correctif MainWindow du 2026-08-02 signalait explicitement 3 occurrences
    // identiques dans LumoraIncognitoWindow.xaml.cs, laissees de cote ce jour-la
    // ("hors perimetre"). Traitees ici, meme raison : Programmatic ne se propage
    // pas jusqu'a Chromium, la molette restait muette en Incognito.
    [Fact]
    public void Incognito_rend_le_focus_au_webview_avec_pointer_pas_programmatic()
    {
        var code = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml.cs");

        Assert.Contains("tab.View.Focus(FocusState.Pointer);", code, StringComparison.Ordinal);
        Assert.Contains("view.PointerEntered += (_, _) => view.Focus(FocusState.Pointer);", code, StringComparison.Ordinal);
        Assert.Contains("_currentTab?.View.Focus(FocusState.Pointer);", code, StringComparison.Ordinal);
        Assert.DoesNotContain("FocusState.Programmatic", code, StringComparison.Ordinal);
    }

    // Re-ecrit le 2026-08-02 : l'ancien module rattachait un handler d'application
    // par ScrollViewer/descendant (source de bugs "molette cassee par
    // intermittence" - controle intermediaire qui marque l'evenement traite,
    // descendant regenere jamais rattache...). Remplace par UN SEUL module qui
    // fait un hit-test geometrique frais a chaque evenement molette : rien a
    // rater, rien a rattacher a l'avance.
    [Fact]
    public void Module_molette_centralise_le_defilement_par_hit_test()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        Assert.Contains("private readonly HashSet<UIElement> _wheelFallbackHookedRoots", code, StringComparison.Ordinal);
        Assert.Contains("private void HookWheelFallbackRoot(UIElement root)", code, StringComparison.Ordinal);
        Assert.Contains("private void ApplyWheelFallback(UIElement hitTestRoot, PointerRoutedEventArgs e)", code, StringComparison.Ordinal);
        Assert.Contains("VisualTreeHelper.FindElementsInHostCoordinates(point, hitTestRoot)", code, StringComparison.Ordinal);
        Assert.Contains("if (candidate is ScrollViewer { VerticalScrollBarVisibility: not ScrollBarVisibility.Disabled } scrollViewer)", code, StringComparison.Ordinal);
        Assert.Contains("TryApplyScrollViewerWheel(scrollViewer, e, \"hit-test-root\");", code, StringComparison.Ordinal);
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
    public void Module_molette_rebranche_le_focus_survol_quand_un_panneau_devient_visible()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        Assert.Contains("AttachScrollViewerPointerSupport(root);", code, StringComparison.Ordinal);
        Assert.Contains("AttachScrollViewerPointerSupport(visiblePanel);", code, StringComparison.Ordinal);
        Assert.Contains("private void AttachScrollViewerPointerSupport(DependencyObject root)", code, StringComparison.Ordinal);
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
        Assert.Contains("HookWheelFallbackRoot(ContentHost);", code, StringComparison.Ordinal);
        Assert.Contains("ResetSettingsScrollPosition();", code, StringComparison.Ordinal);
        Assert.Contains("private void ResetSettingsScrollPosition()", code, StringComparison.Ordinal);
        Assert.Contains("SettingsContentScrollViewer.ChangeView(null, 0, null, true);", code, StringComparison.Ordinal);
        Assert.Contains("SettingsContentScrollViewer.Focus(FocusState.Pointer);", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnostic_bas_niveau_molette_sous_classe_toujours_le_wndproc_et_relaie_vers_loriginal()
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

    // Correctif du 2026-08-07 : le sous-classement WndProc ci-dessus n'est
    // plus un pur diagnostic (contrairement au test precedent, dont le nom
    // documentait explicitement "sans changer le comportement" jusqu'a ce
    // jour). Root cause reelle de l'intermittence molette identifiee ce
    // jour-la : Windows route WM_MOUSEWHEEL/WM_POINTERWHEEL vers le HWND qui
    // a le focus CLAVIER natif, pas vers celui survole par la souris - tous
    // les correctifs precedents (FocusState.Pointer, hit-test XAML) tentaient
    // de faire suivre ce focus jusqu'au HWND enfant reel de WebView2 sans
    // jamais le controler directement. Ce module fait un hit-test Win32 natif
    // (ChildWindowFromPointEx, independant du focus) et redirige
    // explicitement le message vers le HWND enfant reellement sous le
    // curseur - la meme logique que le "scroll sous la souris" des
    // navigateurs. N'affecte que les vrais HWND enfants (WebView2) : les
    // panneaux XAML natifs (Parametres, Modules...) n'ont pas de HWND propre
    // et restent geres par ApplyWheelFallback (hit-test XAML, inchange).
    [Fact]
    public void Molette_native_redirige_le_message_vers_le_hwnd_enfant_sous_le_curseur()
    {
        var chrome = ReadRepoFile("Lumora.WinUI", "MainWindow.WindowChrome.cs");
        var wndProc = ExtractMethod(chrome, "private nint RawWheelDiagnosticsWndProc(nint hWnd, uint msg, nint wParam, nint lParam)");

        Assert.Contains("private static extern nint ChildWindowFromPointEx(nint hWndParent, Win32Point point, uint uFlags);", chrome, StringComparison.Ordinal);
        Assert.Contains("private static extern bool ScreenToClient(nint hWnd, ref Win32Point lpPoint);", chrome, StringComparison.Ordinal);
        Assert.Contains("private static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);", chrome, StringComparison.Ordinal);
        Assert.Contains("private static nint FindDeepestChildAtScreenPoint(nint root, int screenX, int screenY)", chrome, StringComparison.Ordinal);

        Assert.Contains("var target = FindDeepestChildAtScreenPoint(hWnd, screenX, screenY);", wndProc, StringComparison.Ordinal);
        Assert.Contains("if (target != nint.Zero && target != hWnd)", wndProc, StringComparison.Ordinal);
        Assert.Contains("SendMessage(target, msg, wParam, lParam);", wndProc, StringComparison.Ordinal);
    }

    // Verrouille un crash REEL rencontre en verification le 2026-08-07
    // (STATUS_STACK_OVERFLOW 0xc00000fd, confirme par l'Observateur
    // d'evenements Windows) : WebView2 applique la convention Win32
    // documentee et renvoie WM_MOUSEWHEEL a son parent (nous) quand il ne le
    // traite pas, ce qui reentrait dans le meme WndProc et redeclenchait le
    // meme forward - boucle synchrone (SendMessage bloque) jusqu'a deborder
    // la pile en ~600 allers-retours. Sans ce garde-fou, toute reintroduction
    // du forward (meme correctement ecrit par ailleurs) recree ce crash.
    [Fact]
    public void Molette_native_ne_boucle_pas_quand_lenfant_renvoie_le_message_a_son_parent()
    {
        var chrome = ReadRepoFile("Lumora.WinUI", "MainWindow.WindowChrome.cs");
        var wndProc = ExtractMethod(chrome, "private nint RawWheelDiagnosticsWndProc(nint hWnd, uint msg, nint wParam, nint lParam)");

        Assert.Contains("private bool _forwardingWheelMessage;", chrome, StringComparison.Ordinal);
        Assert.Contains("if (_forwardingWheelMessage)", wndProc, StringComparison.Ordinal);
        Assert.Contains("_forwardingWheelMessage = true;", wndProc, StringComparison.Ordinal);
        Assert.Contains("_forwardingWheelMessage = false;", wndProc, StringComparison.Ordinal);

        // Le SendMessage vers l'enfant doit se trouver DANS le bloc protege par
        // le drapeau (try/finally), pas avant qu'il ne soit leve - sinon la
        // reentrance survient avant que le garde ne soit actif.
        var flagIndex = wndProc.IndexOf("_forwardingWheelMessage = true;", StringComparison.Ordinal);
        var sendIndex = wndProc.IndexOf("SendMessage(target, msg, wParam, lParam);", StringComparison.Ordinal);
        var resetIndex = wndProc.IndexOf("_forwardingWheelMessage = false;", StringComparison.Ordinal);
        Assert.True(flagIndex >= 0 && sendIndex > flagIndex && resetIndex > sendIndex,
            "Le SendMessage doit se trouver entre la levee et la retombee du drapeau anti-reentrance.");
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

    // Le 2026-08-02, l'application du defilement a ete retiree de ce diagnostic
    // (elle vit desormais UNIQUEMENT dans ApplyWheelFallback, module unique
    // pose sur ContentHost et sur chaque popup/flyout) - la garder ici EN PLUS
    // aurait double le defilement (les deux handlers auraient applique un pas
    // chacun sur le meme ScrollViewer a chaque tick de molette).
    [Fact]
    public void Diagnostic_racine_parametres_ne_double_plus_le_defilement()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");
        var diagnostic = ExtractMethod(code, "private void SettingsPanel_RootWheelDiagnostics(object sender, PointerRoutedEventArgs e)");

        Assert.DoesNotContain("TryApplyScrollViewerWheel", diagnostic, StringComparison.Ordinal);
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
