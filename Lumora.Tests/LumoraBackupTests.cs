using System.Security.Cryptography;
using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Vérifie le round-trip de la sauvegarde chiffrée (.lum, v2 : Argon2id + AES-GCM ;
// .lumorabackup/.novabackup restent acceptés en import) et le rejet d'un mauvais
// mot de passe.
public sealed class LumoraBackupTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "nova_bak_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Export_puis_Import_restitue_les_donnees_de_navigation()
    {
        var sourceDir = Path.Combine(_root, "source");
        var source = LumoraProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);
        LumoraFile.WriteAllText(source.BookmarksFile, "signet\tperso\thttps://exemple.fr");
        var sourceVault = new VaultStore(source.VaultFile);

        // .lum : extension par defaut depuis le 2026-08-14 (session "Compte et
        // ouverture") - voir Export_puis_Import_fonctionnent_quelle_que_soit_l_extension_du_fichier
        // pour la preuve que .lumorabackup/.novabackup restent tout aussi valides.
        var backupFile = Path.Combine(_root, "sauvegarde.lum");
        LumoraBackup.Export(backupFile, "phrase-secrete", source, sourceVault);
        Assert.True(File.Exists(backupFile));

        var targetDir = Path.Combine(_root, "cible");
        var target = LumoraProfilePaths.FromDirectory(targetDir);
        var targetVault = new VaultStore(target.VaultFile);
        LumoraBackup.Import(backupFile, "phrase-secrete", target, targetVault);

        Assert.Equal(
            LumoraFile.ReadAllText(source.BookmarksFile),
            LumoraFile.ReadAllText(target.BookmarksFile));
    }

    // Le format binaire (magic "NOVABAK" + version) est totalement indifferent
    // au nom du fichier : verifie explicitement que le nouveau nom par defaut
    // (.lum) et les deux noms historiques (.lumorabackup, .novabackup) round-trip
    // tous les trois a l'identique - c'est la garantie de retrocompatibilite
    // promise pour ce chantier (aucune sauvegarde existante ne doit devenir
    // illisible).
    [Theory]
    [InlineData(".lum")]
    [InlineData(".lumorabackup")]
    [InlineData(".novabackup")]
    public void Export_puis_Import_fonctionnent_quelle_que_soit_l_extension_du_fichier(string extension)
    {
        var sourceDir = Path.Combine(_root, "source-ext" + extension.Replace(".", ""));
        var source = LumoraProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);
        LumoraFile.WriteAllText(source.BookmarksFile, "signet\tperso\thttps://exemple.fr");
        var sourceVault = new VaultStore(source.VaultFile);

        var backupFile = Path.Combine(_root, "sauvegarde-ext" + extension);
        LumoraBackup.Export(backupFile, "phrase-secrete", source, sourceVault);
        Assert.True(File.Exists(backupFile));

        var target = LumoraProfilePaths.FromDirectory(Path.Combine(_root, "cible-ext" + extension.Replace(".", "")));
        var targetVault = new VaultStore(target.VaultFile);
        LumoraBackup.Import(backupFile, "phrase-secrete", target, targetVault);

        Assert.Equal(
            LumoraFile.ReadAllText(source.BookmarksFile),
            LumoraFile.ReadAllText(target.BookmarksFile));
    }

    [Fact]
    public void Import_avec_mauvais_mot_de_passe_leve_une_exception()
    {
        var sourceDir = Path.Combine(_root, "source2");
        var source = LumoraProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);
        LumoraFile.WriteAllText(source.BookmarksFile, "signet\tperso\thttps://exemple.fr");
        var sourceVault = new VaultStore(source.VaultFile);

        var backupFile = Path.Combine(_root, "sauvegarde2.lumorabackup");
        LumoraBackup.Export(backupFile, "bon-mot-de-passe", source, sourceVault);

        var target = LumoraProfilePaths.FromDirectory(Path.Combine(_root, "cible2"));
        var targetVault = new VaultStore(target.VaultFile);
        Assert.Throws<CryptographicException>(
            () => LumoraBackup.Import(backupFile, "mauvais", target, targetVault));
    }

    // Ce qui manquait avant l'extension du 2026-08-12 : groupes d'onglets, web
    // apps, RSS, passkeys, downloads, index sémantique, favicons/icônes et avatar
    // faisaient partie du profil sur disque mais pas du .lumorabackup.
    [Fact]
    public void Export_puis_Import_restitue_les_fichiers_profil_ajoutes()
    {
        var sourceDir = Path.Combine(_root, "source3");
        var source = LumoraProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);
        LumoraFile.WriteAllText(source.SavedTabGroupsFile, "[{\"name\":\"travail\"}]");
        LumoraFile.WriteAllText(source.SiteRelocationsFile, "{}");
        LumoraFile.WriteAllText(source.SemanticIndexFile, "[]");
        LumoraFile.WriteAllText(source.DownloadsFile, "[]");
        LumoraFile.WriteAllText(source.PasskeysFile, "exemple.fr");
        LumoraFile.WriteAllText(source.WebAppsFile, "[]");
        LumoraFile.WriteAllText(source.RssFeedsFile, "[]");

        Directory.CreateDirectory(source.FaviconsDir);
        File.WriteAllBytes(Path.Combine(source.FaviconsDir, "abc.png"), [1, 2, 3]);
        Directory.CreateDirectory(source.WebAppIconsDir);
        File.WriteAllBytes(Path.Combine(source.WebAppIconsDir, "app.ico"), [4, 5, 6]);
        File.WriteAllBytes(Path.Combine(source.ProfileDir, "avatar.png"), [7, 8, 9]);
        var sourceVault = new VaultStore(source.VaultFile);

        var backupFile = Path.Combine(_root, "sauvegarde3.lumorabackup");
        LumoraBackup.Export(backupFile, "phrase-secrete", source, sourceVault);

        var targetDir = Path.Combine(_root, "cible3");
        var target = LumoraProfilePaths.FromDirectory(targetDir);
        var targetVault = new VaultStore(target.VaultFile);
        LumoraBackup.Import(backupFile, "phrase-secrete", target, targetVault);

        Assert.Equal("[{\"name\":\"travail\"}]", LumoraFile.ReadAllText(target.SavedTabGroupsFile));
        Assert.Equal("exemple.fr", LumoraFile.ReadAllText(target.PasskeysFile));
        Assert.Equal("[]", LumoraFile.ReadAllText(target.RssFeedsFile));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(target.FaviconsDir, "abc.png")));
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(Path.Combine(target.WebAppIconsDir, "app.ico")));
        Assert.Equal(new byte[] { 7, 8, 9 }, File.ReadAllBytes(Path.Combine(target.ProfileDir, "avatar.png")));
    }

    // 2026-08-13 : le coffre est desormais TOUJOURS inclus, dechiffre, quel que
    // soit son mode (portable ou DPAPI) - remplace l'ancien comportement
    // "inclus seulement si deja portable" qui laissait la majorite des
    // utilisateurs (mode DPAPI par defaut) sans leurs mots de passe dans une
    // sauvegarde presentee comme complete.
    [Fact]
    public void Export_inclut_le_coffre_portable_et_le_restitue_a_l_import()
    {
        var sourceDir = Path.Combine(_root, "source4");
        var source = LumoraProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);

        var vault = new VaultStore(source.VaultFile);
        vault.SetMasterPassword("mot-de-passe-coffre");
        vault.Upsert("https://exemple.fr", "alice", "s3cret");
        Assert.True(VaultStore.IsPortable(source.VaultFile));

        var backupFile = Path.Combine(_root, "sauvegarde4.lumorabackup");
        var result = LumoraBackup.Export(backupFile, "phrase-secrete", source, vault);
        Assert.Equal(1, result.CredentialCount);

        var target = LumoraProfilePaths.FromDirectory(Path.Combine(_root, "cible4"));
        var targetVault = new VaultStore(target.VaultFile);
        LumoraBackup.Import(backupFile, "phrase-secrete", target, targetVault);

        // Le coffre cible reste en mode DPAPI (defaut d'un profil neuf) : les
        // identifiants sont directement lisibles, aucun second mot de passe a
        // redemander - RestoreFromBackup ne force pas la reactivation du mode
        // "mot de passe maitre" d'origine.
        Assert.False(targetVault.IsLocked);
        Assert.Single(targetVault.ListCredentials());
        Assert.Equal("alice", targetVault.ListCredentials()[0].Username);
    }

    [Fact]
    public void Export_inclut_aussi_le_coffre_en_mode_dpapi_par_defaut()
    {
        var sourceDir = Path.Combine(_root, "source5");
        var source = LumoraProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);

        var vault = new VaultStore(source.VaultFile);
        vault.Upsert("https://exemple.fr", "alice", "s3cret"); // pas de mot de passe maître = mode DPAPI
        Assert.False(VaultStore.IsPortable(source.VaultFile));

        var backupFile = Path.Combine(_root, "sauvegarde5.lumorabackup");
        var result = LumoraBackup.Export(backupFile, "phrase-secrete", source, vault);
        Assert.Equal(1, result.CredentialCount);

        var target = LumoraProfilePaths.FromDirectory(Path.Combine(_root, "cible5"));
        var targetVault = new VaultStore(target.VaultFile);
        LumoraBackup.Import(backupFile, "phrase-secrete", target, targetVault);
        Assert.Single(targetVault.ListCredentials());
    }

    // L'export est bloque plutot que de continuer silencieusement sans le
    // coffre - retour utilisateur explicite (voir MEMORY.md, 2026-08-13).
    [Fact]
    public void Export_leve_si_le_coffre_portable_est_verrouille()
    {
        var sourceDir = Path.Combine(_root, "source6");
        var source = LumoraProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);

        var vault = new VaultStore(source.VaultFile);
        vault.SetMasterPassword("mot-de-passe-coffre");
        vault.Upsert("https://exemple.fr", "alice", "s3cret");
        vault.Lock();
        Assert.True(vault.IsLocked);

        var backupFile = Path.Combine(_root, "sauvegarde6.lumorabackup");
        Assert.Throws<VaultLockedForBackupException>(
            () => LumoraBackup.Export(backupFile, "phrase-secrete", source, vault));
        Assert.False(File.Exists(backupFile));
    }

    // 2026-08-15 : profile.lumora (nom + mot de passe de connexion) est chiffre
    // DIFFEREMMENT du reste du profil (UserProfile.Save = DPAPI sans entropie,
    // vs LumoraFile = DPAPI + entropie "Lumora.WinUI.v1") - un round-trip
    // Export/Import doit restituer un compte qui se reconnecte avec le MEME
    // mot de passe, pas seulement un fichier profile.lumora present sur le
    // disque cible. Couvre le bug reel signale par l'utilisateur (compte
    // absent d'une sauvegarde par ailleurs complete).
    [Fact]
    public void Export_puis_Import_restitue_le_compte_avec_le_meme_mot_de_passe()
    {
        var sourceDir = Path.Combine(_root, "source8");
        var source = LumoraProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);

        var account = UserProfile.Create("Alice", "mot-de-passe-du-compte!", "123456")
            .WithRecoveryKey("NOVA-ABCDEF-123456-GHIJKL");
        account.Save(source.ProfileFile);
        var sourceVault = new VaultStore(source.VaultFile);

        var backupFile = Path.Combine(_root, "sauvegarde8.lumorabackup");
        LumoraBackup.Export(backupFile, "phrase-secrete", source, sourceVault);

        var target = LumoraProfilePaths.FromDirectory(Path.Combine(_root, "cible8"));
        var targetVault = new VaultStore(target.VaultFile);
        LumoraBackup.Import(backupFile, "phrase-secrete", target, targetVault);

        var restored = UserProfile.Load(target.ProfileFile, target.LegacyProfileFile);
        Assert.NotNull(restored);
        Assert.Equal("Alice", restored!.Name);
        Assert.True(restored.VerifyPassword("mot-de-passe-du-compte!"));
        Assert.True(restored.VerifyPin("123456"));
        Assert.True(restored.VerifyRecoveryKey("NOVA-ABCDEF-123456-GHIJKL"));
    }

    // Cartes de paiement : meme traitement que les identifiants, verifie
    // separement puisque stockees dans un blob distinct du coffre.
    [Fact]
    public void Export_puis_Import_restitue_les_cartes_de_paiement()
    {
        var sourceDir = Path.Combine(_root, "source7");
        var source = LumoraProfilePaths.FromDirectory(sourceDir);
        Directory.CreateDirectory(source.NavigationDir);

        var vault = new VaultStore(source.VaultFile);
        vault.UpsertCard(new VaultPaymentCard
        {
            Label = "Carte perso", Holder = "Alice", Number = "4111111111111111",
            ExpMonth = 12, ExpYear = 2030
        });

        var backupFile = Path.Combine(_root, "sauvegarde7.lumorabackup");
        var result = LumoraBackup.Export(backupFile, "phrase-secrete", source, vault);
        Assert.Equal(1, result.CardCount);

        var target = LumoraProfilePaths.FromDirectory(Path.Combine(_root, "cible7"));
        var targetVault = new VaultStore(target.VaultFile);
        LumoraBackup.Import(backupFile, "phrase-secrete", target, targetVault);

        Assert.Single(targetVault.ListCards());
        Assert.Equal("Alice", targetVault.ListCards()[0].Holder);
    }
}
