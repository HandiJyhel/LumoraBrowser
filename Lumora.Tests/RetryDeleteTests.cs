using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Verrouille le comportement de RetryDelete (Lumora.WinUI/Storage/RetryDelete.cs),
// extrait le 2026-08-14 (session "Compte et ouverture") suite a un bug reel
// trouve par l'utilisateur : "Reinitialiser ce profil..." supprimait le
// dossier de profil sans aucune nouvelle tentative ni retour d'erreur, alors
// que WebView2 garde souvent ses fichiers verrouilles quelques instants apres
// sa fermeture - Directory.Delete echouait en silence. Ces tests reproduisent
// un vrai verrou de fichier (FileShare.None), pas une simulation abstraite.
public sealed class RetryDeleteTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "lumora_retrydelete_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Supprime_normalement_un_dossier_non_verrouille()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "fichier.txt"), "contenu");

        var ok = RetryDelete.TryDeleteDirectory(_root, maxAttempts: 5, delayMs: 10, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task Reussit_si_le_verrou_se_libere_pendant_les_nouvelles_tentatives()
    {
        // Reproduit exactement le scenario du bug reel : un fichier "verrouille
        // par WebView2" (ici un FileStream exclusif) qui se libere quelques
        // centaines de ms plus tard - le temps que le moteur termine sa sortie.
        Directory.CreateDirectory(_root);
        var lockedFile = Path.Combine(_root, "verrouille.txt");
        File.WriteAllText(lockedFile, "donnees");

        var stream = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var releaseTask = Task.Run(() =>
        {
            Thread.Sleep(300);
            stream.Dispose();
        });

        var ok = RetryDelete.TryDeleteDirectory(_root, maxAttempts: 15, delayMs: 100, out var error);
        await releaseTask;

        Assert.True(ok);
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public void Echoue_proprement_sans_exception_si_le_verrou_persiste()
    {
        // Le coeur du bug reel : si la suppression echoue vraiment, l'appelant
        // doit pouvoir le savoir (et le dire a l'utilisateur) au lieu que
        // l'exception parte en silence et que rien ne semble s'etre passe.
        Directory.CreateDirectory(_root);
        var lockedFile = Path.Combine(_root, "verrouille.txt");
        File.WriteAllText(lockedFile, "donnees");

        using var stream = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var ok = RetryDelete.TryDeleteDirectory(_root, maxAttempts: 3, delayMs: 20, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.True(Directory.Exists(_root));
    }

    [Fact]
    public void Dossier_deja_absent_est_un_succes_immediat()
    {
        var absent = Path.Combine(_root, "n-existe-pas");

        var ok = RetryDelete.TryDeleteDirectory(absent, maxAttempts: 5, delayMs: 10, out var error);

        Assert.True(ok);
        Assert.Null(error);
    }
}
