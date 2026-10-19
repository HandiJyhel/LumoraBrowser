using System.Text;

namespace Lumora.WinUI;

// Page Nouvel onglet en mode Neutre (le mode par defaut) : une page utile et
// personnelle - phrase d'accueil, prenom et avatar du profil, recherche,
// raccourcis de la personne, derniere page ouverte. Aucune lumiere decorative.
// Les autres modes d'usage gardent leur mise en page (MainWindow.NewTabHome.cs).
public sealed partial class MainWindow
{
    // Au-dela, on prefere la pastille-initiale a embarquer une grosse image
    // dans chaque nouvel onglet.
    private const long NewTabAvatarMaxBytes = 1_500_000;

    private string? _avatarDataUriCache;
    private string? _avatarDataUriCachePath;
    private DateTime _avatarDataUriCacheWriteTimeUtc;

    private string NewTabNeutralHtml()
    {
        var now = DateTime.Now;
        var firstName = _isGuestMode ? null : _userProfile?.Name;
        var phrase = NewTabText.ResolvePhrase(_uiSettings.NewTabTitle);
        var headline = phrase ?? NewTabText.Greeting(now.Hour, firstName);
        var highContrast = _uiSettings.AccessibilityHighContrast;

        var overlays = new StringBuilder();
        if (!highContrast && GetWallpaperDataUri() is not null)
        {
            overlays.Append("""<div class="neutral-veil" aria-hidden="true"></div>""");
        }
        if (!highContrast && NewTabIsDarkTheme())
        {
            overlays.Append("""<div class="neutral-grain" aria-hidden="true"></div>""");
        }

        var guestNote = _isGuestMode
            ? """<p class="neutral-note">Mode invité : rien n'est gardé quand tu fermes Lumora.</p>"""
            : string.Empty;

        return $$"""
          {{overlays}}
          <section class="neutral-home" aria-label="Nouvel onglet">
            <div class="neutral-brand">
              <img class="neutral-logo" src="{{LumoraLogoDataUri()}}" alt="" aria-hidden="true">
              <span class="neutral-name">{{NewTabMarkup.HtmlText(BrandingText.ProductName)}}</span>
            </div>
            <div class="neutral-hello">
              {{NewTabAvatarHtml(firstName)}}
              <div>
                <h1 class="neutral-phrase">{{NewTabMarkup.HtmlText(headline)}}</h1>
                <p class="neutral-date"><time datetime="{{now:yyyy-MM-dd'T'HH:mm}}">{{NewTabMarkup.HtmlText(NewTabText.DateLine(now))}}</time></p>
              </div>
            </div>
            {{NewTabSearchFormHtml("neutral-search")}}
            {{NewTabNeutralShortcutsHtml()}}
            {{NewTabResumeHtml()}}
            {{guestNote}}
          </section>
        """;
    }

    private string NewTabAvatarHtml(string? firstName)
    {
        if (_isGuestMode || string.IsNullOrWhiteSpace(firstName))
        {
            return string.Empty;
        }

        var uri = GetAvatarDataUri();
        if (uri is not null)
        {
            return $"""<img class="neutral-avatar" src="{uri}" alt="" aria-hidden="true">""";
        }

        var color = NewTabMarkup.HtmlAttribute(NewTabText.TileColor(firstName));
        var initial = NewTabMarkup.HtmlText(NewTabText.Initial(firstName));
        return $"""<span class="neutral-avatar initial" style="--tile:{color}" aria-hidden="true">{initial}</span>""";
    }

    private string? GetAvatarDataUri()
    {
        var path = FindAvatarFile();
        if (path is null)
        {
            _avatarDataUriCache = null;
            _avatarDataUriCachePath = null;
            return null;
        }

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > NewTabAvatarMaxBytes) return null;

            var writeTimeUtc = info.LastWriteTimeUtc;
            if (_avatarDataUriCache is not null &&
                string.Equals(_avatarDataUriCachePath, path, StringComparison.OrdinalIgnoreCase) &&
                _avatarDataUriCacheWriteTimeUtc == writeTimeUtc)
            {
                return _avatarDataUriCache;
            }

            var mime = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "image/png"
            };
            _avatarDataUriCache = $"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
            _avatarDataUriCachePath = path;
            _avatarDataUriCacheWriteTimeUtc = writeTimeUtc;
            return _avatarDataUriCache;
        }
        catch
        {
            return null;
        }
    }

    // Aucun raccourci : une phrase qui invite a en ajouter, plutot qu'un bouton
    // qui flotte seul. Avec des raccourcis : la grille habituelle.
    private string NewTabNeutralShortcutsHtml()
    {
        var hasShortcuts = _uiSettings.NewTabShortcuts
            .Any(shortcut => !string.IsNullOrWhiteSpace(shortcut.Title) && !string.IsNullOrWhiteSpace(shortcut.Url));
        if (hasShortcuts)
        {
            return NewTabShortcutsHtml();
        }

        return """
          <p class="neutral-empty">Aucun raccourci pour l'instant. Ajoute les sites que tu ouvres le plus, ils apparaîtront ici. <button class="neutral-link" type="button" onclick="addShortcut()">Ajouter un raccourci</button></p>
        """;
    }

    // "Tu étais sur X, il y a 12 minutes." - la derniere page reellement
    // ouverte, lue dans l'historique local. Absente en invite (historique
    // vide), si le reglage est coupe, ou si la visite date de plus d'un mois.
    private string NewTabResumeHtml()
    {
        if (_isGuestMode || !_uiSettings.NewTabShowResume || _historyPanel is null)
        {
            return string.Empty;
        }

        var entry = _historyPanel.Store.AllEntries().FirstOrDefault();
        if (entry is null) return string.Empty;

        var sentence = NewTabText.ResumeHtml(entry.Url, entry.Title, entry.VisitedAt, DateTimeOffset.Now);
        return sentence is null ? string.Empty : $"""<p class="neutral-resume">{sentence}</p>""";
    }

    private string NewTabNeutralCss()
    {
        var highContrast = _uiSettings.AccessibilityHighContrast;
        var dark = NewTabIsDarkTheme();
        var large = _uiSettings.AccessibilityLargeText;
        var palette = NewTabPalette();
        var text = NewTabTextColorCss();
        var title = NewTabLogoTextColorCss();
        var muted = NewTabModeMutedCss();
        var accent = highContrast ? "#ffd500" : palette.Accent;
        var line = highContrast ? "#fff" : (dark ? "rgba(255,248,234,.34)" : "rgba(70,60,49,.38)");
        var thickness = _uiSettings.AccessibilityVisibleFocus || highContrast ? "2px" : "1px";
        var tileRule = highContrast
            ? string.Empty
            : ".mode-neutral .shortcut-dot{background:var(--tile,var(--nt-shortcut-dot-bg));border:0;color:#fff}";

        return $$"""
        .mode-neutral{align-items:flex-start}
        .mode-neutral::after{display:none}
        .mode-neutral main{width:100%;gap:0}
        .neutral-home{width:100%;display:flex;flex-direction:column;align-items:flex-start;padding:9vh 0 24px 9%;position:relative;z-index:1}
        .neutral-brand{display:flex;align-items:center;gap:9px;margin-bottom:30px}
        .neutral-logo{width:24px;height:24px;object-fit:contain}
        .neutral-name{font-size:15px;font-weight:600;color:{{title}}}
        .neutral-hello{display:flex;align-items:center;gap:16px;margin-bottom:24px}
        .neutral-avatar{width:54px;height:54px;border-radius:50%;object-fit:cover;flex:none}
        .neutral-avatar.initial{display:flex;align-items:center;justify-content:center;background:var(--tile,#6b4a33);color:#fff;font-size:22px;font-weight:600}
        .neutral-phrase{font-size:{{(large ? "36px" : "32px")}};font-style:italic;font-weight:500;line-height:1.15;color:{{title}};margin:0;max-width:760px;overflow-wrap:anywhere}
        .neutral-date{font-size:{{(large ? "16px" : "14px")}};color:{{muted}};margin-top:5px}
        .mode-neutral .search.neutral-search{width:min(540px,100%);height:{{(large ? "52px" : "46px")}};border-radius:0;background:transparent;border:0;border-bottom:{{thickness}} solid {{line}};box-shadow:none;padding:0 2px}
        .mode-neutral .search.neutral-search:focus-within{border-bottom-color:{{accent}};box-shadow:none}
        .mode-neutral .neutral-search svg{color:{{muted}}}
        .mode-neutral .neutral-search input{color:{{text}}}
        .mode-neutral .neutral-search input::placeholder{color:{{muted}}}
        .mode-neutral .shortcuts{display:grid;grid-template-columns:repeat(auto-fill,116px);gap:18px;margin-top:34px;max-width:640px;justify-content:start}
        .mode-neutral .shortcut-card{width:auto;min-height:0;border:0;background:transparent;box-shadow:none;padding:6px;align-items:flex-start;border-radius:8px}
        .mode-neutral .shortcut-card:hover,.mode-neutral .shortcut-card:focus-within{background:transparent;border-color:transparent}
        .mode-neutral .shortcut-link{align-items:flex-start;gap:8px}
        .mode-neutral .shortcut-dot{width:48px;height:48px;border-radius:12px;font-size:20px}
        {{tileRule}}
        .mode-neutral .shortcut-title{text-align:left;font-size:13px;color:{{text}}}
        .mode-neutral .shortcut-card:hover .shortcut-title,.mode-neutral .shortcut-link:focus .shortcut-title{text-decoration:underline;text-decoration-color:{{accent}};text-underline-offset:3px}
        .mode-neutral .shortcut-link:focus-visible{outline:2px solid {{accent}};outline-offset:3px;border-radius:8px}
        .mode-neutral .add-shortcut{border:0;background:transparent}
        .mode-neutral .add-shortcut .shortcut-dot{background:transparent;border:1px solid {{line}};color:{{muted}};font-size:22px;font-weight:400}
        .neutral-empty{margin:32px 0 0;max-width:46ch;color:{{muted}};line-height:1.55;font-size:14px}
        .neutral-link{border:0;background:transparent;color:{{accent}};font:inherit;text-decoration:underline;text-underline-offset:3px;cursor:pointer;padding:0}
        .neutral-link:focus-visible{outline:2px solid {{accent}};outline-offset:3px;border-radius:4px}
        .neutral-resume{margin:30px 0 0;max-width:540px;color:{{muted}};font-size:13px;line-height:1.5}
        .neutral-resume a{color:{{text}};text-decoration:underline;text-decoration-color:{{line}};text-underline-offset:3px}
        .neutral-resume a:hover,.neutral-resume a:focus-visible{text-decoration-color:{{accent}};outline:none}
        .neutral-note{margin:22px 0 0;color:{{muted}};font-size:12.5px;font-style:italic}
        .neutral-grain{position:fixed;inset:0;pointer-events:none;opacity:.07;mix-blend-mode:screen;background-image:url("data:image/svg+xml;utf8,%3Csvg xmlns='http://www.w3.org/2000/svg' width='180' height='180'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='.85' numOctaves='2' stitchTiles='stitch'/%3E%3CfeColorMatrix values='0 0 0 0 1  0 0 0 0 .93  0 0 0 0 .82  0 0 0 .55 0'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)'/%3E%3C/svg%3E")}
        .neutral-veil{position:fixed;inset:0;pointer-events:none;background:linear-gradient(90deg,{{palette.Ink}}DB 0%,{{palette.Ink}}C7 55%,{{palette.Ink}}40 100%)}
        @media(max-width:860px){.neutral-home{padding-left:0}.neutral-phrase{font-size:{{(large ? "30px" : "26px")}}} }
        """;
    }
}
