using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

// Tableau de bord "Mon compte" (Parametres > Profils locaux, 0.93.45.0-dev,
// session "Recuperation") : inventaire des donnees reellement stockees dans
// le profil + etat de la derniere sauvegarde .lum. Complement de
// RefreshProfileSettings (MainWindow.Profile.cs, qui l'appelle), extrait en
// partial dedie pour ne pas alourdir davantage ce fichier deja gros - meme
// raison que MainWindow.SettingsStorage.cs.
public sealed partial class MainWindow
{
    // Onglets "Mon compte" (Aperçu/Sécurité/Sauvegarde et profils) : même motif
    // que PrivacySubNav_Click (MainWindow.xaml.cs) - un seul contenu de panneau
    // visible à la fois, bascule par le Tag du RadioButton coché. Sauvegarde et
    // Autres profils fusionnés en un seul onglet (demande explicite du
    // 2026-08-15/16 : "doivent être dans la même catégorie") - la Zone
    // dangereuse reste volontairement hors de tout onglet (ProfileDangerZonePanel,
    // MainWindow.xaml), toujours visible.
    private void AccountTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tab }) return;

        AccountTabOverviewContent.Visibility = tab == "overview" ? Visibility.Visible : Visibility.Collapsed;
        AccountTabSecurityContent.Visibility = tab == "security" ? Visibility.Visible : Visibility.Collapsed;
        AccountTabBackupContent.Visibility = tab == "backup" ? Visibility.Visible : Visibility.Collapsed;
        ResetSettingsScrollPosition();
    }

    private void RefreshAccountDashboard()
    {
        if (_userProfile is null || _isGuestMode)
        {
            return;
        }

        var bookmarkCount = _allBookmarkNodes.Count(node => node.Kind == BookmarkKind.Url);
        var historyCount = _historyPanel.Store.AllEntries().Count;
        // Coffre verrouille (mode "mot de passe maitre" pas encore devérrouille
        // cette session) : ListCredentials/ListCards renvoient une liste vide
        // par design (VaultStore.IsLocked) - afficher "Verrouillé" plutôt qu'un
        // trompeur "0", qui laisserait croire que le coffre est réellement vide.
        var credentialCount = _vault.IsLocked ? (int?)null : _vault.ListCredentials().Count;
        var cardCount = _vault.IsLocked ? (int?)null : _vault.ListCards().Count;

        AccountInventoryBookmarksCount.Text = bookmarkCount.ToString();
        AccountInventoryHistoryCount.Text = historyCount.ToString();
        AccountInventoryCredentialsCount.Text = credentialCount?.ToString() ?? "Verrouillé";
        AccountInventoryCardsCount.Text = cardCount?.ToString() ?? "Verrouillé";
        AccountInventoryTabGroupsCount.Text = _savedTabGroups.Count.ToString();
        AccountInventoryWebAppsCount.Text = _webApps.All().Count.ToString();
        AccountInventoryRssCount.Text = _rssFeeds.All().Count.ToString();
        AccountInventoryDataSizeText.Text = AccountDashboardFormatter.FormatDataSize(ComputeBackupScopeSizeBytes(_profile));

        if (AccountDashboardFormatter.HasBackup(_uiSettings.LastBackupAtUnix))
        {
            var backupAt = DateTimeOffset.FromUnixTimeSeconds(_uiSettings.LastBackupAtUnix);
            AccountLastBackupHeadlineText.Text =
                $"Dernière sauvegarde : {AccountDashboardFormatter.FormatRelativeAge(backupAt, DateTimeOffset.Now)}";
            AccountLastBackupDetailText.Text =
                $"{backupAt.LocalDateTime:dd/MM/yyyy HH:mm} · {_uiSettings.LastBackupFileName}";
        }
        else
        {
            AccountLastBackupHeadlineText.Text = "Aucune sauvegarde effectuée avec ce compte";
            AccountLastBackupDetailText.Text = "Exportez une sauvegarde pour pouvoir tout récupérer, compte inclus, en cas de besoin.";
        }
    }

    // Meme perimetre que LumoraBackup.Export (voir son commentaire d'en-tete) -
    // deliberement PAS webview2/tor/, qui sont le moteur/les caches partages,
    // pas des donnees personnelles (voir MEMORY.md,
    // idee-optimisation-empreinte-disque-profil : "le levier est webview2/,
    // pas coffre/favoris/notes"). Un profil avec un cache WebView2 volumineux
    // afficherait sinon une taille trompeuse, sans rapport avec ce qu'une
    // sauvegarde couvre reellement.
    private static long ComputeBackupScopeSizeBytes(LumoraProfilePaths profile)
    {
        long total = 0;

        // TryGetFileLength (pas File.Exists + .Length separes) : entre le test
        // d'existence et la lecture de la taille, un fichier peut disparaitre
        // (suppression concurrente, RetryDelete...) et lever une
        // FileNotFoundException non rattrapee - course TOCTOU trouvee en audit
        // le 2026-08-19. Un fichier disparu compte pour 0, jamais un plantage
        // de l'ouverture de l'onglet "Aperçu".
        foreach (var file in new[]
        {
            profile.BookmarksFile, profile.HistoryFile, profile.NotesFile, profile.AnnotationsFile,
            profile.TabsFile, profile.UiSettingsFile, profile.SavedTabGroupsFile, profile.SiteRelocationsFile,
            profile.SemanticIndexFile, profile.DownloadsFile, profile.PasskeysFile, profile.WebAppsFile,
            profile.RssFeedsFile, profile.ProfileFile, profile.VaultFile
        })
        {
            total += TryGetFileLength(file);
        }

        foreach (var dir in new[] { profile.FaviconsDir, profile.WebAppIconsDir })
        {
            if (!Directory.Exists(dir)) continue;
            IEnumerable<string> files;
            try { files = Directory.GetFiles(dir); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            foreach (var f in files) total += TryGetFileLength(f);
        }

        var avatarPath = ProfileAvatarResolver.Find(profile.ProfileDir);
        if (avatarPath is not null) total += TryGetFileLength(avatarPath);

        return total;
    }

    private static long TryGetFileLength(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
