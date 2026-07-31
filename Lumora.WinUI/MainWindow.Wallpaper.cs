using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lumora.WinUI;

// ── Fond d'ecran de la page Nouvel onglet (local) ───────────────────────────
// Image stockee directement dans le dossier du profil (wallpaper.<ext>),
// comme l'avatar : jamais envoyee ni synchronisee. Limitee a la page Nouvel
// onglet (pas au chrome du navigateur) pour ne jamais risquer la lisibilite
// des boutons/texte de la barre d'outils. Encodee en data URI dans le HTML
// de la page (NavigateToString) plutot que reference par chemin de fichier :
// plus simple et plus fiable qu'un acces file:// depuis une page sans URL
// reelle, meme principe que l'avatar cote XAML (BitmapImage direct).
public sealed partial class MainWindow
{
    private static readonly string[] WallpaperExtensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp"];
    private string? _pendingWallpaperSourcePath;
    private bool _pendingWallpaperRemoval;
    private string? _wallpaperDataUriCache;
    private string? _wallpaperDataUriCachePath;
    private DateTime _wallpaperDataUriCacheWriteTimeUtc;

    private string? FindWallpaperFile()
    {
        if (_isGuestMode || string.IsNullOrEmpty(_profile.ProfileDir)) return null;
        foreach (var ext in WallpaperExtensions)
        {
            var path = Path.Combine(_profile.ProfileDir, "wallpaper" + ext);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    // Mis en cache par chemin + date d'ecriture : evite de relire et de
    // re-encoder l'image en base64 a chaque ouverture d'un nouvel onglet.
    private string? GetWallpaperDataUri()
    {
        var path = FindWallpaperFile();
        if (path is null)
        {
            _wallpaperDataUriCache = null;
            _wallpaperDataUriCachePath = null;
            return null;
        }

        var writeTimeUtc = File.GetLastWriteTimeUtc(path);
        if (_wallpaperDataUriCache is not null &&
            string.Equals(_wallpaperDataUriCachePath, path, StringComparison.OrdinalIgnoreCase) &&
            _wallpaperDataUriCacheWriteTimeUtc == writeTimeUtc)
        {
            return _wallpaperDataUriCache;
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            var mime = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "image/png"
            };
            _wallpaperDataUriCache = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            _wallpaperDataUriCachePath = path;
            _wallpaperDataUriCacheWriteTimeUtc = writeTimeUtc;
            return _wallpaperDataUriCache;
        }
        catch
        {
            return null;
        }
    }

    private void RefreshWallpaperUi()
    {
        var wallpaperPath = FindWallpaperFile();
        if (wallpaperPath is null)
        {
            WallpaperPreviewBrush.ImageSource = null;
            RemoveWallpaperButton.Visibility = Visibility.Collapsed;
            WallpaperPendingText.Visibility = Visibility.Collapsed;
            return;
        }

        WallpaperPreviewBrush.ImageSource = new BitmapImage(new Uri(wallpaperPath, UriKind.Absolute));
        RemoveWallpaperButton.Visibility = Visibility.Visible;
        WallpaperPendingText.Visibility = Visibility.Collapsed;
    }

    private async void ChangeWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode) { StatusText.Text = "Indisponible en mode invite."; return; }

        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        foreach (var ext in WallpaperExtensions) picker.FileTypeFilter.Add(ext);

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            _pendingWallpaperSourcePath = file.Path;
            _pendingWallpaperRemoval = false;
            WallpaperPreviewBrush.ImageSource = new BitmapImage(new Uri(file.Path, UriKind.Absolute));
            WallpaperPendingText.Text = "Nouveau fond d'ecran pret. Cliquez sur Appliquer les changements pour l'utiliser.";
            WallpaperPendingText.Visibility = Visibility.Visible;
            MarkSettingsChangesPending("Fond d'ecran en attente de validation.");
            StatusText.Text = "Fond d'ecran en attente de validation.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de l'apercu de l'image : {ex.Message}";
        }
    }

    private void RemoveWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _pendingWallpaperSourcePath = null;
            _pendingWallpaperRemoval = true;
            WallpaperPreviewBrush.ImageSource = null;
            WallpaperPendingText.Text = "Suppression en attente. Cliquez sur Appliquer les changements pour confirmer.";
            WallpaperPendingText.Visibility = Visibility.Visible;
            MarkSettingsChangesPending("Suppression du fond d'ecran en attente.");
            StatusText.Text = "Suppression du fond d'ecran en attente de validation.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de la suppression : {ex.Message}";
        }
    }

    private bool HasPendingWallpaperChange() =>
        _pendingWallpaperRemoval || !string.IsNullOrWhiteSpace(_pendingWallpaperSourcePath);

    private bool ApplyPendingWallpaperChange()
    {
        if (_isGuestMode || !HasPendingWallpaperChange())
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(_profile.ProfileDir);
            if (FindWallpaperFile() is { } current)
            {
                File.Delete(current);
            }

            if (!_pendingWallpaperRemoval && !string.IsNullOrWhiteSpace(_pendingWallpaperSourcePath))
            {
                var extension = Path.GetExtension(_pendingWallpaperSourcePath).ToLowerInvariant();
                if (!WallpaperExtensions.Contains(extension)) extension = ".png";
                var destination = Path.Combine(_profile.ProfileDir, "wallpaper" + extension);
                File.Copy(_pendingWallpaperSourcePath, destination, overwrite: true);
            }

            _pendingWallpaperSourcePath = null;
            _pendingWallpaperRemoval = false;
            _wallpaperDataUriCache = null;
            RefreshWallpaperUi();
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de la validation du fond d'ecran : {ex.Message}";
            return false;
        }
    }

    private void ResetPendingWallpaperChange()
    {
        _pendingWallpaperSourcePath = null;
        _pendingWallpaperRemoval = false;
        RefreshWallpaperUi();
    }
}
