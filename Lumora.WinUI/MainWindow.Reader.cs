using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Lumora.WinUI;

// ── Mode lecture et annotations de pages ─────────────────────────────────────
// Le bouton « Mode lecture » injecte ReaderMode.js (à la demande, jamais en
// tâche de fond) : article extrait et affiché épuré, surlignage et commentaires
// à la sélection. Les annotations vivent dans AnnotationStore
// (annotations.lumora, chiffré DPAPI) et sont réappliquées à chaque retour sur
// la page — c'est la reprise d'activité. Rien ne quitte l'appareil.
public sealed partial class MainWindow
{
    private string? _readerScriptCache;
    // Page dont l'ouverture du mode lecture est attendue après navigation
    // (clic « Ouvrir en mode lecture » depuis le panneau Notes).
    private string? _pendingReaderUrl;

    private async void ReaderModeButton_Click(object sender, RoutedEventArgs e) =>
        await ToggleReaderModeAsync();

    private async void ReaderModeMenu_Click(object sender, RoutedEventArgs e) =>
        await ToggleReaderModeAsync();

    // ensureOpen : ouverture garantie (reprise depuis le panneau Notes) au lieu
    // d'un bascule — un lecteur déjà ouvert serait fermé par le toggle.
    private async Task ToggleReaderModeAsync(bool ensureOpen = false)
    {
        var tab = CurrentTab();
        var core = tab?.View?.CoreWebView2;
        var address = tab?.View?.Source?.ToString() ?? tab?.Address ?? string.Empty;
        if (core is null || !BookmarkStore.IsWebUrl(address))
        {
            StatusText.Text = "Ouvrez une page web pour utiliser le mode lecture.";
            return;
        }

        try
        {
            _readerScriptCache ??= await LoadReaderScriptAsync();
            await core.ExecuteScriptAsync(_readerScriptCache);

            var annotations = _annotations.ForPage(address);
            var payload = JsonSerializer.Serialize(annotations.Select(annotation => new
            {
                id = annotation.Id,
                quote = annotation.Quote,
                prefix = annotation.Prefix,
                suffix = annotation.Suffix,
                comment = annotation.Comment
            }));
            var entryPoint = ensureOpen ? "open" : "toggle";
            var result = await core.ExecuteScriptAsync($"window.__lumoraReader.{entryPoint}({payload})");

            if (result.Contains("on", StringComparison.Ordinal))
            {
                StatusText.Text = annotations.Count > 0
                    ? $"Mode lecture : {annotations.Count} annotation(s) réaffichée(s). Sélectionnez du texte pour en ajouter."
                    : "Mode lecture : sélectionnez du texte pour le surligner ou le commenter.";
            }
            else
            {
                StatusText.Text = "Mode lecture quitté.";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Mode lecture indisponible : {ex.Message}";
        }
    }

    // Messages {t:"lumora.annotation"} envoyés par ReaderMode.js. Routés depuis
    // BrowserCore_WebMessageReceived.
    private void HandleReaderAnnotationMessage(CoreWebView2? core, JsonObject obj)
    {
        var action = obj["a"]?.GetValue<string>();
        switch (action)
        {
            case "add":
            {
                // L'URL de l'annotation vient de core.Source (attestee par
                // WebView2), jamais du champ JSON "u" : une page quelconque
                // pourrait sinon planter une annotation falsifiee sous
                // n'importe quelle URL de son choix (meme mecanisme de
                // confiance que OriginFromSource pour les passkeys).
                var url = core?.Source ?? string.Empty;
                var created = _annotations.Add(
                    url,
                    obj["ti"]?.GetValue<string>() ?? string.Empty,
                    obj["q"]?.GetValue<string>() ?? string.Empty,
                    obj["p"]?.GetValue<string>() ?? string.Empty,
                    obj["s"]?.GetValue<string>() ?? string.Empty,
                    obj["c"]?.GetValue<string>() ?? string.Empty);
                if (created is null) return;

                // Rendre à la page l'id définitif : le surlignage vit sous un id
                // provisoire tant que le store n'a pas répondu.
                var tempId = obj["ref"]?.GetValue<string>();
                if (core is not null && !string.IsNullOrWhiteSpace(tempId))
                {
                    _ = core.ExecuteScriptAsync(
                        $"window.__lumoraReader && window.__lumoraReader.confirmAdd({JsonSerializer.Serialize(tempId)}, {JsonSerializer.Serialize(created.Id)})");
                }

                StatusText.Text = _isGuestMode
                    ? "Passage surligné (mode invité : conservé pour cette session seulement)."
                    : "Passage surligné et enregistré.";
                UpdateReaderModeUi();
                return;
            }
            case "comment":
            {
                var id = obj["id"]?.GetValue<string>() ?? string.Empty;
                if (_annotations.UpdateComment(id, obj["c"]?.GetValue<string>() ?? string.Empty) is not null)
                {
                    StatusText.Text = "Commentaire enregistré.";
                }
                return;
            }
            case "remove":
            {
                var id = obj["id"]?.GetValue<string>() ?? string.Empty;
                if (_annotations.Remove(id))
                {
                    StatusText.Text = "Annotation supprimée.";
                    UpdateReaderModeUi();
                }
                return;
            }
            case "apply-missing":
            {
                var count = obj["n"]?.GetValue<int>() ?? 0;
                if (count > 0)
                {
                    StatusText.Text = $"{count} annotation(s) n'ont pas retrouvé leur passage (la page a peut-être changé).";
                }
                return;
            }
            case "reader-closed":
                StatusText.Text = "Mode lecture quitté.";
                return;
        }
    }

    // Bouton de la barre d'outils : visible sur les pages web, avec une pastille
    // indiquant le nombre d'annotations enregistrées sur la page courante.
    // Appelé par UpdateBookmarkStar (même cadence : navigation, changement
    // d'onglet) et après chaque ajout/suppression d'annotation.
    private void UpdateReaderModeUi(string? address = null)
    {
        if (ReaderModeButton is null) return;

        address ??= CurrentTab()?.Address;
        var isWeb = !string.IsNullOrWhiteSpace(address) && BookmarkStore.IsWebUrl(address);
        ReaderModeButton.Opacity = isWeb ? 1 : 0.72;
        UpdateModulesPinUi();
        if (!isWeb)
        {
            ReaderAnnotationBadge.Visibility = Visibility.Collapsed;
            ToolTipService.SetToolTip(ReaderModeButton, "Mode lecture - ouvrez une page web pour annoter");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ReaderModeButton, "Mode lecture - ouvrez une page web pour annoter");
            return;
        }

        var count = _annotations.CountForPage(address!);
        ReaderAnnotationBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ReaderAnnotationBadgeText.Text = count > 99 ? "99+" : count.ToString();

        var label = count > 0
            ? $"Mode lecture - {count} annotation(s) enregistrée(s) sur cette page"
            : "Mode lecture - surligner et commenter cette page";
        ToolTipService.SetToolTip(ReaderModeButton, label);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ReaderModeButton, label);
    }

    // Reprise d'activité depuis le panneau Notes : navigue vers la page puis
    // ouvre le mode lecture dès que la navigation aboutit (voir
    // BrowserView_NavigationCompleted). Si la page est déjà affichée, ouvre
    // directement.
    private async Task OpenPageInReaderAsync(string url)
    {
        var currentAddress = CurrentTab()?.View?.Source?.ToString() ?? CurrentTab()?.Address ?? string.Empty;
        ShowPanel(BrowserPanel, DisplayTitle(url));
        if (AnnotationStore.NormalizeUrl(currentAddress) == AnnotationStore.NormalizeUrl(url))
        {
            await ToggleReaderModeAsync(ensureOpen: true);
            return;
        }

        _pendingReaderUrl = AnnotationStore.NormalizeUrl(url);
        // Navigation demandée par l'utilisateur : jamais re-vérifiée par le
        // bouclier anti-redirection.
        _navHealth.RegisterExplicitNavigation(url);
        NavigateCurrentTab(url, DisplayTitle(url));
    }

    // Appelé en fin de navigation réussie sur l'onglet actif.
    private void OpenReaderIfPending(string address)
    {
        if (_pendingReaderUrl is null ||
            AnnotationStore.NormalizeUrl(address) != _pendingReaderUrl)
        {
            return;
        }

        _pendingReaderUrl = null;
        _ = ToggleReaderModeAsync(ensureOpen: true);
    }

    private static async Task<string> LoadReaderScriptAsync()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Reader", "ReaderMode.js"),
            Path.Combine(Environment.CurrentDirectory, "Lumora.WinUI", "Reader", "ReaderMode.js"),
            Path.Combine(Environment.CurrentDirectory, "Reader", "ReaderMode.js"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return await File.ReadAllTextAsync(path);
        }

        throw new FileNotFoundException("Script du mode lecture introuvable : ReaderMode.js");
    }
}
