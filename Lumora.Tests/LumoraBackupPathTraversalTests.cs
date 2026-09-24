using System.IO.Compression;
using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Faille corrigée le 2026-09-24 (audit de sécurité) : à l'import d'une
// sauvegarde, l'avatar était écrit sous le nom de son entrée d'archive
// (StartsWith("avatar.") + Path.Combine(FullName)). Une sauvegarde piégée,
// chiffrée avec un mot de passe fourni par l'attaquant, pouvait ainsi écrire
// un fichier n'importe où - par exemple dans le dossier Démarrage de Windows.
public sealed class LumoraBackupPathTraversalTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "lumora_zipslip_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }

    private string CraftBackup(params (string Name, string Content)[] entries)
    {
        using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(content);
            }
        }

        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "piege.lum");
        LumoraBackup.WriteEncryptedArchive(path, "mot-de-passe-de-l-attaquant", zipStream.ToArray());
        return path;
    }

    [Fact]
    public void Avatar_piege_ne_sort_pas_du_dossier_du_profil()
    {
        var backup = CraftBackup(("avatar./../../evade.txt", "charge malveillante"));
        var target = LumoraProfilePaths.FromDirectory(Path.Combine(_root, "profils", "cible"));
        var escapedPath = Path.GetFullPath(Path.Combine(target.ProfileDir, "avatar./../../evade.txt"));

        LumoraBackup.Import(backup, "mot-de-passe-de-l-attaquant", target, new VaultStore(target.VaultFile));

        Assert.False(File.Exists(escapedPath));
        Assert.Empty(Directory.GetFiles(_root, "evade.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public void Favicon_piege_ne_sort_pas_de_son_dossier()
    {
        var backup = CraftBackup(
            ("navigation/favicons/../../../../evade2.txt", "x"),
            ("navigation/favicons/", ""),
            ("navigation/favicons/ok.png", "png"));
        var target = LumoraProfilePaths.FromDirectory(Path.Combine(_root, "profils", "cible2"));

        LumoraBackup.Import(backup, "mot-de-passe-de-l-attaquant", target, new VaultStore(target.VaultFile));

        var outside = Directory.GetFiles(_root, "evade2.txt", SearchOption.AllDirectories)
            .Where(f => !Path.GetFullPath(f).StartsWith(Path.GetFullPath(target.FaviconsDir), StringComparison.OrdinalIgnoreCase));
        Assert.Empty(outside);
        Assert.True(File.Exists(Path.Combine(target.FaviconsDir, "ok.png")));
    }

    [Fact]
    public void Avatar_legitime_toujours_importe()
    {
        var backup = CraftBackup(("avatar.png", "image"));
        var target = LumoraProfilePaths.FromDirectory(Path.Combine(_root, "profils", "cible3"));

        LumoraBackup.Import(backup, "mot-de-passe-de-l-attaquant", target, new VaultStore(target.VaultFile));

        Assert.True(File.Exists(Path.Combine(target.ProfileDir, "avatar.png")));
    }

    [Theory]
    [InlineData("avatar.png", true)]
    [InlineData("AVATAR.JPG", true)]
    [InlineData("avatar./../../x.exe", false)]
    [InlineData("avatar.exe", false)]
    [InlineData("dossier/avatar.png", false)]
    public void Nom_d_avatar_accepte(string name, bool expected) =>
        Assert.Equal(expected, LumoraBackup.IsAvatarEntryName(name));

    [Theory]
    [InlineData("../../x.txt", "x.txt")]
    [InlineData("a/b/c.png", "c.png")]
    [InlineData("C:\\Windows\\evil.dll", "evil.dll")]
    public void Chemin_d_extraction_reste_dans_le_dossier(string entry, string expectedName)
    {
        var dir = Path.Combine(_root, "dest");
        var result = LumoraBackup.SafeExtractionPath(dir, entry);
        Assert.Equal(Path.Combine(Path.GetFullPath(dir), expectedName), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("dossier/")]
    [InlineData("..")]
    [InlineData("avatar.png:flux")]
    public void Entree_invalide_ignoree(string entry) =>
        Assert.Null(LumoraBackup.SafeExtractionPath(Path.Combine(_root, "dest"), entry));
}
