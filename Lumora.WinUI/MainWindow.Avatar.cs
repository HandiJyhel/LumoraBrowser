using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lumora.WinUI;

// ── Avatar de profil (local) ─────────────────────────────────────────────
// Image stockee directement dans le dossier du profil (avatar.<ext>), a
// cote du nom d'utilisateur affiche. Jamais envoyee ni synchronisee : un
// simple fichier local, comme une favicon.
public sealed partial class MainWindow
{
    // Extensions/logique de resolution partagees avec les profils non actifs
    // (selecteur, gestion des utilisateurs) via ProfileAvatarResolver
    // (Models/Profiles.cs) - garde une reference locale pour
    // ChangeAvatarButton_Click/ApplyPendingAvatarChange, qui restent propres
    // au profil actif.
    private static readonly string[] AvatarExtensions = ProfileAvatarResolver.Extensions;
    private string? _pendingAvatarSourcePath;
    private bool _pendingAvatarRemoval;

    private string? FindAvatarFile() =>
        _isGuestMode ? null : ProfileAvatarResolver.Find(_profile.ProfileDir);

    private void RefreshAvatarUi()
    {
        var avatarPath = FindAvatarFile();
        if (avatarPath is null)
        {
            ProfileAvatarBrush.ImageSource = null;
            ProfileStatusAvatarBrush.ImageSource = null;
            ProfileStatusAvatar.Visibility = Visibility.Collapsed;
            RemoveAvatarButton.Visibility = Visibility.Collapsed;
            AvatarPendingText.Visibility = Visibility.Collapsed;
            ProfileSectionAvatarBrush.ImageSource = null;
            ProfileSectionAvatarPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        var image = new BitmapImage(new Uri(avatarPath, UriKind.Absolute));
        ProfileAvatarBrush.ImageSource = image;
        ProfileStatusAvatarBrush.ImageSource = image;
        ProfileStatusAvatar.Visibility = Visibility.Visible;
        RemoveAvatarButton.Visibility = Visibility.Visible;
        AvatarPendingText.Visibility = Visibility.Collapsed;
        ProfileSectionAvatarBrush.ImageSource = image;
        ProfileSectionAvatarPlaceholder.Visibility = Visibility.Collapsed;
    }

    private async void ChangeAvatarButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode) { StatusText.Text = "Indisponible en mode invité."; return; }

        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        foreach (var ext in AvatarExtensions) picker.FileTypeFilter.Add(ext);

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            _pendingAvatarSourcePath = file.Path;
            _pendingAvatarRemoval = false;
            ProfileAvatarBrush.ImageSource = new BitmapImage(new Uri(file.Path, UriKind.Absolute));
            AvatarPendingText.Text = "Nouvelle image prête. Cliquez sur Appliquer les changements pour l'utiliser partout.";
            AvatarPendingText.Visibility = Visibility.Visible;
            MarkSettingsChangesPending("Avatar en attente de validation.");
            StatusText.Text = "Image de profil en attente de validation.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de l'aperçu de l'image : {ex.Message}";
        }
    }

    private void RemoveAvatarButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _pendingAvatarSourcePath = null;
            _pendingAvatarRemoval = true;
            ProfileAvatarBrush.ImageSource = null;
            AvatarPendingText.Text = "Suppression en attente. Cliquez sur Appliquer les changements pour confirmer.";
            AvatarPendingText.Visibility = Visibility.Visible;
            MarkSettingsChangesPending("Suppression de l'avatar en attente.");
            StatusText.Text = "Suppression de l'image en attente de validation.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de la suppression : {ex.Message}";
        }
    }

    private bool HasPendingAvatarChange() =>
        _pendingAvatarRemoval || !string.IsNullOrWhiteSpace(_pendingAvatarSourcePath);

    private bool ApplyPendingAvatarChange()
    {
        if (_isGuestMode || !HasPendingAvatarChange())
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(_profile.ProfileDir);
            if (FindAvatarFile() is { } current)
            {
                File.Delete(current);
            }

            if (!_pendingAvatarRemoval && !string.IsNullOrWhiteSpace(_pendingAvatarSourcePath))
            {
                var extension = Path.GetExtension(_pendingAvatarSourcePath).ToLowerInvariant();
                if (!AvatarExtensions.Contains(extension)) extension = ".png";
                var destination = Path.Combine(_profile.ProfileDir, "avatar" + extension);
                File.Copy(_pendingAvatarSourcePath, destination, overwrite: true);
            }

            _pendingAvatarSourcePath = null;
            _pendingAvatarRemoval = false;
            RefreshAvatarUi();
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de la validation de l'image : {ex.Message}";
            return false;
        }
    }

    private void ResetPendingAvatarChange()
    {
        _pendingAvatarSourcePath = null;
        _pendingAvatarRemoval = false;
        RefreshAvatarUi();
    }
}
