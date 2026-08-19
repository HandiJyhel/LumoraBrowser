using Xunit;

namespace Lumora.Tests;

// Verrouille trois ajouts du 2026-07-22 :
// - un vrai panneau de gestion utilisateur (Modifier/Supprimer un AUTRE
//   profil exigent desormais son mot de passe, aucune notion d'admin dans
//   Lumora) ;
// - la politique "invite = Live Linux" (Personnalisation et Coffre/
//   Portefeuille disparaissent entierement de la navigation en mode invite,
//   pas seulement bloques a l'usage) ;
// - le fond d'ecran personnalisable, limite a la page Nouvel onglet.
public sealed class RealUserManagementAndGuestPolicyTests
{
    [Fact]
    public void Premier_profil_cree_recoit_un_dossier_nomme_d_apres_l_utilisateur()
    {
        // Trouve en usage reel le 2026-07-22 : le tout premier profil garde
        // l'identifiant sentinelle "default" quel que soit le prenom saisi,
        // incoherent avec tout profil suivant (nomme d'apres l'utilisateur).
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");
        var createProfile = ExtractMethod(code, "private async void CreateProfileButton_Click(object sender, RoutedEventArgs e)");

        Assert.Contains("_pendingProfileId = LumoraProfileRegistry.CreateProfileId(name);", createProfile, StringComparison.Ordinal);
        Assert.DoesNotContain("_profileEntries.Count == 0 &&", createProfile, StringComparison.Ordinal);
    }

    [Fact]
    public void Changer_de_dossier_copie_le_vrai_profil_actif_pas_un_chemin_fige_sur_default()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.SettingsStorage.cs");
        var copyMethod = ExtractMethod(code, "private void CopyProfileTo(string destDir)");

        Assert.Contains("new DirectoryInfo(_profile.ProfileDir);", copyMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("\"profiles\", \"default\"", copyMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void Modifier_et_supprimer_un_autre_profil_exigent_son_mot_de_passe()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");

        Assert.Contains(
            "private async Task<UserProfile?> RequireTargetProfilePasswordAsync(LumoraProfileEntry entry)",
            code, StringComparison.Ordinal);

        var modify = ExtractMethod(code, "private async Task ModifyProfileAsync(LumoraProfileEntry entry)");
        var delete = ExtractMethod(code, "private async Task DeleteProfileAsync(LumoraProfileEntry entry)");
        var openDir = ExtractMethod(code, "private async Task OpenProfileDirectory(LumoraProfileEntry entry)");

        Assert.Contains("RequireTargetProfilePasswordAsync(entry)", modify, StringComparison.Ordinal);
        Assert.Contains("RequireTargetProfilePasswordAsync(entry)", delete, StringComparison.Ordinal);
        Assert.Contains("RequireTargetProfilePasswordAsync(entry)", openDir, StringComparison.Ordinal);
    }

    [Fact]
    public void Panneau_gestion_utilisateur_propose_modifier_et_supprimer()
    {
        // Depuis la session "Ecran de connexion" (2026-08-10) : Modifier/Supprimer/
        // Ouvrir le dossier vivent derriere le menu "..." de chaque carte (MenuFlyoutItem,
        // propriete Text) plutot qu'en boutons visibles en permanence (Content) - la
        // fonction reste identique, seule sa presentation a change.
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");

        Assert.Contains("Text = \"Modifier\"", code, StringComparison.Ordinal);
        Assert.Contains("Text = \"Supprimer\"", code, StringComparison.Ordinal);
        Assert.Contains("Text = \"Ouvrir le dossier\"", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Reinitialiser_son_propre_profil_redemande_le_mot_de_passe()
    {
        // Trouve en revoyant l'ecran (session "Ecran de connexion", 10/08, point 4) :
        // "Reinitialiser" supprime immediatement et definitivement le dossier du
        // profil actif (contrairement a "Supprimer" un autre profil, qui met en
        // quarantaine et exige deja son mot de passe) mais ne redemandait qu'une
        // simple confirmation textuelle - l'action la plus destructrice de l'ecran
        // etait la moins protegee. Meme regle appliquee ici : redemander le mot de
        // passe du profil qu'on s'apprete a reinitialiser.
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");
        var resetMethod = ExtractMethod(code, "private async void ResetProfileButton_Click(object sender, RoutedEventArgs e)");

        // enteredPassword (variable locale) et non plus passwordBox.Password
        // directement depuis le 2026-08-14 : lire .Password sur le thread UI avant
        // Task.Run, voir Aucun_Task_Run_ne_lit_Password_directement_sur_un_controle_XAML.
        Assert.Contains("VerifyPassword(enteredPassword)", resetMethod, StringComparison.Ordinal);
        Assert.Contains("ShowSimpleDialogAsync(\"Mot de passe incorrect\"", resetMethod, StringComparison.Ordinal);
    }

    // Bug reel trouve par l'utilisateur en testant (session "Compte et ouverture",
    // 2026-08-14) : le bouton "Reinitialiser" ne faisait rien de visible - aucun
    // message, profil intact - parce que Directory.Delete echouait en silence
    // (aucun try/catch) contre le dossier webview2/ toujours verrouille par le
    // moteur Chromium en cours d'execution. Meme piege deja rencontre et corrige
    // pour le dossier ephemere invite (DeleteGuestSessionDirectoryWithRetry,
    // MainWindow.xaml.cs) - verrouille ici pour le profil reel : fermer les
    // WebView2 avant de supprimer, et rapporter un echec a l'utilisateur au lieu
    // de l'avaler en silence.
    [Fact]
    public void Reinitialiser_ferme_les_webview2_et_signale_un_echec_de_suppression()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");
        var resetMethod = ExtractMethod(code, "private async void ResetProfileButton_Click(object sender, RoutedEventArgs e)");

        Assert.Contains("tab.View?.Close();", resetMethod, StringComparison.Ordinal);
        Assert.Contains("RetryDelete.TryDeleteDirectory(_profile.ProfileDir", resetMethod, StringComparison.Ordinal);
        Assert.Contains("Réinitialisation impossible", resetMethod, StringComparison.Ordinal);
    }

    // 3e bug reel trouve par l'utilisateur sur ce meme bouton (session "Compte et
    // ouverture", 2026-08-14) - confirme par winui-runtime-trace.log
    // (resultat=None a chaque tentative) : DefaultButton=Close sur le dialogue de
    // confirmation faisait qu'appuyer sur Entree apres avoir tape le mot de passe
    // (reflexe naturel) annulait SILENCIEUSEMENT le dialogue, comme un clic sur
    // "Annuler" - aucune verification de mot de passe n'etait meme tentee. Corrige
    // en DefaultButton=None (Entree ne declenche plus rien, reste ouvert) + un
    // retour visible si le dialogue est quand meme annule (StatusText).
    [Fact]
    public void Dialogue_de_reinitialisation_n_annule_plus_silencieusement_sur_entree()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");
        var resetMethod = ExtractMethod(code, "private async void ResetProfileButton_Click(object sender, RoutedEventArgs e)");

        Assert.Contains("DefaultButton = ContentDialogButton.None", resetMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultButton = ContentDialogButton.Close", resetMethod, StringComparison.Ordinal);
        Assert.Contains("Réinitialisation annulée", resetMethod, StringComparison.Ordinal);
    }

    // Bug reel trouve par l'utilisateur (session "Compte et ouverture", 2026-08-14) :
    // "Réinitialiser ce profil..." ne faisait RIEN de visible, meme mot de passe
    // correct - confirme par winui-runtime-trace.log, un COMException 0x8001010E
    // (RPC_E_WRONG_THREAD) partait en silence a la ligne `Task.Run(() => ...
    // passwordBox.Password)`. Un objet XAML (PasswordBox) ne peut etre lu que sur
    // le thread UI ; l'accéder DANS le lambda passe a Task.Run (qui s'execute sur
    // un thread de pool) leve toujours cette exception. Trouve a 5 endroits du
    // meme fichier (verification de mot de passe pour reinitialiser/modifier un
    // profil, changer son mot de passe, definir un PIN). Le motif correct
    // (deja utilise ailleurs dans ce fichier, ex. LoginButton_Click) est de lire
    // .Password dans une variable locale AVANT Task.Run. Ce test empeche toute
    // regression future de cette classe de bug, sur tout le fichier.
    [Fact]
    public void Aucun_Task_Run_ne_lit_Password_directement_sur_un_controle_XAML()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");

        var offenders = System.Text.RegularExpressions.Regex.Matches(
            code, @"Task\.Run\([^)]*\.Password[^)]*\)");

        Assert.True(offenders.Count == 0,
            "Task.Run lit .Password directement sur un controle XAML (COMException " +
            "0x8001010E garanti) : " + string.Join(" | ", offenders.Select(m => m.Value)));
    }

    [Fact]
    public void Mode_invite_masque_personnalisation_et_coffre_de_la_navigation()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");
        var mainCode = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        // SkipProfileButton_Click relance desormais un process invite dedie
        // (GuestProcessLauncher) pour que le dossier WebView2 soit reellement
        // ephemere ; EnterGuestMode est le code qui s'execute une fois dans ce
        // process dedie et applique la politique "Live Linux" ci-dessous.
        var enterGuestMode = ExtractMethod(code, "private void EnterGuestMode()");
        Assert.Contains("SettingsNavAppearance.Visibility = Visibility.Collapsed;", enterGuestMode, StringComparison.Ordinal);
        Assert.Contains("SettingsNavVault.Visibility = Visibility.Collapsed;", enterGuestMode, StringComparison.Ordinal);

        var navClick = ExtractMethod(mainCode, "private void SettingsNav_Click(object sender, RoutedEventArgs e)");
        Assert.Contains("_isGuestMode && section is \"appearance\" or \"vault\"", navClick, StringComparison.Ordinal);
    }

    [Fact]
    public void Fond_ecran_existe_et_se_limite_au_nouvel_onglet()
    {
        var wallpaperCode = ReadRepoFile("Lumora.WinUI", "MainWindow.Wallpaper.cs");
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var newTabCode = ReadRepoFile("Lumora.WinUI", "MainWindow.NewTabHome.cs");

        Assert.Contains("private string? FindWallpaperFile()", wallpaperCode, StringComparison.Ordinal);
        Assert.Contains("private bool ApplyPendingWallpaperChange()", wallpaperCode, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WallpaperPreviewBrush\"", xaml, StringComparison.Ordinal);
        Assert.Contains("GetWallpaperDataUri()", newTabCode, StringComparison.Ordinal);

        // Jamais reference depuis le chrome du navigateur (barre d'onglets,
        // barre d'adresse...), uniquement depuis le generateur de la page
        // Nouvel onglet - portee volontairement limitee.
        Assert.DoesNotContain("GetWallpaperDataUri", ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void Selecteur_de_profil_permet_d_annuler_vers_la_session_active_sans_contourner_le_verrouillage()
    {
        // Trouve en usage reel le 2026-07-28 : ouvrir "Changer de profil" en
        // cours de session (depuis les Parametres) laissait l'ecran de
        // connexion affiche par-dessus la navigation en cours, sans aucun
        // moyen d'y revenir sans changer/creer un profil ou passer en invite.
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");

        Assert.Contains("x:Name=\"ProfilePickerCancelButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"ProfilePickerCancelButton_Click\"", xaml, StringComparison.Ordinal);

        var showProfilePicker = ExtractMethod(code, "private void ShowProfilePicker()");
        Assert.Contains(
            "(_userProfile is not null && !_vault.IsLocked) || _isGuestMode",
            showProfilePicker, StringComparison.Ordinal);

        var cancelClick = ExtractMethod(code, "private void ProfilePickerCancelButton_Click(object sender, RoutedEventArgs e)");
        Assert.Contains("DismissLoginOverlay();", cancelClick, StringComparison.Ordinal);
        Assert.Contains("(_userProfile is not null && !_vault.IsLocked) || _isGuestMode", cancelClick, StringComparison.Ordinal);

        // Regression de securite decouverte en implementant le bouton ci-dessus :
        // CreateProfileCancelButton (ajoute le 2026-07-22) souffrait du meme
        // defaut - _userProfile reste non-null pendant un verrouillage
        // (LockSessionNow purge seulement la cle du coffre), donc sans ce
        // garde-fou "Annuler" y rouvrait la navigation sans redemander le
        // mot de passe/PIN.
        Assert.Contains(
            "CreateProfileCancelButton.Visibility =\r\n                    (_userProfile is not null && !_vault.IsLocked) || _profileEntries.Count > 0",
            code, StringComparison.Ordinal);

        var createCancelClick = ExtractMethod(code, "private void CreateProfileCancelButton_Click(object sender, RoutedEventArgs e)");
        Assert.Contains("_userProfile is not null && !_vault.IsLocked", createCancelClick, StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Signature introuvable : {signature}");

        var braceOpen = source.IndexOf('{', start);
        var depth = 0;
        var i = braceOpen;
        for (; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0) break;
            }
        }

        return source[start..(i + 1)];
    }

    private static string ReadRepoFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Fichier projet introuvable: {string.Join(Path.DirectorySeparatorChar, segments)}");
    }
}
