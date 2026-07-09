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
using PulseBrowser.Privacy;
using PulseBrowser.Privacy.NetworkBlocker;
using PulseBrowser.Privacy.TelemetryBlocker;
using PulseBrowser.Privacy.ParameterCleaner;
using PulseBrowser.Privacy.HttpsEnforcer;
using PulseBrowser.Privacy.CnameUncloaker;
using PulseBrowser.Privacy.CosmeticFilter;
using PulseBrowser.Privacy.ConsentManager;
using PulseBrowser.WinUI.Credentials;
using PulseBrowser.WinUI.PasswordManager;

namespace PulseBrowser.WinUI;

public sealed partial class MainWindow : Window
{
    private const string Version = "0.48.1-dev";
    private const double VerticalTabsCompactWidth = 50;
    private const double VerticalTabsMinExpandedWidth = 120;
    private const double VerticalTabsDefaultWidth = 210;
    private const double VerticalTabsMaxWidth = 320;

    private readonly PulseProfilePaths _profile = PulseProfilePaths.Default();
    private readonly BookmarkStore _bookmarks;
    private readonly WebAppStore _webApps;
    private readonly ObservableCollection<BookmarkListItem> _bookmarkItems = new();
    private readonly ObservableCollection<BookmarkListItem> _bookmarkFolderItems = new();
    private readonly List<BrowserTabState> _tabs = new();
    private readonly List<BrowserImportSource> _importSources = new();
    private readonly Dictionary<string, string> _faviconCache = new(StringComparer.OrdinalIgnoreCase);
    private UiSettings _uiSettings = UiSettings.Default();
    private List<BookmarkNode> _allBookmarkNodes = new();
    // Vue web de l'onglet ACTIF (chaque onglet possède son propre WebView2).
    private WebView2? _browserView;
    // La surface navigateur n'est prête qu'après l'activation de la fenêtre :
    // aucune vue web ne doit être créée avant (crash WebView2 au démarrage sinon).
    private bool _browserSurfaceReady;
    private bool _webViewDisabled;
    private string _currentBookmarkFolderId = BookmarkStore.ToolbarRootId;
    private string _bookmarkSearch = string.Empty;
    private bool _isProgrammaticNavigation;
    private bool _suppressUiSettingsSave = true;
    private bool _suppressTabNavigation;
    private bool _isGuestMode;
    private bool _verticalTabsEnabled;
    private bool _verticalTabsCompact;
    private bool _compactModeEnabled;
    private Microsoft.UI.Windowing.AppWindow? _appWindow;
    private double _verticalTabsExpandedWidth = VerticalTabsDefaultWidth;
    private int _nextTabId = 1;
    private readonly HistoryStore _history;
    private readonly ObservableCollection<HistoryListItem> _historyItems = new();
    private readonly ObservableCollection<CommandPaletteItem> _commandPaletteItems = new();
    private readonly List<DownloadEntry> _downloads = new();
    private string _historySearch = string.Empty;
    private bool _suppressTabSave = true;
    private readonly VaultStore _vault;
    private readonly PasswordManagerService _passwordManager;
    private readonly PasswordManagerInteractionService _passwordManagerInteraction;
    private readonly CredentialService _credentialService = new();
    private (string Origin, string Username, string Password, string LoginUrl)? _pendingCredential;
    private VaultCredential? _pendingAutoFill;
    private readonly List<PasskeyEntry> _passkeys = new();
    private UserProfile? _userProfile;
    private UserProfile? _pendingUserProfile;
    private List<PulseProfileEntry> _profileEntries = new();
    private string _pinBuffer = string.Empty;
    private int _pinFailCount;
    private string? _pendingProfileDir;
    private string? _pendingProfileId;
    // Mot de passe de création retenu le temps de finaliser le profil, pour clé
    // le coffre au même mot de passe (couplage session ↔ coffre). Effacé aussitôt.
    private string? _pendingProfilePassword;
    // PIN de création retenu de même, pour activer le déverrouillage du coffre par PIN.
    private string? _pendingProfilePin;
    private string? _pendingRecoveryKey;
    private List<MigrationBrowserEntry> _migrationEntries = new();
    private bool _restartRequired;
    private DispatcherTimer? _sessionTimer;
    private readonly PrivacyEngine _privacy = new();
    private NetworkBlockerModule? _networkBlocker;
    private TelemetryBlockerModule? _telemetryBlocker;
    private CosmeticFilterModule? _cosmeticFilter;
    private ConsentManagerModule? _consentModule;
    // Identifiants des scripts privacy enregistrés, PAR moteur (un WebView2 par onglet).
    private readonly Dictionary<CoreWebView2, string> _cosmeticScriptIds = new();
    private readonly Dictionary<CoreWebView2, string> _consentScriptIds = new();
    // Migration Chromium → vault.pulse déjà effectuée pour ce lancement.
    private bool _browserPasswordsMigrated;
    private string? _currentPageDomain;
    private bool _suppressShieldToggle;
    private bool _suppressSiteControlTrustToggle;
    private static readonly System.Net.Http.HttpClient FaviconHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public MainWindow()
    {
        WinUiRuntimeTrace.Write("MainWindow constructor start");

        // WebView2 stocke ses cookies/sessions dans le profil Pulse plutôt que dans un
        // dossier collé à l'exe (PulseBrowser.WinUI.exe.WebView2). Conséquence : changer
        // ou supprimer un profil déconnecte réellement des sites, et la purge est complète.
        // Doit être fait AVANT toute création de moteur WebView2 dans le process. Partagé
        // avec les fenêtres d'application web (WebView2Bootstrap), qui doivent pointer
        // vers le même profil pour partager cookies et sessions.
        WebView2Bootstrap.ConfigureOnce(_profile.BrowserDataDir);

        InitializeComponent();
        WinUiRuntimeTrace.Write("MainWindow after InitializeComponent");
        Title = $"Pulse Browser {Version}";

        // MinWidth dynamique selon l'état des onglets verticaux
        var hwnd = WindowNative.GetWindowHandle(this);
        var winId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(winId);
        _appWindow.Changed += AppWindow_Changed;
        ApplyAppIcon();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        ApplyWindowTitleBarColors();
        ApplyTitleBarSafeArea();

        VerticalTabsResizeThumb.PointerEntered += (_, _) => SetCursorSizeWestEast();
        VerticalTabsResizeThumb.PointerExited  += (_, _) => RestoreDefaultCursor();

        // Saisie clavier PIN + reset du timer de session
        Content.KeyDown      += RootKeyDown;
        Content.PointerMoved += (_, _) => ResetSessionTimer();
        var commandPaletteAccelerator = new KeyboardAccelerator
        {
            Key = VirtualKey.K,
            Modifiers = VirtualKeyModifiers.Control
        };
        commandPaletteAccelerator.Invoked += CommandPaletteAccelerator_Invoked;
        Content.KeyboardAccelerators.Add(commandPaletteAccelerator);
        // L'accélérateur est porté par la racine (toute la fenêtre). Son infobulle
        // automatique « Ctrl+K » resterait collée car le WebView2 avale l'événement de
        // sortie du pointeur → on la désactive.
        Content.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;

        _vault = new VaultStore(_profile.VaultFile);
        _passwordManager = new PasswordManagerService(_vault);
        _passwordManagerInteraction = new PasswordManagerInteractionService(_passwordManager);
        _credentialService.CredentialCaptured += CredentialService_CredentialCaptured;
        _credentialService.PageStateChanged += CredentialService_PageStateChanged;
        _bookmarks = new BookmarkStore(_profile.BookmarksFile, _profile.LegacyBookmarksFile, _profile.LegacyFavoritesFile);
        _webApps = new WebAppStore(_profile.WebAppsFile);
        WinUiRuntimeTrace.Write("BookmarkStore created");
        BookmarksList.ItemsSource = _bookmarkItems;
        BookmarkFoldersList.ItemsSource = _bookmarkFolderItems;
        AboutProfilePathText.Text = _profile.ProfileDir;
        AboutVersionText.Text = Version;
        _uiSettings = UiSettings.Load(_profile.UiSettingsFile, _profile.LegacyUiSettingsFile);
        ApplyUiSettings();
        InitPrivacyEngine();

        ReloadBookmarks();
        WinUiRuntimeTrace.Write("Bookmarks loaded");
        ReloadImportSources();
        WinUiRuntimeTrace.Write("Import sources loaded");
        _history = new HistoryStore(_profile.HistoryFile, _profile.LegacyHistoryFile);
        HistoryList.ItemsSource = _historyItems;
        CommandPaletteList.ItemsSource = _commandPaletteItems;
        WinUiRuntimeTrace.Write("History store loaded");
        LoadPasskeys();
        ApplyStartupPage();

        WinUiRuntimeTrace.Write("Initial tab ready");
        ShowPanel(BrowserPanel, "Accueil Pulse");
        _suppressTabSave = false;
        WinUiRuntimeTrace.Write("MainWindow constructor end");
        _ = InitializeLoginOverlayAsync();
    }

    public void InitializeBrowserSurface()
    {
        if (Environment.GetEnvironmentVariable("PULSE_BROWSER_DISABLE_WEBVIEW2") == "1")
        {
            _webViewDisabled = true;
            BrowserHost.Children.Clear();
            BrowserHost.Children.Add(new TextBlock
            {
                Text = "Moteur web desactive pour diagnostic.",
                Margin = new Thickness(24),
                TextWrapping = TextWrapping.Wrap
            });
            StatusText.Text = "Moteur web desactive pour diagnostic.";
            WinUiRuntimeTrace.Write("Browser surface skipped by environment");
            return;
        }

        WinUiRuntimeTrace.Write("InitializeBrowserSurface start");
        _browserSurfaceReady = true;

        var tab = CurrentTab();
        if (tab is null)
        {
            AddTab("Nouvel onglet", "pulse://accueil", select: true);
            return;
        }

        tab.PendingAddress ??= tab.Address;
        EnsureTabView(tab);
        WinUiRuntimeTrace.Write("Initial tab view requested");
    }

    private void NewTabMenu_Click(object sender, RoutedEventArgs e) =>
        AddTab("Nouvel onglet", "pulse://accueil", select: true);

    private void HomeMenu_Click(object sender, RoutedEventArgs e)
    {
        NavigateCurrentTab("pulse://accueil", "Accueil Pulse");
        ShowPanel(BrowserPanel, "Accueil Pulse");
    }

    private void AboutMenu_Click(object sender, RoutedEventArgs e) =>
        ShowPanel(AboutPanel, "A propos de Pulse Browser");

    private void SettingsMenu_Click(object sender, RoutedEventArgs e)
    {
        StorageCurrentFolderText.Text = _profile.ProfileDir;
        ShowPanel(SettingsPanel, "Parametres");
        SettingsNav_Click(SettingsNavNavigation, new RoutedEventArgs());
    }

    private void SettingsNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton btn || btn.Tag is not string section) return;
        SettingsSectionNavigation.Visibility = section == "navigation" ? Visibility.Visible : Visibility.Collapsed;
        SettingsSectionAppearance.Visibility = section == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        SettingsSectionAccessibility.Visibility = section == "accessibility" ? Visibility.Visible : Visibility.Collapsed;
        SettingsSectionStartup.Visibility    = section == "startup"    ? Visibility.Visible : Visibility.Collapsed;
        SettingsSectionVault.Visibility      = section == "vault"      ? Visibility.Visible : Visibility.Collapsed;
        SettingsSectionProfile.Visibility    = section == "profile"    ? Visibility.Visible : Visibility.Collapsed;
        SettingsSectionStorage.Visibility    = section == "storage"    ? Visibility.Visible : Visibility.Collapsed;
        SettingsSectionPrivacy.Visibility    = section == "privacy"    ? Visibility.Visible : Visibility.Collapsed;
        if (section == "privacy") UpdatePrivacyUi();
    }

    private void ShowPanel(FrameworkElement visiblePanel, string status)
    {
        BrowserPanel.Visibility = Visibility.Collapsed;
        BookmarksPanel.Visibility = Visibility.Collapsed;
        ImportPanel.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility = Visibility.Collapsed;
        AboutPanel.Visibility = Visibility.Collapsed;
        HistoryPanel.Visibility = Visibility.Collapsed;
        DownloadsPanel.Visibility = Visibility.Collapsed;
        VaultPanel.Visibility = Visibility.Collapsed;
        PasskeysPanel.Visibility = Visibility.Collapsed;
        SiteControlPanel.Visibility = Visibility.Collapsed;
        SessionsPanel.Visibility = Visibility.Collapsed;
        WalletPanel.Visibility = Visibility.Collapsed;
        WebAppsPanel.Visibility = Visibility.Collapsed;

        visiblePanel.Visibility = Visibility.Visible;
        StatusText.Text = status;

        // Depuis un panneau interne (coffre, historique, paramètres...), la barre
        // d'état propose de revenir à la page web en cours.
        BackToPageButton.Visibility = ReferenceEquals(visiblePanel, BrowserPanel)
            ? Visibility.Collapsed
            : Visibility.Visible;
        StatusBarRow.Visibility = ReferenceEquals(visiblePanel, BrowserPanel)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void BackToPageButton_Click(object sender, RoutedEventArgs e)
    {
        var tab = CurrentTab();
        ShowPanel(BrowserPanel, tab?.Title ?? "Accueil Pulse");
    }

    private void ApplyWindowTitleBarColors()
    {
        if (_appWindow is null) return;

        var translucent = IsTranslucentChromeEnabled();
        var titleBarBackgroundAlpha = translucent ? (byte)226 : (byte)255;
        var titleBar = _appWindow.TitleBar;
        titleBar.BackgroundColor = UiColor(38, 37, 34, titleBarBackgroundAlpha);
        titleBar.InactiveBackgroundColor = UiColor(38, 37, 34, titleBarBackgroundAlpha);
        titleBar.ForegroundColor = UiColor(242, 238, 231);
        titleBar.InactiveForegroundColor = UiColor(160, 154, 146);
        titleBar.ButtonBackgroundColor = UiColor(38, 37, 34, titleBarBackgroundAlpha);
        titleBar.ButtonInactiveBackgroundColor = UiColor(38, 37, 34, titleBarBackgroundAlpha);
        titleBar.ButtonForegroundColor = UiColor(242, 238, 231);
        titleBar.ButtonInactiveForegroundColor = UiColor(160, 154, 146);
        titleBar.ButtonHoverBackgroundColor = UiColor(50, 48, 44, translucent ? (byte)238 : (byte)255);
        titleBar.ButtonHoverForegroundColor = UiColor(255, 255, 255);
        titleBar.ButtonPressedBackgroundColor = UiColor(68, 63, 56, translucent ? (byte)244 : (byte)255);
        titleBar.ButtonPressedForegroundColor = UiColor(255, 255, 255);
    }

    private static Windows.UI.Color UiColor(byte r, byte g, byte b, byte a = 255) =>
        new() { A = a, R = r, G = g, B = b };

    private void ApplyAppIcon()
    {
        if (_appWindow is null) return;

        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "PulseBrowser.ico");
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
        TitleBarDragRegion.Margin = new Thickness(0, 0, safeRight, 0);
        TitleBarDragRegion.Width = Math.Max(140, Math.Min(320, safeRight + 36));
    }

    private string NormalizeAddress(string raw)
    {
        var value = raw.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return "pulse://accueil";

        // Protocoles connus → navigation directe
        if (value.StartsWith("pulse://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return value;

        // Contient des espaces → forcément une recherche
        if (value.Contains(' '))
            return SearchUrl(value);

        // localhost / localhost:port → navigation locale sans TLS
        if (value.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("localhost:", StringComparison.OrdinalIgnoreCase))
            return $"http://{value}";

        // Ressemble à un domaine (contient un point, pas d'espace) → https://
        if (value.Contains('.'))
            return $"https://{value}";

        // Mot seul sans point → recherche
        return SearchUrl(value);
    }

    private string SearchUrl(string query)
    {
        var q = Uri.EscapeDataString(query);
        // Langue de l'interface système (ex. "fr") pour éviter les résultats en anglais.
        var lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return _uiSettings.SearchEngine switch
        {
            "duckduckgo" => $"https://duckduckgo.com/?q={q}&kl={lang}-{lang}",
            "brave"      => $"https://search.brave.com/search?q={q}",
            "bing"       => $"https://www.bing.com/search?q={q}&setlang={lang}",
            _            => $"https://www.google.com/search?q={q}&hl={lang}",
        };
    }

    private static string HashUrl(string url) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant()[..32];

    // Normalisation d'origine centralisée sur PublicSuffixService (source unique).
    private static string OriginOf(string url) => PublicSuffixService.OriginOf(url);

    private static string HashOrigin(string url) => HashUrl(OriginOf(url));

    private static string DisplayTitle(string address)
    {
        if (Uri.TryCreate(address, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        return address;
    }
}
