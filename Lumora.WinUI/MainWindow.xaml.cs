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

public sealed partial class MainWindow : Window
{
    private const string Version = "0.79.0-dev";
    private const double VerticalTabsCompactWidth = 64;
    private const double VerticalTabsMinExpandedWidth = 120;
    private const double VerticalTabsDefaultWidth = 210;
    private const double VerticalTabsMaxWidth = 320;

    private readonly LumoraProfilePaths _profile = LumoraProfilePaths.Default();
    private readonly BookmarkStore _bookmarks;
    private readonly WebAppStore _webApps;
    private readonly ObservableCollection<BookmarkListItem> _bookmarkItems = new();
    private readonly ObservableCollection<BookmarkListItem> _bookmarkFolderItems = new();
    private readonly List<BrowserTabState> _tabs = new();
    private readonly List<TabGroup> _tabGroups = new();
    private int _nextGroupId = 1;
    private readonly HashSet<int> _collapsedGroupIds = new();
    // Identifiants des groupes vivants déjà rangés dans la bibliothèque : sert au
    // garde-fou (ne pas reproposer de garder un groupe déjà enregistré).
    private readonly HashSet<int> _savedGroupIds = new();
    private SavedTabGroupStore _savedTabGroups = null!;
    private NoteStore _notes = null!;
    private AnnotationStore _annotations = null!;
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
    private bool _suppressTabOrderSync;
    private bool _isGuestMode;
    private bool _verticalTabsEnabled;
    private bool _verticalTabsCompact;
    private bool _compactModeEnabled;
    private bool _isFullScreenMode;
    private CoreWebView2? _contentFullScreenCore;
    private bool _wasLumoraFullScreenBeforeContentFullScreen;
    private AppWindowPresenterKind? _presenterKindBeforeContentFullScreen;
    private OverlappedPresenterState? _overlappedStateBeforeContentFullScreen;
    // Masquage differe des barres immersives en plein ecran : un Collapsed
    // instantane au PointerExited fait clignoter la barre, car la zone de
    // survol (10px) est entierement recouverte par la barre elle-meme (44px)
    // une fois visible, et le pointeur n'a pas toujours bouge pour que le
    // hit-test WinUI reassigne l'evenement Entered a la barre avant l'Exited.
    private DispatcherTimer? _fullScreenTopChromeHideTimer;
    private DispatcherTimer? _verticalTabsRailAutoHideTimer;
    private Microsoft.UI.Windowing.AppWindow? _appWindow;
    private double _verticalTabsExpandedWidth = VerticalTabsDefaultWidth;
    private int _nextTabId = 1;
    private readonly HistoryPanelController _historyPanel;
    private readonly ObservableCollection<CommandPaletteItem> _commandPaletteItems = new();
    private bool _suppressTabSave = true;
    private readonly VaultStore _vault;
    private readonly PasswordManagerService _passwordManager;
    private readonly PasswordManagerInteractionService _passwordManagerInteraction;
    private readonly CredentialService _credentialService = new();
    private (string Origin, string Username, string Password, string LoginUrl, string Label)? _pendingCredential;
    private IReadOnlyList<VaultCredential> _pendingAutoFillCandidates = Array.Empty<VaultCredential>();
    private string? _pendingGeneratedPassword;
    private readonly List<PasskeyEntry> _passkeys = new();
    private UserProfile? _userProfile;
    private UserProfile? _pendingUserProfile;
    private List<LumoraProfileEntry> _profileEntries = new();
    private string _pinBuffer = string.Empty;
    private int _pinFailCount;
    private string? _pendingProfileDir;
    private string? _pendingProfileId;
    private LumoraProfilePaths? _profileCreationTarget;
    // Mot de passe de création retenu le temps de finaliser le profil, pour clé
    // le coffre au même mot de passe (couplage session ↔ coffre). Effacé aussitôt.
    private string? _pendingProfilePassword;
    // PIN de création retenu de même, pour activer le déverrouillage du coffre par PIN.
    private string? _pendingProfilePin;
    private string? _pendingRecoveryKey;
    private List<MigrationBrowserEntry> _migrationEntries = new();
    private bool _restartRequired;
    private DispatcherTimer? _sessionTimer;
    private bool _systemLockHooked;
    private readonly PrivacyEngine _privacy = new();
    private NetworkBlockerModule? _networkBlocker;
    private TelemetryBlockerModule? _telemetryBlocker;
    private CosmeticFilterModule? _cosmeticFilter;
    private ConsentManagerModule? _consentModule;
    // Identifiants des scripts privacy enregistrés, PAR moteur (un WebView2 par onglet).
    private readonly Dictionary<CoreWebView2, string> _cosmeticScriptIds = new();
    private readonly Dictionary<CoreWebView2, string> _consentScriptIds = new();
    private readonly Dictionary<CoreWebView2, string> _loginCompatibilityScriptIds = new();
    private readonly Dictionary<CoreWebView2, string> _loginDiagnosticScriptIds = new();
    private readonly SiteLoginDiagnosticRecorder _loginDiagnostics = new();
    private readonly Dictionary<int, int> _popupParentTabIds = new();
    private readonly HashSet<int> _federatedIdentityPopupTabIds = new();
    // Migration Chromium → vault.lumora déjà effectuée pour ce lancement.
    private bool _browserPasswordsMigrated;
    private string? _currentPageDomain;
    private bool _suppressShieldToggle;
    private bool _suppressSiteControlTrustToggle;
    private bool _suppressSiteControlCompatibilityToggle;
    private bool _suppressSiteControlDiagnosticToggle;
    private bool _suppressSitePermissionUi;
    private static readonly System.Net.Http.HttpClient FaviconHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public MainWindow()
    {
        WinUiRuntimeTrace.Write("MainWindow constructor start");

        // WebView2 stocke ses cookies/sessions dans le profil Lumora plutôt que dans un
        // dossier collé à l'exe (Lumora.WinUI.exe.WebView2). Conséquence : changer
        // ou supprimer un profil déconnecte réellement des sites, et la purge est complète.
        // Doit être fait AVANT toute création de moteur WebView2 dans le process. Partagé
        // avec les fenêtres d'application web (WebView2Bootstrap), qui doivent pointer
        // vers le même profil pour partager cookies et sessions.
        WebView2Bootstrap.ConfigureOnce(_profile.BrowserDataDir);

        InitializeComponent();
        WinUiRuntimeTrace.Write("MainWindow after InitializeComponent");
        Title = $"Lumora {Version}";

        // Glisser-deposer natif de la barre d'onglets horizontale (CanReorderTabs) :
        // WinUI reordonne directement TabItems (ObservableCollection), donc on
        // resynchronise _tabs (source de verite pour l'ordre/la persistance) via
        // ce changement plutot que de dupliquer une logique de suivi de position.
        if (BrowserTabs.TabItems is System.Collections.Specialized.INotifyCollectionChanged notifyingTabItems)
        {
            notifyingTabItems.CollectionChanged += BrowserTabs_TabItems_CollectionChanged;
        }

        // Source de vérité unique pour "quel moteur est actif" : interrogée en direct
        // à chaque besoin plutôt que poussée via une copie qui pouvait se désynchroniser.
        // ActiveTabIdProvider pilote le routage des messages (comparaison d'ID, jamais
        // d'objet) ; ActiveCoreProvider ne sert qu'à exécuter le remplissage lui-même.
        _credentialService.ActiveTabIdProvider = () => CurrentTab()?.Id;
        _credentialService.ActiveCoreProvider = () => _browserView?.CoreWebView2;

        // MinWidth dynamique selon l'état des onglets verticaux
        var hwnd = WindowNative.GetWindowHandle(this);
        var winId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(winId);
        _appWindow.Changed += AppWindow_Changed;
        ApplyAppIcon();
        ExtendsContentIntoTitleBar = true;
        ApplyWindowTitleBarColors();
        ApplyTitleBarSafeArea();
        RootShell.SizeChanged += (_, _) => UpdateTitleBarDragRegion();

        VerticalTabsResizeThumb.PointerEntered += (_, _) => SetCursorSizeWestEast();
        VerticalTabsResizeThumb.PointerExited  += (_, _) => RestoreDefaultCursor();

        // Saisie clavier PIN + reset du timer de session
        Content.KeyDown      += RootKeyDown;
        Content.PointerMoved += (_, _) => ResetSessionTimer();
        // SystemEvents garde une reference statique forte vers ses abonnes : se
        // desabonner a la fermeture, sinon fuite de la fenetre + crash au prochain
        // verrouillage/veille Windows.
        Closed += (_, _) => UnhookSystemLockEvents();
        var commandPaletteAccelerator = new KeyboardAccelerator
        {
            Key = VirtualKey.K,
            Modifiers = VirtualKeyModifiers.Control
        };
        commandPaletteAccelerator.Invoked += CommandPaletteAccelerator_Invoked;
        Content.KeyboardAccelerators.Add(commandPaletteAccelerator);
        var privateWindowAccelerator = new KeyboardAccelerator
        {
            Key = VirtualKey.N,
            Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift
        };
        privateWindowAccelerator.Invoked += PrivateWindowAccelerator_Invoked;
        Content.KeyboardAccelerators.Add(privateWindowAccelerator);
        var reopenTabAccelerator = new KeyboardAccelerator
        {
            Key = VirtualKey.T,
            Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift
        };
        reopenTabAccelerator.Invoked += ReopenClosedTabAccelerator_Invoked;
        Content.KeyboardAccelerators.Add(reopenTabAccelerator);
        // L'accélérateur est porté par la racine (toute la fenêtre). Son infobulle
        // automatique « Ctrl+K » resterait collée car le WebView2 avale l'événement de
        // sortie du pointeur → on la désactive.
        Content.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;

        _vault = new VaultStore(_profile.VaultFile);
        _passwordManager = new PasswordManagerService(_vault);
        _passwordManagerInteraction = new PasswordManagerInteractionService(_passwordManager);
        _credentialService.CredentialCaptured += CredentialService_CredentialCaptured;
        _credentialService.PageStateChanged += CredentialService_PageStateChanged;
        _credentialService.FillReported += CredentialService_FillReported;
        _bookmarks = new BookmarkStore(_profile.BookmarksFile, _profile.LegacyBookmarksFile, _profile.LegacyFavoritesFile);
        var repairedBookmarkDuplicates = _bookmarks.RepairImportedFolderDuplicates();
        if (repairedBookmarkDuplicates > 0)
        {
            WinUiRuntimeTrace.Write($"Bookmark duplicate repair changed {repairedBookmarkDuplicates} item(s)");
        }
        _webApps = new WebAppStore(_profile.WebAppsFile);
        RepairInvalidWebAppIconsAndShortcuts();
        WinUiRuntimeTrace.Write("BookmarkStore created");
        BookmarksList.ItemsSource = _bookmarkItems;
        BookmarkFoldersList.ItemsSource = _bookmarkFolderItems;
        AboutProfilePathText.Text = _profile.ProfileDir;
        AboutVersionText.Text = Version;
        LoadAboutAuthenticity();
        _uiSettings = UiSettings.Load(_profile.UiSettingsFile, _profile.LegacyUiSettingsFile);
        ApplyUiSettings();
        InitPrivacyEngine();

        ReloadBookmarks();
        WinUiRuntimeTrace.Write("Bookmarks loaded");
        ReloadImportSources();
        WinUiRuntimeTrace.Write("Import sources loaded");
        _historyPanel = new HistoryPanelController(
            new HistoryStore(_profile.HistoryFile, _profile.LegacyHistoryFile),
            new DownloadHistoryStore(_profile.DownloadsFile));
        _savedTabGroups = new SavedTabGroupStore(
            _profile.SavedTabGroupsFile, LumoraFile.TryReadAllText, LumoraFile.WriteAllText);
        _notes = new NoteStore(_profile.NotesFile);
        _annotations = new AnnotationStore(_profile.AnnotationsFile);
        _siteRelocations = new SiteRelocationStore(
            _profile.SiteRelocationsFile, LumoraFile.TryReadAllText, LumoraFile.WriteAllText);
        HistoryList.ItemsSource = _historyPanel.Items;
        CommandPaletteList.ItemsSource = _commandPaletteItems;
        AddressSuggestionsList.ItemsSource = _addressSuggestionItems;
        WinUiRuntimeTrace.Write("History store loaded");
        LoadPasskeys();
        ApplyStartupPage();

        WinUiRuntimeTrace.Write("Initial tab ready");
        ShowPanel(BrowserPanel, "Accueil Lumora");
        _suppressTabSave = false;
        WinUiRuntimeTrace.Write("MainWindow constructor end");
        _ = InitializeLoginOverlayAsync();
    }

    public void InitializeBrowserSurface()
    {
        if (Environment.GetEnvironmentVariable("LUMORA_DISABLE_WEBVIEW2") == "1")
        {
            _webViewDisabled = true;
            BrowserHost.Children.Clear();
            BrowserHost.Children.Add(new TextBlock
            {
                Text = "Moteur web désactivé pour diagnostic.",
                Margin = new Thickness(24),
                TextWrapping = TextWrapping.Wrap
            });
            StatusText.Text = "Moteur web désactivé pour diagnostic.";
            WinUiRuntimeTrace.Write("Browser surface skipped by environment");
            return;
        }

        WinUiRuntimeTrace.Write("InitializeBrowserSurface start");
        _browserSurfaceReady = true;

        var tab = CurrentTab();
        if (tab is null)
        {
            AddTab("Nouvel onglet", "lumora://accueil", select: true);
            return;
        }

        tab.PendingAddress ??= tab.Address;
        EnsureTabView(tab);
        WinUiRuntimeTrace.Write("Initial tab view requested");
    }

    private void NewTabMenu_Click(object sender, RoutedEventArgs e) =>
        AddTab("Nouvel onglet", "lumora://accueil", select: true);

    private void HomeMenu_Click(object sender, RoutedEventArgs e)
    {
        NavigateCurrentTab("lumora://accueil", "Accueil Lumora");
        ShowPanel(BrowserPanel, "Accueil Lumora");
    }

    private void AboutMenu_Click(object sender, RoutedEventArgs e)
    {
        AboutNavSummary.IsChecked = true;
        ShowAboutSection("summary");
        ShowPanel(AboutPanel, "À propos de Lumora");
    }

    private void AboutNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string section })
            ShowAboutSection(section);
    }

    private void ShowAboutSection(string section)
    {
        AboutSectionSummary.Visibility = section == "summary" ? Visibility.Visible : Visibility.Collapsed;
        AboutSectionModules.Visibility = section == "modules" ? Visibility.Visible : Visibility.Collapsed;
        AboutSectionPrivacy.Visibility = section == "privacy" ? Visibility.Visible : Visibility.Collapsed;
        AboutSectionAuthenticity.Visibility = section == "authenticity" ? Visibility.Visible : Visibility.Collapsed;
        AboutSectionCredits.Visibility = section == "credits" ? Visibility.Visible : Visibility.Collapsed;
        AboutSectionTechnical.Visibility = section == "technical" ? Visibility.Visible : Visibility.Collapsed;
        AboutSectionProfile.Visibility = section == "profile" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LoadAboutAuthenticity()
    {
        var info = BuildAuthenticity.Load(AppContext.BaseDirectory, Version);
        AboutAuthenticityChannelText.Text = info.Channel;
        AboutAuthenticityShaText.Text = info.Sha256;
        AboutAuthenticitySigstoreText.Text = info.Sigstore;
        AboutAuthenticityWindowsText.Text = info.WindowsSignature;
        AboutAuthenticityProfileText.Text = info.ProfileMode;
    }

    private void SettingsMenu_Click(object sender, RoutedEventArgs e)
    {
        StorageCurrentFolderText.Text = _profile.ProfileDir;
        ShowPanel(SettingsPanel, "Paramètres");
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

    private void OpenPersonalizationSettings()
    {
        StorageCurrentFolderText.Text = _profile.ProfileDir;
        ShowPanel(SettingsPanel, "Personnalisation");
        SettingsNavAppearance.IsChecked = true;
        SettingsNav_Click(SettingsNavAppearance, new RoutedEventArgs());
    }

    private void ProfileStatusButton_Click(object sender, RoutedEventArgs e) =>
        OpenPersonalizationSettings();

    private void OpenPersonalizationFromProfileButton_Click(object sender, RoutedEventArgs e) =>
        OpenPersonalizationSettings();

    private void SettingsOpenCommandPaletteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiSettings.CommandPaletteEnabled)
        {
            StatusText.Text = "Activez d'abord la palette de commande Ctrl+K.";
            return;
        }

        ShowPanel(BrowserPanel, CurrentTab()?.Title ?? "Accueil Lumora");
        ToggleCommandPalette();
    }

    private void ModulesMenu_Click(object sender, RoutedEventArgs e)
    {
        ModulesFlyout.Hide();
        ShowPanel(ModulesPanel, "Modules Lumora");
    }

    private void ModulesFlyoutCloseButton_Click(object sender, RoutedEventArgs e) =>
        ModulesFlyout.Hide();

    private void ModulesOpenCommandPalette_Click(object sender, RoutedEventArgs e)
    {
        ModulesFlyout.Hide();
        if (!_uiSettings.CommandPaletteEnabled)
        {
            StatusText.Text = "Activez d'abord la palette de commande Ctrl+K.";
            return;
        }

        ShowPanel(BrowserPanel, CurrentTab()?.Title ?? "Accueil Lumora");
        ShowCommandPalette();
    }

    private void ModulesReadAloud_Click(object sender, RoutedEventArgs e)
    {
        ModulesFlyout.Hide();
        if (!_uiSettings.ReadAloudEnabled)
        {
            StatusText.Text = "Activez la lecture à voix haute dans Paramètres > Accessibilité.";
            return;
        }

        ReadAloudFlyout.ShowAt(sender as FrameworkElement ?? ModulesButton);
    }

    private void ModulesMedia_Click(object sender, RoutedEventArgs e)
    {
        ModulesFlyout.Hide();
        ShowPanel(ModulesPanel, "Modules média Lumora");
        StatusText.Text = "Modules média : détacher la vidéo ou télécharger depuis les tuiles vidéo.";
    }

    private void ModulesVideoDownload_Click(object sender, RoutedEventArgs e)
    {
        ModulesFlyout.Hide();
        ShowVideoDownloadFlyout(sender as FrameworkElement ?? ModulesButton);
    }

    private void ModulesSearchAssist_Click(object sender, RoutedEventArgs e)
    {
        ModulesFlyout.Hide();
        _ = RunSearchAssistAsync(sender as FrameworkElement ?? ModulesButton);
    }

    private async void ModulesTranslate_Click(object sender, RoutedEventArgs e)
    {
        ModulesFlyout.Hide();
        var tab = CurrentTab();
        var view = tab?.View;
        var address = view?.Source?.ToString() ?? tab?.Address ?? string.Empty;
        if (view is null || !BookmarkStore.IsWebUrl(address))
        {
            StatusText.Text = "Ouvrez une page web pour utiliser la traduction.";
            return;
        }

        await OfferTranslationIfNeededAsync(view, address);
        if (TranslateBar.Visibility != Visibility.Visible)
        {
            StatusText.Text = "Aucune traduction proposee pour cette page.";
        }
    }

    private void ModulePinToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string moduleId } toggle)
        {
            return;
        }

        var pinned = _uiSettings.PinnedModuleIds;
        if (toggle.IsChecked == true)
        {
            if (!pinned.Contains(moduleId, StringComparer.OrdinalIgnoreCase))
            {
                pinned.Add(moduleId);
            }
        }
        else
        {
            pinned.RemoveAll(id => string.Equals(id, moduleId, StringComparison.OrdinalIgnoreCase));
        }

        _uiSettings.Save(_profile.UiSettingsFile);
        UpdateModulesPinUi();
    }

    private void UpdateModulesPinUi()
    {
        var pinned = _uiSettings.PinnedModuleIds;
        bool IsPinned(string id) => pinned.Contains(id, StringComparer.OrdinalIgnoreCase);

        ReaderModeButton.Visibility = IsPinned("reader") ? Visibility.Visible : Visibility.Collapsed;
        NotesModuleButton.Visibility = IsPinned("notes") ? Visibility.Visible : Visibility.Collapsed;
        ReadAloudButton.Visibility = IsPinned("readAloud") ? Visibility.Visible : Visibility.Collapsed;
        VideoDownloadButton.Visibility = IsPinned("videoDownload") ? Visibility.Visible : Visibility.Collapsed;
        SearchAssistButton.Visibility = IsPinned("searchAssist") ? Visibility.Visible : Visibility.Collapsed;
        DetachVideoPinnedButton.Visibility = IsPinned("detachVideo") ? Visibility.Visible : Visibility.Collapsed;
        TranslatePinnedButton.Visibility = IsPinned("translate") ? Visibility.Visible : Visibility.Collapsed;
        WebAppsPinnedButton.Visibility = IsPinned("webApps") ? Visibility.Visible : Visibility.Collapsed;
        DictationPinnedButton.Visibility = IsPinned("dictation") ? Visibility.Visible : Visibility.Collapsed;

        ReaderPinToggleButton.IsChecked = IsPinned("reader");
        NotesPinToggleButton.IsChecked = IsPinned("notes");
        ReadAloudPinToggleButton.IsChecked = IsPinned("readAloud");
        VideoDownloadPinToggleButton.IsChecked = IsPinned("videoDownload");
        SearchAssistPinToggleButton.IsChecked = IsPinned("searchAssist");
        DetachVideoPinToggleButton.IsChecked = IsPinned("detachVideo");
        TranslatePinToggleButton.IsChecked = IsPinned("translate");
        WebAppsPinToggleButton.IsChecked = IsPinned("webApps");
        DictationPinToggleButton.IsChecked = IsPinned("dictation");

        PanelReaderPinToggleButton.IsChecked = IsPinned("reader");
        PanelNotesPinToggleButton.IsChecked = IsPinned("notes");
        PanelReadAloudPinToggleButton.IsChecked = IsPinned("readAloud");
        PanelVideoDownloadPinToggleButton.IsChecked = IsPinned("videoDownload");
        PanelSearchAssistPinToggleButton.IsChecked = IsPinned("searchAssist");
        PanelDetachVideoPinToggleButton.IsChecked = IsPinned("detachVideo");
        PanelTranslatePinToggleButton.IsChecked = IsPinned("translate");
        PanelWebAppsPinToggleButton.IsChecked = IsPinned("webApps");
        PanelDictationPinToggleButton.IsChecked = IsPinned("dictation");
    }

    private void SettingsStartupShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsNavStartup.IsChecked = true;
        SettingsNav_Click(SettingsNavStartup, new RoutedEventArgs());
    }

    private void SettingsAccessibilityShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsNavAccessibility.IsChecked = true;
        SettingsNav_Click(SettingsNavAccessibility, new RoutedEventArgs());
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
        SavedTabGroupsPanel.Visibility = Visibility.Collapsed;
        NotesPanel.Visibility = Visibility.Collapsed;
        VaultPanel.Visibility = Visibility.Collapsed;
        PasskeysPanel.Visibility = Visibility.Collapsed;
        ModulesPanel.Visibility = Visibility.Collapsed;
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
        StatusBarRow.Visibility = Visibility.Visible;
    }

    private void BackToPageButton_Click(object sender, RoutedEventArgs e)
    {
        var tab = CurrentTab();
        ShowPanel(BrowserPanel, tab?.Title ?? "Accueil Lumora");

        if (tab?.View is { } view && BookmarkStore.IsWebUrl(tab.Address))
        {
            // Rendre le focus clavier au WebView2. Sans ca, apres un passage par un
            // panneau interne (coffre, historique, parametres...), les raccourcis de
            // la page (Ctrl+V, Ctrl+F...) et le menu contextuel ne repondent plus
            // tant que l'utilisateur n'a pas reclique dans la page : le focus etait
            // reste dans le XAML du panneau. Meme correctif qu'a l'activation d'onglet.
            view.Focus(FocusState.Programmatic);

            // L'utilisateur revient peut-etre du coffre ou il a consulte un identifiant :
            // re-proposer le remplissage sur la page en cours si un compte correspond
            // (l'offre initiale ne se declenche qu'au chargement de la page).
            OfferAutoFill(tab.Address);
        }
    }

    private void ApplyWindowTitleBarColors()
    {
        if (_appWindow is null) return;

        var translucent = IsTranslucentChromeEnabled();
        var titleBarBackgroundAlpha = translucent ? (byte)226 : (byte)255;
        var titleBar = _appWindow.TitleBar;
        titleBar.BackgroundColor = UiColor(20, 32, 42, titleBarBackgroundAlpha);
        titleBar.InactiveBackgroundColor = UiColor(20, 32, 42, titleBarBackgroundAlpha);
        titleBar.ForegroundColor = UiColor(255, 248, 234);
        titleBar.InactiveForegroundColor = UiColor(195, 185, 165);
        titleBar.ButtonBackgroundColor = UiColor(20, 32, 42, titleBarBackgroundAlpha);
        titleBar.ButtonInactiveBackgroundColor = UiColor(20, 32, 42, titleBarBackgroundAlpha);
        titleBar.ButtonForegroundColor = UiColor(255, 248, 234);
        titleBar.ButtonInactiveForegroundColor = UiColor(195, 185, 165);
        titleBar.ButtonHoverBackgroundColor = UiColor(34, 49, 58, translucent ? (byte)238 : (byte)255);
        titleBar.ButtonHoverForegroundColor = UiColor(255, 255, 255);
        titleBar.ButtonPressedBackgroundColor = UiColor(50, 69, 76, translucent ? (byte)244 : (byte)255);
        titleBar.ButtonPressedForegroundColor = UiColor(255, 255, 255);
    }

    private static Windows.UI.Color UiColor(byte r, byte g, byte b, byte a = 255) =>
        new() { A = a, R = r, G = g, B = b };

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

    // Logique pure extraite dans AddressNormalizer (partagée avec la fenêtre privée).
    private string NormalizeAddress(string raw) =>
        AddressNormalizer.Normalize(raw, _uiSettings.SearchEngine);

    private string SearchUrl(string query) =>
        AddressNormalizer.SearchUrl(query, _uiSettings.SearchEngine);

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

    private static string DisplayAddressForBar(string address) =>
        address.Equals("lumora://accueil", StringComparison.OrdinalIgnoreCase) ? string.Empty : address;
}
