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
using Microsoft.UI.Xaml.Automation.Peers;
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
    internal const string Version = "0.93.54.0-dev";

    // Numero de version RENDU PUBLIC, distinct du numero de version de
    // developpement ci-dessus. Les deux suivent des logiques totalement
    // separees : "Version" ci-dessus est le compteur interne granulaire
    // (palier.ajout.correctif, regi par les regles de AGENTS.md), tandis que
    // "ReleaseVersion" est le numero que verra le grand public pour une vraie
    // sortie publique - il commence a "1.0.0" par convention, independamment
    // d'ou en est le compteur interne, et n'a pas a suivre les memes regles de
    // palier. Reste `null` tant qu'aucune release n'est coupee (l'ecran A
    // propos affiche alors "Version de developpement * {Version}" comme
    // aujourd'hui) ; a renseigner ("1.0.0") au moment precis de couper une
    // vraie release, pour que l'ecran A propos n'affiche plus que ce numero.
    internal const string? ReleaseVersion = null;
    // 64 -> 40 (round 3, 2026-08-12) : retour utilisateur avec capture d'ecran
    // d'un vrai rail Edge reduit a l'appui - notre grille 2x2 de 4 icones
    // n'existait que pour loger 4 boutons d'action dans le rail. Ces 4
    // boutons sont retombes a 2 (agrandir, nouvel onglet - les plus
    // frequents) empiles en colonne simple ; muet global et recherche
    // rejoignent le menu clic droit du rail (deja cable, voir
    // WorkspaceLayoutSurface_RightTapped). 40px correspond a peu pres a la
    // largeur d'un favicon + marge, comme la reference montree.
    private const double VerticalTabsCompactWidth = 40;
    private const double VerticalTabsMinExpandedWidth = 120;
    // Valeur alignee sur UiSettings.VerticalTabsWidth (Models/UiSettings.cs) -
    // voir le commentaire la-bas pour le detail de la mesure (2026-08-12).
    // Cette constante ne sert que d'initialiseur avant chargement des
    // reglages ; UiSettings.VerticalTabsWidth est la valeur reellement
    // appliquee au demarrage.
    private const double VerticalTabsDefaultWidth = 310;
    private const double VerticalTabsMaxWidth = 320;

    private readonly LumoraProfilePaths _profile = LumoraProfilePaths.Default();
    private readonly BookmarkStore _bookmarks;
    private readonly WebAppStore _webApps;
    private RssFeedStore _rssFeeds = null!;
    private readonly ObservableCollection<BookmarkListItem> _bookmarkItems = new();
    private readonly ObservableCollection<BookmarkListItem> _bookmarkFolderItems = new();
    private readonly List<BrowserTabState> _tabs = new();
    private readonly List<TabGroup> _tabGroups = new();
    private int _nextGroupId = 1;
    private readonly HashSet<int> _collapsedGroupIds = new();
    // Identifiants des groupes vivants déjà rangés dans la bibliothèque : sert au
    // garde-fou (ne pas reproposer de garder un groupe déjà enregistré).
    private readonly HashSet<int> _savedGroupIds = new();
    // Sélection multiple d'onglets (menu contextuel : regrouper/fermer plusieurs
    // onglets d'un coup). Distincte de l'onglet actif : Ctrl/Shift+clic l'alimente
    // en plus de l'activation normale, un clic simple la vide. Non persistée.
    private readonly HashSet<int> _selectedTabIds = new();
    private int? _selectionAnchorTabId;
    // Vue divisée (0.93.4.x) : deux onglets déjà ouverts affichés côte à côte dans
    // BrowserHost, chacun gardant sa propre identité (historique, adresse, fermeture).
    // Non persistée entre les sessions - repos volontaire du scope, cf. MainWindow.SplitView.cs.
    private (int LeftId, int RightId)? _splitView;
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
    // Vrai quand ce process a ete lance via GuestProcessLauncher (--guest) :
    // _profile pointe deja vers un dossier ephemere (LUMORA_PROFILE_DIR pose
    // par le lanceur), il faut entrer en mode invite immediatement sans
    // montrer le picker de profil. Voir InitializeLoginOverlayAsync.
    private readonly bool _pendingGuestLaunch;
    // Fenêtre d'où reprendre un déverrouillage déjà fait (mono-instance /
    // "Nouvelle fenêtre" / détachement d'onglet, voir MainWindow.NewWindow.cs)
    // au lieu de repasser par l'écran de connexion. Null pour toute fenêtre
    // lancée normalement (App.xaml.cs).
    private readonly MainWindow? _unlockSource;
    // Vrai tant que la page de demarrage (ApplyStartupPage, notamment la
    // restauration des onglets de la derniere session) n'a pas encore ete
    // appliquee. Trouve en usage reel le 2026-08-12 : ApplyStartupPage() etait
    // appele directement dans le constructeur, AVANT InitializeLoginOverlayAsync -
    // un onglet restaure (site necessitant une connexion Google, par exemple)
    // commencait donc a charger et executer son JS derriere l'ecran de connexion,
    // avant meme que le profil soit deverrouille (StopAllTabNavigations, appele
    // seulement une fois l'ecran affiche, arrivait trop tard pour les requetes
    // deja parties). Desormais ApplyStartupPage() n'est declenche qu'a la sortie
    // reussie de l'ecran de connexion (DismissLoginOverlay), une seule fois par
    // process - un reverrouillage/deverrouillage en cours de session (LockSessionNow)
    // ne doit pas re-restaurer/dupliquer les onglets deja ouverts.
    private bool _pendingStartupPageApply = true;
    private bool _verticalTabsEnabled;
    private bool _verticalTabsCompact;
    // Recherche + coupe-son global du rail d'onglets verticaux (2026-08-12,
    // comparaison utilisateur avec Edge) : etat de session, pas persiste
    // (comme _verticalTabsCompact, remis a zero a chaque redemarrage).
    private string _verticalTabsFilter = string.Empty;
    private bool _allTabsMuted;
    private bool _compactModeEnabled;
    private string _tabStripPosition = "top";
    private string _bookmarksBarPosition = "top";
    // Taille de l'interface (boutons de la barre d'outils, ligne d'outils,
    // barre d'adresse, barre de favoris) : "comfortable" | "standard" |
    // "dense", voir MainWindow.UiDensity.cs. Reglage independant du Mode
    // d'usage et de _compactModeEnabled.
    private string _uiDensity = "standard";
    private bool _isFullScreenMode;
    private string _lastAnnouncedStatus = string.Empty;
    private DateTimeOffset _lastStatusAnnouncementAt = DateTimeOffset.MinValue;
    private int _lastAccessibilityShellZoneIndex = 2;
    private CoreWebView2? _contentFullScreenCore;
    // Filet de secours pour la sortie du plein ecran contenu (video...) : les
    // DEUX signaux existants (evenement WinRT ContainsFullScreenElementChanged,
    // deja documente peu fiable depuis 0.60.6-dev, et le listener JS
    // fullscreenchange qui postMessage nova.fullscreenExit) peuvent rater une
    // sortie dans le meme cas (ex. bouton plein ecran du lecteur video qui ne
    // declenche ni l'un ni l'autre chemin de facon fiable), laissant Lumora
    // bloque en chrome masque - signale par l'utilisateur le 2026-07-29. Sonde
    // directement core.ContainsFullScreenElement (l'etat reel, pas un
    // evenement) toutes les secondes tant qu'on croit etre en plein ecran
    // contenu : se corrige seul en ~1s au pire si les deux signaux ont manque.
    private DispatcherTimer? _contentFullScreenWatchdogTimer;
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
    private readonly SemanticHistoryIndex _semanticIndex;
    private readonly ObservableCollection<CommandPaletteItem> _commandPaletteItems = new();
    // Menu Demarrer (ModulesFlyout) : rail de categories (Epingles + sections)
    // a gauche, volet detail a droite - refonte "maitre/detail" a la Windows 7
    // (0.93.5.0-dev, remplace l'ancien bascule Epingles/Toutes-les-applications).
    // Toutes ces vues viennent de StartMenuTileRegistry.All + _uiSettings,
    // reconstruites ensemble par RebuildStartMenuViewModels() (MainWindow.StartMenu.cs).
    private readonly ObservableCollection<StartMenuCategoryViewModel> _startMenuCategories = new();
    private readonly ObservableCollection<StartMenuTileViewModel> _startMenuPinnedTiles = new();
    private readonly ObservableCollection<StartMenuTileViewModel> _startMenuDetailTiles = new();
    private readonly ObservableCollection<StartMenuTileViewModel> _startMenuFilteredTiles = new();
    private bool _suppressTabSave = true;
    private readonly VaultStore _vault;
    private readonly PasswordManagerService _passwordManager;
    private readonly PasswordManagerInteractionService _passwordManagerInteraction;
    private readonly CredentialService _credentialService = new();
    private ToolbarCustomizationService _toolbarCustomization = new();
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
    private readonly Dictionary<CoreWebView2, string> _geolocationSpoofScriptIds = new();
    private readonly Dictionary<CoreWebView2, string> _fingerprintProtectionScriptIds = new();
    private readonly HashSet<ScrollViewer> _hoverFocusHookedScrollViewers = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<FlyoutBase> _overlayHookedFlyouts = new(ReferenceEqualityComparer.Instance);
    // Racines (ContentHost + racine de contenu de chaque popup/flyout ouvert)
    // deja equipees du filet de secours molette hit-test - un popup peut se
    // rouvrir plusieurs fois (meme instance de contenu), ce set evite d'y
    // empiler plusieurs handlers identiques (double defilement).
    private readonly HashSet<UIElement> _wheelFallbackHookedRoots = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Popup> _overlayHookedPopups = new(ReferenceEqualityComparer.Instance);
    private bool _automaticPointerFocusBootstrapped;
    // Fixe une fois par lancement de Lumora : le bruit anti-fingerprinting reste
    // stable pour toute la session (une page qui redessine son canvas plusieurs
    // fois ne doit pas voir une empreinte differente a chaque fois), mais change
    // d'un lancement a l'autre pour ne pas devenir lui-meme un identifiant stable.
    private readonly long _fingerprintSessionSeed = Random.Shared.NextInt64(1, int.MaxValue);
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
    private bool _suppressSiteComfortUi;
    private bool _suppressSitePermissionUi;
    private static readonly System.Net.Http.HttpClient FaviconHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public MainWindow(bool startInGuestMode = false, MainWindow? unlockSource = null)
    {
        WinUiRuntimeTrace.Write("MainWindow constructor start");
        _pendingGuestLaunch = startInGuestMode;
        _unlockSource = unlockSource;
        if (_pendingGuestLaunch)
        {
            GuestProcessLauncher.CleanupStaleSessionFolders();
        }

        // Chargé avant ConfigureOnce : le drapeau anti-fuite WebRTC est un argument
        // Chromium figé au démarrage du moteur WebView2, donc il faut connaître le
        // réglage AVANT cette étape (un ApplyUiSettings ultérieur ne pourrait plus
        // le faire prendre effet pour cette session).
        _uiSettings = UiSettings.Load(_profile.UiSettingsFile, _profile.LegacyUiSettingsFile);

        // WebView2 stocke ses cookies/sessions dans le profil Lumora plutôt que dans un
        // dossier collé à l'exe (Lumora.WinUI.exe.WebView2). Conséquence : changer
        // ou supprimer un profil déconnecte réellement des sites, et la purge est complète.
        // Doit être fait AVANT toute création de moteur WebView2 dans le process. Partagé
        // avec les fenêtres d'application web (WebView2Bootstrap), qui doivent pointer
        // vers le même profil pour partager cookies et sessions.
        WebView2Bootstrap.ConfigureOnce(_profile.BrowserDataDir, _uiSettings.WebRtcLeakProtectionEnabled);

        InitializeComponent();
        WinUiRuntimeTrace.Write("MainWindow after InitializeComponent");
        Title = $"Lumora {Version}";
        UpdateAddressIdentityChrome(string.Empty);
        InitializeOpenPanelsTaskbar();

        // Initialiser le service de réorganisation de la barre d'outils
        // (0.93.34.0-dev, mode édition accessible via clic droit sur la barre
        // depuis 0.93.35.0-dev - l'appui long initial ne se déclenchait
        // quasiment jamais sur un Button, retour utilisateur direct)
        // 6 boutons ajoutes (2026-08-14, demande explicite utilisateur) :
        // favoris/incognito/bouclier/coffre/historique/telechargements,
        // deplaces depuis leurs colonnes fixes de NavigationToolbar vers
        // ToolbarButtonsPanel pour devenir reordonnables comme les modules.
        var toolbarButtonMap = new Dictionary<string, UIElement>
        {
            ["AddBookmarkButton"] = AddBookmarkButton,
            ["IncognitoToolbarButton"] = IncognitoToolbarButton,
            ["ShieldButton"] = ShieldButton,
            ["VaultQuickAccessButton"] = VaultQuickAccessButton,
            ["HistoryToolbarButton"] = HistoryToolbarButton,
            ["DownloadsIndicatorButton"] = DownloadsIndicatorButton,
            ["ReaderModeButton"] = ReaderModeButton,
            ["NotesModuleButton"] = NotesModuleButton,
            ["ReadAloudButton"] = ReadAloudButton,
            ["DetachVideoPinnedButton"] = DetachVideoPinnedButton,
            ["VideoDownloadButton"] = VideoDownloadButton,
            ["TranslatePinnedButton"] = TranslatePinnedButton,
            ["WebAppsPinnedButton"] = WebAppsPinnedButton,
            ["SearchAssistButton"] = SearchAssistButton,
            ["DictationPinnedButton"] = DictationPinnedButton,
            ["RssModuleButton"] = RssModuleButton,
            ["ReadingLensButton"] = ReadingLensButton,
            ["ModulesQuickAccessButton"] = ModulesQuickAccessButton,
            ["ConnectionsQuickAccessButton"] = ConnectionsQuickAccessButton,
            ["ConsentIndicatorButton"] = ConsentIndicatorButton,
            ["PopupRecoveryButton"] = PopupRecoveryButton,
            ["SplitViewButton"] = SplitViewButton
        };
        _toolbarCustomization.Initialize(_uiSettings, _profile.UiSettingsFile, toolbarButtonMap, ToolbarButtonsPanel);

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
        HookRawMouseWheelDiagnostics(hwnd);
        ApplyAppIcon();
        ExtendsContentIntoTitleBar = true;
        ApplyWindowTitleBarColors();
        ApplyTitleBarSafeArea();
        ApplyCustomCaptionButtons();
        RootShell.SizeChanged += (_, _) =>
        {
            UpdateTitleBarDragRegion();
            UpdateResponsiveChromeLayout();
            ApplyCustomCaptionButtons();
        };
        HookAutomaticPointerFocus();
        RootShell.Loaded += (_, _) => HookAutomaticPointerFocus();
        HookSettingsScrollDiagnostics();
        UpdateResponsiveChromeLayout();

        VerticalTabsResizeThumb.PointerEntered += (_, _) => SetCursorSizeWestEast();
        VerticalTabsResizeThumb.PointerExited  += (_, _) => RestoreDefaultCursor();

        // Saisie clavier PIN + reset du timer de session
        Content.KeyDown      += RootKeyDown;
        Content.PointerMoved += (_, _) => ResetSessionTimer();
        // SystemEvents garde une reference statique forte vers ses abonnes : se
        // desabonner a la fermeture, sinon fuite de la fenetre + crash au prochain
        // verrouillage/veille Windows.
        Closed += (_, _) => UnhookSystemLockEvents();
        // Mono-instance (App.xaml.cs, 2026-08-13) : cette fenetre reste
        // candidate pour porter une relance redirigee (OpenNewWindowFromExternalActivation,
        // MainWindow.NewWindow.cs) tant qu'elle est ouverte.
        _liveInstances.Add(this);
        Closed += (_, _) => _liveInstances.Remove(this);
        // Session invite : _profile.ProfileDir est le dossier ephemere pose par
        // GuestProcessLauncher (voir _pendingGuestLaunch/EnterGuestMode). Un vrai
        // profil ne doit JAMAIS voir son dossier supprime ici - garde explicite
        // sur _isGuestMode, qui ne devient vrai que via ce chemin invite.
        // Fermer chaque WebView2 explicitement AVANT de supprimer : sans ca, le
        // process moteur Chromium peut garder ses fichiers (Cache, LevelDB...)
        // verrouilles quelques instants apres le Closed de la fenetre WinUI, et
        // Directory.Delete echoue silencieusement (meme piege que
        // LumoraIncognitoWindow, qui ferme deja tab.View avant de supprimer son
        // dossier de session - verifie en conditions reelles : sans cette fermeture
        // explicite, le dossier invite survivait bel et bien a la fermeture).
        Closed += (_, _) =>
        {
            if (!_isGuestMode) return;
            foreach (var tab in _tabs)
            {
                try { tab.View?.Close(); } catch { }
            }
            DeleteGuestSessionDirectoryWithRetry(_profile.ProfileDir);
        };
        var commandPaletteAccelerator = new KeyboardAccelerator
        {
            Key = VirtualKey.K,
            Modifiers = VirtualKeyModifiers.Control
        };
        commandPaletteAccelerator.Invoked += CommandPaletteAccelerator_Invoked;
        Content.KeyboardAccelerators.Add(commandPaletteAccelerator);
        var incognitoWindowAccelerator = new KeyboardAccelerator
        {
            Key = VirtualKey.N,
            Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift
        };
        incognitoWindowAccelerator.Invoked += IncognitoWindowAccelerator_Invoked;
        Content.KeyboardAccelerators.Add(incognitoWindowAccelerator);
        var newWindowAccelerator = new KeyboardAccelerator
        {
            Key = VirtualKey.N,
            Modifiers = VirtualKeyModifiers.Control
        };
        newWindowAccelerator.Invoked += NewWindowAccelerator_Invoked;
        Content.KeyboardAccelerators.Add(newWindowAccelerator);
        var reopenTabAccelerator = new KeyboardAccelerator
        {
            Key = VirtualKey.T,
            Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift
        };
        reopenTabAccelerator.Invoked += ReopenClosedTabAccelerator_Invoked;
        Content.KeyboardAccelerators.Add(reopenTabAccelerator);
        RegisterAccessibilityZoneAccelerators();
        RegisterAccessibilityQuickActionAccelerators();
        RegisterAccessibilityContextAccelerators();
        RegisterAccessibilityRescueAccelerators();
        RegisterAccessibilityKeyboardShortcuts();
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
        ApplyVersionDisplay();
        LoadAboutAuthenticity();
        ApplyUiSettings();
        InitPrivacyEngine();

        ReloadBookmarks();
        // Cablage unique (pas par bouton, voir MainWindow.BookmarksDragDrop.cs
        // et MEMORY.md 2026-08-14) : les panneaux BookmarksBarPanel/
        // BookmarksBottomBarPanel existent des l'InitializeComponent et ne
        // sont jamais recrees, contrairement aux boutons qu'ils contiennent.
        WireBookmarkDragPanels();
        WinUiRuntimeTrace.Write("Bookmarks loaded");
        ReloadImportSources();
        WinUiRuntimeTrace.Write("Import sources loaded");
        _historyPanel = new HistoryPanelController(
            new HistoryStore(_profile.HistoryFile, _profile.LegacyHistoryFile),
            new DownloadHistoryStore(_profile.DownloadsFile));
        _semanticIndex = new SemanticHistoryIndex(_profile.SemanticIndexFile);
        _savedTabGroups = new SavedTabGroupStore(
            _profile.SavedTabGroupsFile, LumoraFile.TryReadAllText, LumoraFile.WriteAllText);
        _notes = new NoteStore(_profile.NotesFile);
        _rssFeeds = new RssFeedStore(_profile.RssFeedsFile);
        _annotations = new AnnotationStore(_profile.AnnotationsFile);
        _siteRelocations = new SiteRelocationStore(
            _profile.SiteRelocationsFile, LumoraFile.TryReadAllText, LumoraFile.WriteAllText);
        HistoryList.ItemsSource = _historyPanel.Items;
        CommandPaletteList.ItemsSource = _commandPaletteItems;
        AddressSuggestionsList.ItemsSource = _addressSuggestionItems;
        WinUiRuntimeTrace.Write("History store loaded");
        LoadPasskeys();
        // ApplyStartupPage() (restauration des onglets de la derniere session,
        // donc navigation reseau reelle vers des sites externes) N'EST PLUS
        // appele ici : differe a DismissLoginOverlay() pour qu'aucun onglet ne
        // commence a charger avant que l'ecran de connexion soit deverrouille
        // (voir _pendingStartupPageApply). ShowPanel bascule quand meme sur le
        // panneau navigateur (vide pour l'instant) : LoginOverlay le recouvre
        // entierement (fond opaque) tant que le profil n'est pas deverrouille.
        ShowPanel(BrowserPanel, "Accueil Lumora");
        _suppressTabSave = false;
        WinUiRuntimeTrace.Write("MainWindow constructor end");
        // Fenêtre liée (Nouvelle fenêtre/mono-instance/détacher un onglet) :
        // essaie de reprendre le déverrouillage déjà fait par _unlockSource
        // AVANT de lancer le flux normal - les deux ne doivent jamais tourner
        // en même temps (TryFastUnlockFrom, MainWindow.NewWindow.cs).
        if (_unlockSource is not null && TryFastUnlockFrom(_unlockSource))
        {
            WinUiRuntimeTrace.Write("MainWindow fast-unlocked from sibling window");
        }
        else
        {
            _ = InitializeLoginOverlayAsync();
        }
    }

    // view.Close() rend la main avant que le process moteur WebView2 sous-jacent
    // ait fini de liberer les fichiers du profil (LevelDB/SQLite) : verifie en
    // conditions reelles, un Directory.Delete immediatement apres echoue de
    // facon fiable avec IOException ("used by another process"). Quelques
    // tentatives espacees suffisent le temps que Chromium termine sa sortie - la
    // fenetre est de toute facon deja fermee a ce stade, une legere attente
    // synchrone ici est invisible pour l'utilisateur.
    // Delegue au module partage RetryDelete (Storage/RetryDelete.cs, extrait le
    // 2026-08-14 - meme mecanisme desormais reutilise par
    // ResetProfileButton_Click/MainWindow.Profile.cs) - resultat ignore ici :
    // nettoyage best-effort d'un dossier ephemere, jamais critique pour
    // l'utilisateur contrairement a une reinitialisation de profil demandee
    // explicitement.
    private static void DeleteGuestSessionDirectoryWithRetry(string path)
    {
        try { RetryDelete.TryDeleteDirectory(path, maxAttempts: 15, delayMs: 200, out _); }
        catch { /* best-effort : ne jamais faire echouer la fermeture de fenetre pour ca */ }
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
            UpdateStatusText("Moteur web désactivé pour diagnostic.");
            WinUiRuntimeTrace.Write("Browser surface skipped by environment");
            return;
        }

        WinUiRuntimeTrace.Write("InitializeBrowserSurface start");
        _browserSurfaceReady = true;

        var tab = CurrentTab();
        if (tab is null)
        {
            // Si l'ecran de connexion n'est pas encore deverrouille,
            // ApplyStartupPage() (differe a DismissLoginOverlay, voir
            // _pendingStartupPageApply) creera bientot le ou les bons onglets -
            // ne pas ajouter ici un "Nouvel onglet" de secours qui se retrouverait
            // en double a cote des onglets restaures des que l'utilisateur se
            // connecte. L'activation de la fenetre (qui appelle cette methode)
            // peut survenir avant la decision de InitializeLoginOverlayAsync.
            if (_pendingStartupPageApply) return;

            AddTab("Nouvel onglet", "lumora://accueil", select: true);
            return;
        }

        tab.PendingAddress ??= tab.Address;
        EnsureTabView(tab);
        WinUiRuntimeTrace.Write("Initial tab view requested");
    }

    private void NewTabMenu_Click(object sender, RoutedEventArgs e) =>
        AddNewBlankTab();

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
    }

    // Affiche soit "Version de developpement * X.Y.Z.W-dev" (ReleaseVersion
    // non renseignee, cas normal en developpement), soit juste "Version 1.0.0"
    // une fois qu'une vraie release est coupee (ReleaseVersion renseignee) -
    // voir le commentaire sur ReleaseVersion pour la distinction des deux
    // numeros.
    private void ApplyVersionDisplay()
    {
        if (string.IsNullOrEmpty(ReleaseVersion))
        {
            AboutVersionLabelText.Text = "Version de développement";
            AboutVersionSeparatorText.Visibility = Visibility.Visible;
            AboutVersionText.Text = Version;
        }
        else
        {
            AboutVersionLabelText.Text = "Version";
            AboutVersionSeparatorText.Visibility = Visibility.Collapsed;
            AboutVersionText.Text = ReleaseVersion;
        }
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

        // Defense en profondeur en plus du masquage des boutons de navigation
        // (voir SkipProfileButton_Click) : "Mon Lumora" et "Coffre et donnees"
        // touchent a de la personnalisation persistante et a des identifiants
        // reels - hors de portee d'une session invite (politique "Live Linux"),
        // meme si l'entree de navigation ne devrait deja plus etre cliquable.
        if (_isGuestMode && section is "appearance" or "vault")
        {
            section = "overview";
            SettingsNavOverview.IsChecked = true;
        }

        if (section == "appearance") RefreshWallpaperUi();
        if (section == "vault") RefreshVaultSettingsUi();
        // Sans cet appel, le tableau de bord "Aperçu" (favoris/historique/mots
        // de passe/...) restait figé aux valeurs du login pour toute la
        // session : RefreshAccountDashboard n'était appelée qu'au login, au
        // changement de nom ou après un export de sauvegarde, jamais en
        // revenant simplement sur cet onglet (bug réel trouvé en audit le
        // 2026-08-19).
        if (section == "profile") RefreshProfileSettings();

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
        ResetSettingsScrollPosition();
    }

    private void AppearanceSubNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton btn || btn.Tag is not string group) return;

        AppearanceGroupIdentity.Visibility  = group == "identity"  ? Visibility.Visible : Visibility.Collapsed;
        AppearanceGroupTheme.Visibility     = group == "theme"     ? Visibility.Visible : Visibility.Collapsed;
        AppearanceGroupLayout.Visibility    = group == "layout"    ? Visibility.Visible : Visibility.Collapsed;
        AppearanceGroupNewTab.Visibility    = group == "newtab"    ? Visibility.Visible : Visibility.Collapsed;
        AppearanceGroupDiscovery.Visibility = group == "discovery" ? Visibility.Visible : Visibility.Collapsed;
        ResetSettingsScrollPosition();
    }

    private void AccessibilitySubNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton btn || btn.Tag is not string group) return;

        AccessibilityGroupProfiles.Visibility   = group == "profiles"   ? Visibility.Visible : Visibility.Collapsed;
        AccessibilityGroupDisplay.Visibility    = group == "display"    ? Visibility.Visible : Visibility.Collapsed;
        AccessibilityGroupWebContent.Visibility = group == "webcontent" ? Visibility.Visible : Visibility.Collapsed;
        AccessibilityGroupReading.Visibility    = group == "reading"    ? Visibility.Visible : Visibility.Collapsed;
        AccessibilityGroupKeyboard.Visibility   = group == "keyboard"   ? Visibility.Visible : Visibility.Collapsed;
        ResetSettingsScrollPosition();
    }

    private void PrivacySubNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton btn || btn.Tag is not string group) return;

        PrivacyGroupAds.Visibility        = group == "ads"        ? Visibility.Visible : Visibility.Collapsed;
        PrivacyGroupTracking.Visibility   = group == "tracking"   ? Visibility.Visible : Visibility.Collapsed;
        PrivacyGroupConnection.Visibility = group == "connection" ? Visibility.Visible : Visibility.Collapsed;
        PrivacyGroupHygiene.Visibility    = group == "hygiene"    ? Visibility.Visible : Visibility.Collapsed;
        PrivacyGroupExceptions.Visibility = group == "exceptions" ? Visibility.Visible : Visibility.Collapsed;
        ResetSettingsScrollPosition();
    }

    private void SettingsNavigateButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string section })
        {
            return;
        }

        var target = section switch
        {
            "overview" => SettingsNavOverview,
            "appearance" => SettingsNavAppearance,
            "profile" => SettingsNavProfile,
            "accessibility" => SettingsNavAccessibility,
            "navigation" => SettingsNavNavigation,
            "startup" => SettingsNavStartup,
            "privacy" => SettingsNavPrivacy,
            "vault" => SettingsNavVault,
            "storage" => SettingsNavStorage,
            _ => null
        };

        if (target is null)
        {
            return;
        }

        target.IsChecked = true;
        SettingsNav_Click(target, new RoutedEventArgs());
    }

    private void ResetSettingsScrollPosition()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            SettingsContentScrollViewer.UpdateLayout();
            SettingsContentScrollViewer.ChangeView(null, 0, null, true);
            // FocusState.Pointer, pas Programmatic : meme lecon que
            // ScrollViewer_PointerEntered et BrowserHost_PointerEntered - deja
            // confirmee sur WebView2 comme sur les ScrollViewer natifs. Sur la
            // 1re section, la souris survole naturellement le contenu et pose
            // ce focus fiable via ScrollViewer_PointerEntered ; changer de
            // section reprogrammait un focus Programmatic qui ecrasait ce
            // focus fiable, cassant la molette jusqu'a un nouveau survol.
            SettingsContentScrollViewer.Focus(FocusState.Pointer);
            TraceSettingsScrollState("reset settings scroll position");
        });
    }

    private void HookSettingsScrollDiagnostics()
    {
        SettingsContentScrollViewer.Loaded += (_, _) => TraceSettingsScrollState("settings viewer loaded");
        SettingsContentScrollViewer.SizeChanged += (_, _) => TraceSettingsScrollState("settings viewer size changed");
        SettingsContentScrollViewer.ViewChanged += (_, e) =>
            TraceSettingsScrollState($"settings viewer view changed intermediate={e.IsIntermediate}");
        SettingsContentScrollViewer.GettingFocus += (_, _) => TraceSettingsScrollState("settings viewer getting focus");
        SettingsContentScrollViewer.PointerWheelChanged += (_, e) =>
        {
            var point = e.GetCurrentPoint(SettingsContentScrollViewer);
            WinUiRuntimeTrace.Write(
                $"Settings viewer raw wheel: delta={point.Properties.MouseWheelDelta} handled={e.Handled} verticalOffset={SettingsContentScrollViewer.VerticalOffset:F0} scrollableHeight={SettingsContentScrollViewer.ScrollableHeight:F0}");
        };
        SettingsContentScrollViewer.KeyDown += (_, e) =>
        {
            WinUiRuntimeTrace.Write(
                $"Settings viewer key down: key={e.Key} verticalOffset={SettingsContentScrollViewer.VerticalOffset:F0} scrollableHeight={SettingsContentScrollViewer.ScrollableHeight:F0}");
        };

        // Diagnostic pur (Go utilisateur du 2026-07-25, suite au test qui a
        // montre zero evenement molette sur SettingsContentScrollViewer alors
        // que le survol/focus y arrive bien) : trace posee au niveau du
        // panneau Parametres tout entier, handledEventsToo pour voir passer
        // l'evenement meme si un descendant l'a deja marque traite. Objectif :
        // savoir si la molette entre ne serait-ce qu'une fois dans l'arbre
        // XAML de Parametres, ou si elle n'y arrive jamais (auquel cas une
        // autre fenetre - potentiellement le WebView2 sous-jacent - la capte
        // avant meme que Parametres ne la voie).
        SettingsPanel.AddHandler(
            UIElement.PointerWheelChangedEvent,
            new PointerEventHandler(SettingsPanel_RootWheelDiagnostics),
            handledEventsToo: true);
    }

    private void SettingsPanel_RootWheelDiagnostics(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(SettingsPanel);
        // Ajout (Go utilisateur du 2026-07-25, suite au constat que la molette
        // s'arrete "au bout d'un moment" meme apres le correctif de focus sur
        // changement de section) : l'etat du focus au moment exact ou
        // handled bascule de True a False en continu, pour verifier
        // l'hypothese que le focus quitte le ScrollViewer (meme famille de
        // cause que ResetSettingsScrollPosition, mais declenchee ici par la
        // fin d'une animation de defilement plutot que par un changement de
        // section).
        var focusedElement = FocusManager.GetFocusedElement(SettingsPanel.XamlRoot) as FrameworkElement;
        WinUiRuntimeTrace.Write(
            $"Settings panel root wheel (diagnostic pur) : originalSource={e.OriginalSource?.GetType().Name} handled={e.Handled} delta={point.Properties.MouseWheelDelta} scrollViewerFocusState={SettingsContentScrollViewer.FocusState} focusedElement={focusedElement?.Name}({focusedElement?.GetType().Name}) browserPanelVisibility={BrowserPanel.Visibility} browserHostVisibility={BrowserHost.Visibility}");
        TraceWin32FocusState("signal molette recu par le panneau Parametres");

        // Correctif (Go utilisateur du 2026-07-25) : la trace a prouve la
        // cause reelle - le rattachement statique (une seule fois, a
        // l'ouverture du panneau) rate certains descendants regeneres
        // dynamiquement par WinUI (ex: un ContentPresenter recree juste
        // apres la fin d'une animation de defilement, une fois la vue
        // "installee" a sa position finale). Preuve directe : handled=False
        // observe alors qu'AUCUNE trace "Molette ScrollViewer" n'accompagne
        // l'evenement - ni le natif WinUI ni notre propre rattachement
        // n'ont jamais ete invoques pour ce descendant precis, quel que
        // soit le sens du defilement. Le focus (XAML et Win32) reste
        // identique avant/apres : ce n'est pas une histoire de focus.
        //
        // Premiere version de ce filet de secours (0.84.1.17-dev) : remonter
        // l'arbre visuel depuis e.OriginalSource jusqu'au premier
        // ScrollViewer ancetre trouve. Teste en reel : ne s'est JAMAIS
        // declenche dans les cas encore casses (aucune trace
        // "root-fallback-remontee-directe"), signe que la remontee
        // n'atteignait pas SettingsContentScrollViewer pour ces sources
        // precises (topologie d'arbre visuel plus complexe que prevu, ex.
        // template interne du ScrollViewer lui-meme). Simplification : ce
        // panneau n'a qu'un seul ScrollViewer de contenu pertinent
        // (SettingsContentScrollViewer) - inutile de le retrouver par une
        // remontee incertaine, on l'applique directement.
        //
        // Retire le 2026-08-02 : l'application elle-meme (qui vivait ici,
        // "root-fallback-viewer-direct") est desormais centralisee dans
        // ContentHost_WheelFallback (hit-test geometrique, inconditionnel,
        // couvre TOUS les panneaux dont celui-ci). La garder ICI en plus
        // aurait pu doubler le defilement (les deux handlers auraient
        // applique un pas chacun sur le meme ScrollViewer). Cette methode ne
        // sert plus qu'a la trace de diagnostic ci-dessus.
    }

    private void TraceSettingsScrollState(string reason)
    {
        WinUiRuntimeTrace.Write(
            $"Settings viewer state: {reason}; offset={SettingsContentScrollViewer.VerticalOffset:F0}; scrollableHeight={SettingsContentScrollViewer.ScrollableHeight:F0}; extentHeight={SettingsContentScrollViewer.ExtentHeight:F0}; viewportHeight={SettingsContentScrollViewer.ViewportHeight:F0}; computedBar={SettingsContentScrollViewer.ComputedVerticalScrollBarVisibility}");
    }

    // Le hub Modules est un panneau distinct de Parametres : contrairement aux
    // liens de SettingsNavigateButton_Click (utilises DEPUIS Parametres, ou
    // le panneau est deja visible), il faut ici afficher explicitement le
    // panneau Parametres avant de selectionner la section.
    private void ModulesProtectionsSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(SettingsPanel, "Paramètres");
        SettingsNavigateButton_Click(sender, e);
    }

    private void OpenPersonalizationSettings()
    {
        StorageCurrentFolderText.Text = _profile.ProfileDir;
        ShowPanel(SettingsPanel, "Mon Lumora");
        SettingsNavAppearance.IsChecked = true;
        SettingsNav_Click(SettingsNavAppearance, new RoutedEventArgs());
    }

    private void OpenWorkspaceSettings()
    {
        StorageCurrentFolderText.Text = _profile.ProfileDir;
        ShowPanel(SettingsPanel, "Espace de travail");
        SettingsNavNavigation.IsChecked = true;
        SettingsNav_Click(SettingsNavNavigation, new RoutedEventArgs());
    }

    private void OpenProfileSettings()
    {
        StorageCurrentFolderText.Text = _profile.ProfileDir;
        ShowPanel(SettingsPanel, "Profils locaux");
        SettingsNavProfile.IsChecked = true;
        SettingsNav_Click(SettingsNavProfile, new RoutedEventArgs());
    }

    // Tuile "Bloqueur de pub" du Menu Demarrer (StartMenuTileIds.AdBlocker) :
    // meme motif que OpenProfileSettings() - ouvre directement le
    // sous-onglet "Publicités et traceurs" de Confidentialité, ou vivent
    // NetworkBlockerSwitch/StrictAdBlockSwitch, plutot que la vue
    // d'ensemble de Confidentialité.
    private void OpenAdBlockerSettings()
    {
        StorageCurrentFolderText.Text = _profile.ProfileDir;
        ShowPanel(SettingsPanel, "Confidentialité");
        SettingsNavPrivacy.IsChecked = true;
        SettingsNav_Click(SettingsNavPrivacy, new RoutedEventArgs());
        PrivacySubNavAds.IsChecked = true;
        PrivacySubNav_Click(PrivacySubNavAds, new RoutedEventArgs());
    }

    private void ProfileStatusButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateProfileFlyoutUi();
        UpdateStatusText(_isGuestMode
            ? "Menu profil invité disponible."
            : "Menu profil Lumora disponible.");
    }

    private void OpenPersonalizationFromProfileButton_Click(object sender, RoutedEventArgs e) =>
        OpenPersonalizationSettings();

    private void SettingsOpenCommandPaletteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiSettings.CommandPaletteEnabled)
        {
            UpdateStatusText("Activez d'abord la palette de commande Ctrl+K.");
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
            UpdateStatusText("Activez d'abord la palette de commande Ctrl+K.");
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
            UpdateStatusText("Activez la lecture à voix haute dans Paramètres > Accessibilité.");
            return;
        }

        ReadAloudFlyout.ShowAt(sender as FrameworkElement ?? ModulesButton);
    }

    private void ModulesMedia_Click(object sender, RoutedEventArgs e)
    {
        ModulesFlyout.Hide();
        ShowPanel(ModulesPanel, "Modules média Lumora");
        UpdateStatusText("Modules média : détacher la vidéo ou télécharger depuis les tuiles vidéo.");
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
            UpdateStatusText("Ouvrez une page web pour utiliser la traduction.");
            return;
        }

        await OfferTranslationIfNeededAsync(view, address);
        if (TranslateBar.Visibility != Visibility.Visible)
        {
            UpdateStatusText("Aucune traduction proposée pour cette page.");
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
        RssPanel.Visibility = Visibility.Collapsed;
        VaultPanel.Visibility = Visibility.Collapsed;
        ModulesPanel.Visibility = Visibility.Collapsed;
        SiteControlPanel.Visibility = Visibility.Collapsed;
        SessionsPanel.Visibility = Visibility.Collapsed;
        WalletPanel.Visibility = Visibility.Collapsed;
        WebAppsPanel.Visibility = Visibility.Collapsed;
        ReadingLensPanel.Visibility = Visibility.Collapsed;

        visiblePanel.Visibility = Visibility.Visible;
        AttachScrollViewerPointerSupport(visiblePanel);
        UpdateStatusText(DescribePanelStatus(visiblePanel, status));

        // Depuis un panneau interne (coffre, historique, paramètres...), la barre
        // d'état propose de revenir à la page web en cours.
        BackToPageButton.Visibility = ReferenceEquals(visiblePanel, BrowserPanel)
            ? Visibility.Collapsed
            : Visibility.Visible;
        StatusBarRow.Visibility = Visibility.Visible;
        FocusVisiblePanelEntryPoint(visiblePanel);

        // Barre des taches Lumora (2026-08-18, MainWindow.OpenPanelsTaskbar.cs) :
        // point d'ancrage unique, couvre tous les appelants existants et
        // futurs de ShowPanel sans qu'aucun d'eux n'ait besoin d'etre modifie.
        SyncOpenPanelsTaskbar(visiblePanel);
    }

    private string DescribePanelStatus(FrameworkElement visiblePanel, string status)
    {
        if (ReferenceEquals(visiblePanel, BrowserPanel))
        {
            return status;
        }

        if (ReferenceEquals(visiblePanel, WalletPanel))
        {
            return $"{status}. Ajoutez une carte locale ou utilisez-en une sur la page active.";
        }

        if (ReferenceEquals(visiblePanel, SiteControlPanel))
        {
            return $"{status}. Vérifiez la sécurité du site, les permissions et le confort local.";
        }

        if (ReferenceEquals(visiblePanel, SessionsPanel))
        {
            return $"{status}. Consultez les sessions gardées et oubliez-les si nécessaire.";
        }

        if (ReferenceEquals(visiblePanel, VaultPanel))
        {
            return $"{status}. Parcourez vos identifiants, recherchez-en un ou ajoutez-en un nouveau.";
        }

        if (ReferenceEquals(visiblePanel, SettingsPanel))
        {
            return $"{status}. Réglez Lumora puis appliquez les changements.";
        }

        if (ReferenceEquals(visiblePanel, ModulesPanel))
        {
            return $"{status}. Choisissez un module ou revenez à votre page web.";
        }

        return status;
    }

    private void FocusVisiblePanelEntryPoint(FrameworkElement visiblePanel)
    {
        if (ReferenceEquals(visiblePanel, BrowserPanel))
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (FindFirstFocusableDescendant(visiblePanel) is { } target)
            {
                target.Focus(FocusState.Programmatic);
            }
        });
    }

    // Quand l'utilisateur survole une zone scrollable ou la page web, Lumora lui
    // redonne le focus automatiquement : la molette doit agir sans clic préalable.
    private void HookAutomaticPointerFocus()
    {
        if (Content is not FrameworkElement root)
        {
            return;
        }

        AttachScrollViewerPointerSupport(root);
        HookTransientOverlaySupport(root);
        HookOpenPopupsForXamlRoot(root.XamlRoot);

        if (_automaticPointerFocusBootstrapped)
        {
            return;
        }

        BrowserHost.PointerEntered += BrowserHost_PointerEntered;
        HookWheelFallbackRoot(ContentHost);
        _automaticPointerFocusBootstrapped = true;
    }

    // Re-ecrit le 2026-08-02 : ne fait plus QUE le rattachement du focus au
    // survol (utile pour PageUp/PageDown clavier) et le suivi des popups/
    // flyouts transitoires. L'application reelle de la molette est
    // centralisee dans UN SEUL endroit (ContentHost_WheelFallback, hit-test
    // geometrique frais a chaque evenement) - avant cette date, ce parcours
    // posait AUSSI un handler d'application par ScrollViewer/descendant,
    // source de tout l'historique de bugs "molette cassee par intermittence"
    // (double traitement, controle intermediaire qui marque l'evenement
    // traite avant que le bon rattachement n'ait eu lieu, rattachement raté
    // sur un element regenere par WinUI...). Un seul chemin d'application =
    // plus aucune de ces classes de bug n'est possible.
    private void AttachScrollViewerPointerSupport(DependencyObject root)
    {
        if (root is ScrollViewer viewer && _hoverFocusHookedScrollViewers.Add(viewer))
        {
            viewer.IsTabStop = true;
            viewer.PointerEntered += ScrollViewer_PointerEntered;
        }

        HookFrameworkElementTransientOverlays(root);

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            AttachScrollViewerPointerSupport(VisualTreeHelper.GetChild(root, index));
        }
    }

    private void HookTransientOverlaySupport(DependencyObject root)
    {
        HookFrameworkElementTransientOverlays(root);

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            HookTransientOverlaySupport(VisualTreeHelper.GetChild(root, index));
        }
    }

    private void HookFrameworkElementTransientOverlays(DependencyObject root)
    {
        if (root is Popup popup)
        {
            HookPopupPointerSupport(popup);
        }

        if (root is not FrameworkElement element)
        {
            return;
        }

        if (element.ContextFlyout is { } contextFlyout)
        {
            HookFlyoutPointerSupport(contextFlyout);
        }

        if (element is Button { Flyout: { } buttonFlyout })
        {
            HookFlyoutPointerSupport(buttonFlyout);
        }

        if (element is SplitButton { Flyout: { } splitButtonFlyout })
        {
            HookFlyoutPointerSupport(splitButtonFlyout);
        }

        if (element is ToggleSplitButton { Flyout: { } toggleSplitButtonFlyout })
        {
            HookFlyoutPointerSupport(toggleSplitButtonFlyout);
        }

        if (element is DropDownButton { Flyout: { } dropDownButtonFlyout })
        {
            HookFlyoutPointerSupport(dropDownButtonFlyout);
        }
    }

    private void HookFlyoutPointerSupport(FlyoutBase flyout)
    {
        if (_overlayHookedFlyouts.Add(flyout))
        {
            flyout.Opened += FlyoutPointerSupport_Opened;
            flyout.Closed += (_, _) => RefocusActiveWebViewIfVisible();
        }
    }

    // Meme motif que le rattrapage deja en place dans BrowserView_NavigationCompleted
    // (MainWindow.Navigation.cs) : le focus au survol (BrowserHost_PointerEntered) ne
    // se redeclenche QUE si le pointeur ENTRE dans la zone. Fermer un flyout/popup qui
    // recouvrait la page (Menu Lumora, menu profil, favoris, historique...) ne deplace
    // jamais le pointeur - la souris etait deja au-dessus de la page tout du long, donc
    // aucun nouveau survol n'a lieu et le focus XAML reste coince sur le dernier controle
    // du menu ferme. Symptome cote utilisateur : "la molette marche, sauf juste apres
    // avoir ferme un menu", explique une bonne partie de l'"intermittence" signalee sur
    // les pages web (2026-08-02).
    private void RefocusActiveWebViewIfVisible()
    {
        if (BrowserPanel.Visibility == Visibility.Visible && LoginOverlay.Visibility != Visibility.Visible)
        {
            CurrentTab()?.View?.Focus(FocusState.Pointer);
        }
    }

    private void FlyoutPointerSupport_Opened(object? sender, object e)
    {
        if (sender is Flyout { Content: DependencyObject content })
        {
            AttachScrollViewerPointerSupport(content);
            HookTransientOverlaySupport(content);
            if (content is UIElement contentElement)
            {
                HookWheelFallbackRoot(contentElement);
            }
        }

        if (Content is FrameworkElement root)
        {
            HookOpenPopupsForXamlRoot(root.XamlRoot);
        }
    }

    private void HookPopupPointerSupport(Popup popup)
    {
        if (_overlayHookedPopups.Add(popup))
        {
            popup.Opened += PopupPointerSupport_Opened;
            popup.Closed += (_, _) => RefocusActiveWebViewIfVisible();
        }

        if (popup.IsOpen)
        {
            HookPopupChildTree(popup);
        }
    }

    private void PopupPointerSupport_Opened(object? sender, object e)
    {
        if (sender is Popup popup)
        {
            HookPopupChildTree(popup);
        }
    }

    private void HookPopupChildTree(Popup popup)
    {
        if (popup.Child is not UIElement child)
        {
            return;
        }

        AttachScrollViewerPointerSupport(child);
        HookTransientOverlaySupport(child);
        HookWheelFallbackRoot(child);
    }

    private void HookOpenPopupsForXamlRoot(XamlRoot? xamlRoot)
    {
        if (xamlRoot is null)
        {
            return;
        }

        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
        {
            HookPopupPointerSupport(popup);
        }
    }

    // FocusState.Pointer plutot que Programmatic (meme changement que pour
    // WebView2 juste en dessous, meme famille de probleme) : l'utilisateur a
    // confirme que la molette fonctionne desormais sur les pages web mais
    // pas du tout dans les menus/panneaux natifs de Lumora eux-memes (donc
    // TOUS les ScrollViewer, pas seulement ceux dans un Flyout au-dessus de
    // WebView2 - la piste "airspace WebView2" tentee juste avant est
    // ecartee par ce retour : masquer la page pendant l'ouverture d'un menu
    // n'avait rien change, revert de ce correctif).
    private void ScrollViewer_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is ScrollViewer { Visibility: Visibility.Visible, IsEnabled: true } viewer)
        {
            viewer.Focus(FocusState.Pointer);
            TraceWin32FocusState($"survol ScrollViewer {viewer.Name}");
        }
    }

    // Filet de secours generique (Go utilisateur du 2026-07-27, suite a la
    // decouverte que seuls Parametres et le menu demarrer avaient recu le
    // correctif "root-fallback-viewer-direct" contre le bug de regeneration de
    // ContentPresenter par WinUI - tout autre panneau avec un ScrollViewer
    // (Favoris, Historique, Coffre, Notes, RSS...) restait expose au meme risque
    // sans jamais avoir ete signale par l'utilisateur. Pose une seule fois sur
    // ContentHost (ancetre commun a tous les panneaux, cf. MainWindow.xaml) au
    // lieu d'un hook par panneau : couvre aussi bien l'existant que tout futur
    // panneau, sans nouveau correctif reactif a chaque fois.
    // Re-ecrit le 2026-08-02 : l'ancienne version dependait de
    // `_lastHoveredWheelScrollViewer` (dernier ScrollViewer survole, mis a jour
    // par ScrollViewer_PointerEntered) et reculait des que `e.Handled` etait
    // deja vrai. Ca laissait la molette "cassee par intermittence" chaque fois
    // qu'un controle intermediaire consommait l'evenement sans effet visible
    // (cas trouve la veille : un ScrollViewer horizontal ; d'autres controles
    // natifs, ComboBox/Slider, peuvent faire la meme chose sans jamais avoir
    // ete signales precisement). Nouvelle approche : hit-test GEOMETRIQUE frais
    // a CHAQUE evenement (aucun etat en cache, donc rien a rater si un element
    // a ete regenere ou jamais rattache), et override INCONDITIONNEL de tout ce
    // qui aurait deja marque l'evenement traite - le defilement de la page
    // native passe desormais toujours avant un controle isole qui capterait la
    // molette sans que ce comportement n'ait jamais ete demande dans Lumora.
    // Pose le filet de secours hit-test sur une racine donnee (ContentHost pour
    // la fenetre principale, ou la racine de contenu d'un popup/flyout des son
    // ouverture - un popup ne fait PAS partie de l'arbre visuel de ContentHost,
    // un evenement molette qui y nait ne bubble jamais jusqu'a ContentHost, donc
    // chaque popup a besoin de son propre rattachement).
    private void HookWheelFallbackRoot(UIElement root)
    {
        if (!_wheelFallbackHookedRoots.Add(root))
        {
            return;
        }

        root.AddHandler(
            UIElement.PointerWheelChangedEvent,
            new PointerEventHandler((_, e) => ApplyWheelFallback(root, e)),
            handledEventsToo: true);
    }

    private void ApplyWheelFallback(UIElement hitTestRoot, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(hitTestRoot).Position;
        foreach (var hit in VisualTreeHelper.FindElementsInHostCoordinates(point, hitTestRoot))
        {
            DependencyObject? candidate = hit;
            while (candidate is not null)
            {
                if (candidate is ScrollViewer { VerticalScrollBarVisibility: not ScrollBarVisibility.Disabled } scrollViewer)
                {
                    TryApplyScrollViewerWheel(scrollViewer, e, "hit-test-root");
                    return;
                }

                candidate = VisualTreeHelper.GetParent(candidate);
            }
        }
    }

    private void TryApplyScrollViewerWheel(ScrollViewer viewer, PointerRoutedEventArgs e, string sourceLabel)
    {
        if (viewer.Visibility != Visibility.Visible ||
            !viewer.IsEnabled ||
            viewer.ScrollableHeight <= 0)
        {
            return;
        }

        var delta = e.GetCurrentPoint(viewer).Properties.MouseWheelDelta;
        if (!WheelScrollMath.TryComputeNextVerticalOffset(viewer.VerticalOffset, viewer.ScrollableHeight, delta, out var newOffset))
        {
            return;
        }

        viewer.ChangeView(null, newOffset, null, false);
        e.Handled = true;
        WinUiRuntimeTrace.Write($"Molette ScrollViewer : defilement applique via {sourceLabel} (delta={delta}, nouvel offset={newOffset:F0}).");
    }

    // FocusState.Pointer plutot que Programmatic : signale par plusieurs
    // developpeurs WebView2/WinUI3 Desktop comme plus fiable pour faire
    // remonter le focus jusqu'au contenu reel de la page (Chromium), la ou
    // Programmatic peut rester au niveau de l'enveloppe XAML sans se
    // propager - piste tentee pour "la molette reste muette meme apres un
    // survol/clic" (CoreWebView2Controller.MoveFocus, la solution la plus
    // directe documentee par Microsoft, n'est pas exposee par le controle
    // XAML WebView2 de ce SDK - verifie, pas de propriete CoreWebView2Controller
    // sur Microsoft.UI.Xaml.Controls.WebView2 dans cette version).
    private void BrowserHost_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        CurrentTab()?.View?.Focus(FocusState.Pointer);
    }

    private void BrowserView_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is WebView2 view)
        {
            view.Focus(FocusState.Pointer);
        }
    }

    private FrameworkElement? FindFirstFocusableDescendant(DependencyObject root)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is FrameworkElement element && CanReceiveProgrammaticFocus(element))
            {
                return element;
            }

            if (FindFirstFocusableDescendant(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static bool CanReceiveProgrammaticFocus(FrameworkElement element) =>
        element.Visibility == Visibility.Visible
        && element is Control control
        && control.IsEnabled
        && control.IsTabStop;

    private void UpdateStatusText(
        string text,
        bool announce = true,
        AutomationNotificationKind notificationKind = AutomationNotificationKind.Other)
    {
        StatusText.Text = text;

        if (!announce)
        {
            return;
        }

        RaiseStatusAnnouncement(text, notificationKind);
    }

    private void AnnounceAccessibilityContext(
        string text,
        AutomationNotificationKind notificationKind = AutomationNotificationKind.Other) =>
        RaiseStatusAnnouncement(text, notificationKind);

    private void RaiseStatusAnnouncement(
        string text,
        AutomationNotificationKind notificationKind = AutomationNotificationKind.Other)
    {
        var normalized = text.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (string.Equals(_lastAnnouncedStatus, normalized, StringComparison.Ordinal) &&
            now - _lastStatusAnnouncementAt < TimeSpan.FromSeconds(2))
        {
            return;
        }

        _lastAnnouncedStatus = normalized;
        _lastStatusAnnouncementAt = now;

        DispatcherQueue.TryEnqueue(() =>
        {
            var peer = FrameworkElementAutomationPeer.FromElement(StatusText)
                ?? FrameworkElementAutomationPeer.CreatePeerForElement(StatusText);
            peer?.RaiseNotificationEvent(
                notificationKind,
                AutomationNotificationProcessing.MostRecent,
                normalized,
                "LumoraStatus");
        });
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

    private void UpdateAddressIdentityChrome(string? address)
    {
        _ = address;
        UpdateResponsiveChromeLayout();
    }

    private void NavigationToolbar_SizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateResponsiveChromeLayout();

    private void UpdateResponsiveChromeLayout()
    {
        if (NavigationToolbar is null || AddressBox is null)
        {
            return;
        }

        var width = NavigationToolbar.ActualWidth;
        if (width <= 0)
        {
            return;
        }

        var collapseAddressIdentity = width < 1160;

        if (AddressIdentityBadge is not null)
        {
            AddressIdentityBadge.Visibility = collapseAddressIdentity ? Visibility.Collapsed : Visibility.Visible;
        }

        AddressBox.Padding = new Thickness(
            collapseAddressIdentity ? 18 : 54,
            8,
            18,
            8);

        if (NavigationToolbarToolsShell is not null)
        {
            NavigationToolbarToolsShell.Opacity = width < 1480 ? 0.9 : 1;
        }
    }

    private static string CompactAddressHost(string host)
    {
        var compact = host.Trim();
        if (compact.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            compact = compact[4..];
        }

        return compact.Length <= 18 ? compact : $"{compact[..15]}...";
    }

    private static string CompactAddressDraft(string value) =>
        value.Length <= 18 ? value : $"{value[..15]}...";
}
