using System.Net.Http;
using Microsoft.UI.Xaml;

namespace Lumora.WinUI;

// "Retrouver les icônes manquantes" (2026-08-22, demande explicite utilisateur
// apres capture d'ecran) : un favori importe (navigateur trop ancien, ou
// favicon absente de la base source au moment de l'import) reste sans icone
// jusqu'a ce que l'utilisateur revisite le site lui-meme (backfill passif deja
// en place, voir ApplyFaviconToUi/SetIconForOrigin dans MainWindow.Navigation.cs).
// Cette action retente une recuperation active, sans attendre cette revisite :
// simple GET vers /favicon.ico de chaque origine sans icone usable, converti
// en PNG reel (memes garde-fous que le reste de l'app - FaviconImageConverter,
// FaviconQuality) puis propage a tous les favoris de la meme origine.
//
// Repli silencieux au demarrage (2026-08-23, session "petites corrections",
// maquette approuvee) : la meme logique tourne aussi UNE FOIS par lancement,
// sans texte de statut ni bouton - une quinzaine de favoris icone-seule sans
// favicon retombaient tous sur la meme icone generique, illisible meme avec un
// espacement correct (pas le bug d'espacement deja corrige la veille). Le
// repli visuel definitif (pastille 1ere-lettre, voir BookmarkIconElement dans
// MainWindow.Bookmarks.cs) s'applique de toute facon si cette tentative
// echoue aussi - ce repli silencieux n'est qu'une chance suppplementaire
// d'obtenir la vraie icone avant d'y recourir.
public sealed partial class MainWindow : Window
{
    private static readonly HttpClient FaviconRefreshHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(6)
    };

    private bool _faviconRefreshInProgress;

    private async void RefreshMissingIconsButton_Click(object sender, RoutedEventArgs e) =>
        await RefreshMissingBookmarkIconsAsync(silent: false);

    // Appelee une seule fois au demarrage (constructeur, juste apres le premier
    // ReloadBookmarks()) : "fire-and-forget" volontaire (pas de await dans
    // l'appelant) - ne doit jamais retarder l'ouverture de la fenetre pour une
    // recuperation reseau optionnelle. Les erreurs restent dans
    // TryFetchFaviconIcoAsync (try/catch deja present), rien ne peut remonter
    // ici sans await.
    private async Task RefreshMissingBookmarkIconsAsync(bool silent)
    {
        if (_faviconRefreshInProgress)
        {
            if (!silent) StatusText.Text = "Récupération des icônes déjà en cours.";
            return;
        }

        var missingOrigins = _allBookmarkNodes
            .Where(node => node.Kind == BookmarkKind.Url &&
                           (string.IsNullOrWhiteSpace(node.IconPath) || !FaviconQuality.IsUsablePngFile(node.IconPath)))
            .Select(node => OriginOf(node.Url))
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missingOrigins.Count == 0)
        {
            if (!silent) StatusText.Text = "Tous les favoris ont déjà une icône.";
            return;
        }

        _faviconRefreshInProgress = true;
        if (!silent) StatusText.Text = $"Recherche des icônes manquantes ({missingOrigins.Count} site(s))...";
        try
        {
            var found = 0;
            using var gate = new SemaphoreSlim(6);
            var tasks = missingOrigins.Select(async origin =>
            {
                await gate.WaitAsync();
                try
                {
                    var path = await TryFetchFaviconIcoAsync(origin);
                    if (path is not null)
                    {
                        Interlocked.Increment(ref found);
                        _bookmarks.SetIconForOrigin(origin, path);
                        _faviconCache[origin] = path;
                    }
                }
                finally
                {
                    gate.Release();
                }
            });
            await Task.WhenAll(tasks);

            if (found > 0)
            {
                ReloadBookmarks();
            }
            if (!silent)
            {
                StatusText.Text = found == 0
                    ? $"Aucune icône supplémentaire trouvée sur {missingOrigins.Count} site(s)."
                    : $"{found} icône(s) retrouvée(s) sur {missingOrigins.Count} site(s) sans icône.";
            }
        }
        finally
        {
            _faviconRefreshInProgress = false;
        }
    }

    private async Task<string?> TryFetchFaviconIcoAsync(string origin)
    {
        try
        {
            var url = origin.TrimEnd('/') + "/favicon.ico";
            using var response = await FaviconRefreshHttp.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var png = await FaviconImageConverter.ToPngAsync(bytes);
            if (png is null || !FaviconQuality.IsUsablePng(png))
            {
                return null;
            }

            Directory.CreateDirectory(_profile.FaviconsDir);
            var outputPath = Path.Combine(_profile.FaviconsDir, $"{HashOrigin(origin)}.png");
            await File.WriteAllBytesAsync(outputPath, png);
            return outputPath;
        }
        catch
        {
            // Site sans favicon.ico, hors ligne, TLS invalide... : pas grave,
            // ce favori restera juste sans icone comme avant cette action.
            return null;
        }
    }
}
