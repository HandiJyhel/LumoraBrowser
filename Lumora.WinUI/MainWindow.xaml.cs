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
    private const string Version = "0.83.17-dev";
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
        ShowPanel(SettingsPanel, "Centre Lumora");
        SettingsNavOverview.IsChecked = true;
        SettingsNav_Click(SettingsNavOverview, new RoutedEventArgs());
    }

    private void SettingsNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton btn || btn.Tag is not string section) return;
        SettingsSectionOverview.Visibility = section == "overview" ? Visibility.Visible : Visibility.Collapsed;
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

    private void ModulesUsageModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        if (ModulesUsageModeCombo.SelectedItem is not ComboBoxItem usageModeItem)
        {
            return;
        }

        ApplyUsageModeFromUi(usageModeItem.Tag?.ToString() ?? "neutral");
    }

    private void UsageModeQuickButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string usageMode })
        {
            return;
        }

        ApplyUsageModeFromUi(usageMode);
    }

    private void ApplyUsageModeFromUi(string usageMode)
    {
        var previousUsageMode = _uiSettings.UsageMode;
        _uiSettings.UsageMode = usageMode;
        if (!string.Equals(previousUsageMode, usageMode, StringComparison.OrdinalIgnoreCase))
        {
            _uiSettings.LastIntroducedUsageMode = string.Empty;
        }

        ApplyUsageModePreset(usageMode);
        _uiSettings.Save(_profile.UiSettingsFile);

        ApplyUiSettings();
        RefreshNovaHomePages();
        StatusText.Text = string.Equals(usageMode, "neutral", StringComparison.OrdinalIgnoreCase)
            ? "Mode Lumora applique : Neutre. Accueil simplifie et modules essentiels conserves."
            : $"Mode Lumora applique : {UsageModeLabel(usageMode)}. Une presentation du mode est disponible sur l'accueil.";
    }

    private void UpdateUsageModeButtonUi()
    {
        var mode = (_uiSettings.UsageMode ?? "neutral").ToLowerInvariant();
        var label = UsageModeLabel(mode);
        UsageModeIcon.Glyph = mode switch
        {
            "neutral" => "\uE121",
            "focus" => "\uE8A7",
            "reading" => "\uE736",
            "creative" => "\uE70B",
            "research" => "\uE721",
            "night" => "\uE708",
            _ => "\uE9D2"
        };

        UsageModeLabelText.Text = "Mode";
        UsageModeCurrentText.Text = label;
        ToolTipService.SetToolTip(UsageModeButton, $"Mode d'usage : {label}");
        AutomationProperties.SetName(UsageModeButton, $"Mode d'usage : {label}");

        void Mark(Button button, string tag)
        {
            var active = string.Equals(mode, tag, StringComparison.OrdinalIgnoreCase);
            button.Opacity = active ? 1 : 0.78;
            button.FontWeight = active ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }

        Mark(UsageModeNeutralButton, "neutral");
        Mark(UsageModeBalancedButton, "balanced");
        Mark(UsageModeFocusButton, "focus");
        Mark(UsageModeReadingButton, "reading");
        Mark(UsageModeCreativeButton, "creative");
        Mark(UsageModeResearchButton, "research");
        Mark(UsageModeNightButton, "night");
    }

    private void UpdateModeCompanionUi()
    {
        var mode = (_uiSettings.UsageMode ?? "neutral").ToLowerInvariant();
        var label = UsageModeLabel(mode);
        var companion = ModeCompanion(mode);
        var isNeutral = string.Equals(mode, "neutral", StringComparison.OrdinalIgnoreCase);

        ModeCompanionButton.Visibility = Visibility.Visible;

        ModeCompanionIcon.Glyph = companion.Icon;
        ModeCompanionMascotIcon.Glyph = companion.Icon;
        ModeCompanionModeText.Text = isNeutral ? "Rapide" : label;
        ModeCompanionTitleText.Text = $"Compagnon {label}";
        ModeCompanionBodyText.Text = companion.Body;
        ModeCompanionMemoryTitleText.Text = companion.MemoryTitle;
        ModeCompanionMemoryBox.PlaceholderText = companion.MemoryPlaceholder;
        ModeCompanionMemoryBox.Text = CompanionMemory(mode);
        ModeCompanionPrimaryIcon.Glyph = companion.PrimaryIcon;
        ModeCompanionPrimaryTitleText.Text = companion.PrimaryTitle;
        ModeCompanionPrimaryHintText.Text = companion.PrimaryHint;
        ModeCompanionSecondaryIcon.Glyph = companion.SecondaryIcon;
        ModeCompanionSecondaryTitleText.Text = companion.SecondaryTitle;
        ModeCompanionSecondaryHintText.Text = companion.SecondaryHint;

        ToolTipService.SetToolTip(ModeCompanionButton, $"Compagnon {label}");
        AutomationProperties.SetName(ModeCompanionButton, $"Compagnon du mode {label}");
    }

    private void ModeCompanionButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateModeCompanionUi();
        StatusText.Text = $"Compagnon {UsageModeLabel(_uiSettings.UsageMode)} disponible.";
    }

    private async void ModeCompanionPrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        ModeCompanionFlyout.Hide();
        await RunModeCompanionActionAsync(ModeCompanion((_uiSettings.UsageMode ?? "neutral").ToLowerInvariant()).PrimaryAction);
    }

    private async void ModeCompanionSecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        ModeCompanionFlyout.Hide();
        await RunModeCompanionActionAsync(ModeCompanion((_uiSettings.UsageMode ?? "neutral").ToLowerInvariant()).SecondaryAction);
    }

    private void ModeCompanionSaveButton_Click(object sender, RoutedEventArgs e)
    {
        var mode = (_uiSettings.UsageMode ?? "neutral").ToLowerInvariant();
        SetCompanionMemory(mode, ModeCompanionMemoryBox.Text);
        _uiSettings.Save(_profile.UiSettingsFile);
        RefreshNovaHomePages();
        StatusText.Text = $"Lumie garde votre {ModeCompanion(mode).MemoryName}.";
    }

    private string CompanionMemory(string mode) =>
        mode switch
        {
            "focus" => _uiSettings.CompanionFocusObjective,
            "reading" => _uiSettings.CompanionReadingNote,
            "creative" => _uiSettings.CompanionCreativePostIt,
            "research" => _uiSettings.CompanionResearchTrail,
            "night" => _uiSettings.CompanionNightReminder,
            _ => _uiSettings.CompanionBalancedMemo
        };

    private void SetCompanionMemory(string mode, string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length > 4000)
        {
            trimmed = trimmed[..4000];
        }

        switch (mode)
        {
            case "focus":
                _uiSettings.CompanionFocusObjective = trimmed;
                break;
            case "reading":
                _uiSettings.CompanionReadingNote = trimmed;
                break;
            case "creative":
                _uiSettings.CompanionCreativePostIt = trimmed;
                break;
            case "research":
                _uiSettings.CompanionResearchTrail = trimmed;
                break;
            case "night":
                _uiSettings.CompanionNightReminder = trimmed;
                break;
            default:
                _uiSettings.CompanionBalancedMemo = trimmed;
                break;
        }
    }

    private async Task RunModeCompanionActionAsync(string action)
    {
        switch (action)
        {
            case "notes":
                NotesMenu_Click(this, new RoutedEventArgs());
                StatusText.Text = "Compagnon Notes ouvert.";
                break;

            case "reader":
                ShowPanel(BrowserPanel, CurrentTab()?.Title ?? "Accueil Lumora");
                await ToggleReaderModeAsync(ensureOpen: true);
                StatusText.Text = "Compagnon Lecture ouvert.";
                break;

            case "read_aloud":
                ReadAloudFlyout.ShowAt(ReadAloudButton.Visibility == Visibility.Visible ? ReadAloudButton : ModeCompanionButton);
                StatusText.Text = "Compagnon voix locale ouvert.";
                break;

            case "search_assist":
                await RunSearchAssistAsync(SearchAssistButton.Visibility == Visibility.Visible ? SearchAssistButton : ModeCompanionButton);
                break;

            case "history":
                HistoryMenu_Click(this, new RoutedEventArgs());
                StatusText.Text = "Compagnon Recherche : historique local ouvert.";
                break;

            case "bookmarks":
                BookmarksMenu_Click(this, new RoutedEventArgs());
                StatusText.Text = "Compagnon Recherche : sources gardees ouvertes.";
                break;

            case "command_palette":
                if (!_uiSettings.CommandPaletteEnabled)
                {
                    StatusText.Text = "Palette Ctrl+K desactivee.";
                    return;
                }

                ShowCommandPalette();
                break;

            case "fullscreen":
                ToggleFullScreenMode();
                break;

            case "modules":
                ShowPanel(ModulesPanel, "Modules Lumora");
                StatusText.Text = "Compagnon Modules ouvert.";
                break;

            case "add_shortcut":
                await HandleNewTabShortcutMessageAsync("newtab_add_shortcut", new JsonObject());
                StatusText.Text = "Ajout d'un raccourci Lumora.";
                break;
        }
    }

    private static ModeCompanionDefinition ModeCompanion(string usageMode) =>
        usageMode switch
        {
            "neutral" => new ModeCompanionDefinition(
                "\uE121",
                "Le mode Neutre garde Lumora discret, mais laisse un accès rapide aux raccourcis et aux actions utiles.",
                "Mémo neutre",
                "votre mémo neutre",
                "Ex. rappel minimal de navigation...",
                "add_shortcut",
                "\uE710",
                "Ajouter un raccourci",
                "Garder un repère rapide même en mode neutre.",
                "command_palette",
                "\uE721",
                "Actions rapides",
                "Ouvrir Ctrl+K sans quitter la page."),
            "focus" => new ModeCompanionDefinition(
                "\uE8A7",
                "Gardez votre objectif et les commandes rapides sous la main sans revenir a l'accueil.",
                "Objectif courant",
                "votre objectif",
                "Ex. terminer cette tâche, vérifier un bug, rester sur une seule priorité...",
                "command_palette",
                "\uE721",
                "Ouvrir Ctrl+K",
                "Lancer une action sans quitter la page.",
                "fullscreen",
                "\uE740",
                "Plein ecran",
                "Reduire le chrome quand la tache demande du calme."),
            "reading" => new ModeCompanionDefinition(
                "\uE736",
                "Lecture, annotations, notes et voix locale restent accessibles pendant la navigation.",
                "Note de lecture",
                "votre note de lecture",
                "Ex. passage à relire, idée importante, page à reprendre...",
                "reader",
                "\uE736",
                "Mode lecture",
                "Lire et annoter la page active.",
                "notes",
                "\uE70B",
                "Notes de lecture",
                "Retrouver vos notes locales."),
            "creative" => new ModeCompanionDefinition(
                "\uE70B",
                "Le post-it de creation devient un compagnon : vos idees restent proches pendant les pages web.",
                "Post-it de création",
                "votre post-it",
                "Ex. idée, brouillon, piste créative à ne pas perdre...",
                "notes",
                "\uE70B",
                "Post-it et notes",
                "Ouvrir le carnet local pour capturer ou reprendre une idee.",
                "search_assist",
                "\uE721",
                "Relancer l'idee",
                "Reformuler une piste avec l'assistant local si active."),
            "research" => new ModeCompanionDefinition(
                "\uE721",
                "Collectez et comparez sans perdre le fil : historique, favoris et actions rapides restent proches.",
                "Piste de recherche",
                "votre piste",
                "Ex. source à comparer, question à vérifier, lien ou hypothèse...",
                "history",
                "\uE81C",
                "Historique local",
                "Reprendre les pistes explorees sur cet appareil.",
                "bookmarks",
                "\uE734",
                "Sources gardees",
                "Ouvrir les favoris pour classer et comparer."),
            "night" => new ModeCompanionDefinition(
                "\uE708",
                "Un compagnon plus calme pour lire, ecouter ou reduire l'eclat sans chercher les commandes.",
                "Rappel calme",
                "votre rappel",
                "Ex. à reprendre demain, page à finir plus tard, action minimale...",
                "reader",
                "\uE736",
                "Lecture douce",
                "Basculer la page active en lecture.",
                "read_aloud",
                "\uE767",
                "Ecoute locale",
                "Ouvrir la lecture a voix haute locale."),
            _ => new ModeCompanionDefinition(
                "\uE9D2",
                "Compagnon leger : modules et commandes utiles restent disponibles sans surcharger l'accueil.",
                "Mémo Lumora",
                "votre mémo",
                "Ex. rappel de navigation, page à consulter, petite note...",
                "modules",
                "\uE713",
                "Modules Lumora",
                "Choisir les outils visibles.",
                "command_palette",
                "\uE721",
                "Actions rapides",
                "Ouvrir la palette de commande.")
        };

    private void ApplyUsageModePreset(string? usageMode)
    {
        switch ((usageMode ?? "neutral").ToLowerInvariant())
        {
            case "neutral":
                _uiSettings.NewTabStyle = "minimal";
                _uiSettings.PersonalizationMotionStyle = "subtle";
                _uiSettings.NewTabFocusSearchOnOpen = true;
                _uiSettings.NewTabShortcutsVisible = true;
                _uiSettings.CommandPaletteEnabled = true;
                _uiSettings.CompactModeEnabled = false;
                _uiSettings.CompactModeHidesBookmarks = false;
                _uiSettings.VerticalTabsEnabled = false;
                _uiSettings.BookmarksBarVisible = true;
                _uiSettings.AddressBarSuggestionsEnabled = true;
                _uiSettings.ReadAloudEnabled = false;
                _uiSettings.SearchAssistEnabled = false;
                _uiSettings.TranslationEnabled = false;
                _uiSettings.PinnedModuleIds.Clear();
                break;

            case "focus":
                _uiSettings.NewTabStyle = "minimal";
                _uiSettings.PersonalizationMotionStyle = "subtle";
                _uiSettings.NewTabFocusSearchOnOpen = true;
                _uiSettings.CommandPaletteEnabled = true;
                _uiSettings.CompactModeEnabled = true;
                _uiSettings.CompactModeHidesBookmarks = true;
                _uiSettings.VerticalTabsEnabled = false;
                break;

            case "reading":
                _uiSettings.NewTabStyle = "calm";
                _uiSettings.PersonalizationMotionStyle = "subtle";
                _uiSettings.NewTabFocusSearchOnOpen = false;
                _uiSettings.CompactModeEnabled = false;
                _uiSettings.VerticalTabsEnabled = false;
                _uiSettings.BookmarksBarVisible = true;
                AddPinnedModule("reader");
                AddPinnedModule("notes");
                AddPinnedModule("readAloud");
                _uiSettings.ReadAloudEnabled = true;
                break;

            case "creative":
                _uiSettings.NewTabStyle = "signature";
                _uiSettings.PersonalizationMotionStyle = "dynamic";
                _uiSettings.NewTabFocusSearchOnOpen = true;
                _uiSettings.CompactModeEnabled = false;
                _uiSettings.VerticalTabsEnabled = false;
                AddPinnedModule("notes");
                AddPinnedModule("searchAssist");
                _uiSettings.SearchAssistEnabled = true;
                break;

            case "research":
                _uiSettings.NewTabStyle = "signature";
                _uiSettings.PersonalizationMotionStyle = "luminous";
                _uiSettings.NewTabFocusSearchOnOpen = true;
                _uiSettings.CompactModeEnabled = false;
                _uiSettings.VerticalTabsEnabled = true;
                _uiSettings.BookmarksBarVisible = true;
                _uiSettings.AddressBarSuggestionsEnabled = true;
                AddPinnedModule("notes");
                AddPinnedModule("searchAssist");
                AddPinnedModule("translate");
                _uiSettings.SearchAssistEnabled = true;
                break;

            case "night":
                _uiSettings.ThemeMode = "dark";
                _uiSettings.NewTabStyle = "calm";
                _uiSettings.PersonalizationMotionStyle = "subtle";
                _uiSettings.NewTabFocusSearchOnOpen = false;
                _uiSettings.CompactModeEnabled = true;
                _uiSettings.CompactModeHidesBookmarks = true;
                _uiSettings.VerticalTabsEnabled = false;
                _uiSettings.WindowTransparency = Math.Max(_uiSettings.WindowTransparency, 24);
                AddPinnedModule("reader");
                AddPinnedModule("readAloud");
                _uiSettings.ReadAloudEnabled = true;
                break;

            default:
                _uiSettings.NewTabStyle = "signature";
                _uiSettings.PersonalizationMotionStyle = "luminous";
                _uiSettings.NewTabFocusSearchOnOpen = false;
                _uiSettings.CommandPaletteEnabled = true;
                _uiSettings.CompactModeEnabled = false;
                _uiSettings.CompactModeHidesBookmarks = false;
                _uiSettings.VerticalTabsEnabled = false;
                _uiSettings.BookmarksBarVisible = true;
                _uiSettings.AddressBarSuggestionsEnabled = true;
                break;
        }
    }

    private void AddPinnedModule(string moduleId)
    {
        if (_uiSettings.PinnedModuleIds.Contains(moduleId, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _uiSettings.PinnedModuleIds.Add(moduleId);
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
        RefreshNovaHomePages();
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

    private static string UsageModeLabel(string usageMode) =>
        usageMode switch
        {
            "neutral" => "Neutre",
            "focus" => "Focus",
            "reading" => "Lecture",
            "creative" => "Creation",
            "research" => "Recherche",
            "night" => "Nuit",
            _ => "Equilibre"
        };

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
        ReadingLensPanel.Visibility = Visibility.Collapsed;

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

    private sealed record ModeCompanionDefinition(
        string Icon,
        string Body,
        string MemoryTitle,
        string MemoryName,
        string MemoryPlaceholder,
        string PrimaryAction,
        string PrimaryIcon,
        string PrimaryTitle,
        string PrimaryHint,
        string SecondaryAction,
        string SecondaryIcon,
        string SecondaryTitle,
        string SecondaryHint);
}
