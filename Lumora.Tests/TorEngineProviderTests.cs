using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Lumora.WinUI.Tor;
using Xunit;

namespace Lumora.Tests;

// Teste uniquement la logique pure d'extraction (TryExtractTorExecutable) sur
// des archives tar.gz synthetiques construites en memoire. Le telechargement
// reel (DownloadEngineAsync) tape le reseau dist.torproject.org et est
// volontairement exclu des tests automatises, meme principe que
// YtDlpEngineProviderTests pour DownloadEngineAsync.
public class TorEngineProviderTests
{
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "LumoraTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string CreateSyntheticArchive(string directory, params (string Name, string Content)[] entries)
    {
        var archivePath = Path.Combine(directory, "fake-bundle.tar.gz");
        using (var fileStream = File.Create(archivePath))
        using (var gzip = new GZipStream(fileStream, CompressionLevel.Fastest))
        using (var writer = new TarWriter(gzip))
        {
            foreach (var (name, content) in entries)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, name)
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content))
                };
                writer.WriteEntry(entry);
            }
        }

        return archivePath;
    }

    [Fact]
    public void Extrait_lentree_tor_tor_exe_et_ignore_les_autres_fichiers()
    {
        var dir = CreateTempDir();
        var archivePath = CreateSyntheticArchive(
            dir,
            ("tor/tor-gencert.exe", "contenu-decoy-gencert"),
            ("tor/tor.exe", "contenu-tor-exe-de-test"),
            ("tor/pluggable_transports/lyrebird.exe", "contenu-decoy-lyrebird"));
        var destPath = Path.Combine(dir, "extracted-tor.exe");

        var found = TorEngineProvider.TryExtractTorExecutable(archivePath, destPath);

        Assert.True(found);
        Assert.Equal("contenu-tor-exe-de-test", File.ReadAllText(destPath));
    }

    [Fact]
    public void Renvoie_false_si_aucune_entree_tor_tor_exe_dans_larchive()
    {
        var dir = CreateTempDir();
        var archivePath = CreateSyntheticArchive(
            dir,
            ("tor/tor-gencert.exe", "contenu-decoy-gencert"));
        var destPath = Path.Combine(dir, "extracted-tor.exe");

        var found = TorEngineProvider.TryExtractTorExecutable(archivePath, destPath);

        Assert.False(found);
        Assert.False(File.Exists(destPath));
    }

    [Fact]
    public void Ignore_une_entree_de_meme_nom_dans_un_autre_dossier()
    {
        var dir = CreateTempDir();
        var archivePath = CreateSyntheticArchive(
            dir,
            ("autre-dossier/tor.exe", "contenu-piege"));
        var destPath = Path.Combine(dir, "extracted-tor.exe");

        var found = TorEngineProvider.TryExtractTorExecutable(archivePath, destPath);

        Assert.False(found);
    }

    [Fact]
    public void ArchiveUrl_pointe_vers_dist_torproject_org_et_contient_la_version()
    {
        var url = TorTrustedRelease.ArchiveUrl("16.0.3");

        Assert.Equal(
            "https://dist.torproject.org/torbrowser/16.0.3/tor-expert-bundle-windows-x86_64-16.0.3.tar.gz", url);
        Assert.Equal(url + ".asc", TorTrustedRelease.SignatureUrl("16.0.3"));
    }

    [Fact]
    public void L_empreinte_epinglee_est_une_empreinte_openpgp_v4_de_40_hex()
    {
        Assert.Matches("^[0-9A-F]{40}$", TorTrustedRelease.PinnedSigningKeyFingerprint);
        Assert.StartsWith("https://keys.openpgp.org/", TorTrustedRelease.SigningKeyRefreshUrl);
    }

    // ---- Lecture de l'index des versions ----------------------------------

    private const string IndexExtrait = """
        <a href="../">Parent Directory</a>
        <a href="13.5.7/">13.5.7/</a>
        <a href="15.0.20/">15.0.20/</a>
        <a href="16.0.3/">16.0.3/</a>
        <a href="16.0.10/">16.0.10/</a>
        <a href="16.0a14/">16.0a14/</a>
        <a href="17.0b2/">17.0b2/</a>
        <a href="tbb-nightly/">tbb-nightly/</a>
        <a href="16.0.3/">16.0.3/</a>
        """;

    [Fact]
    public void L_index_ne_garde_que_les_versions_stables_triees_de_la_plus_recente_a_la_plus_ancienne()
    {
        var versions = TorReleaseFeed.ParseStableVersions(IndexExtrait);

        Assert.Equal(new[] { "16.0.10", "16.0.3", "15.0.20", "13.5.7" }, versions.Select(v => v.Text));
    }

    [Fact]
    public void Les_versions_sont_comparees_numeriquement_pas_alphabetiquement()
    {
        var versions = TorReleaseFeed.ParseStableVersions(IndexExtrait);

        Assert.Equal("16.0.10", versions[0].Text);
        Assert.True(versions[0].Value > versions[1].Value);
    }

    [Fact]
    public void Une_version_sans_numero_de_correctif_equivaut_a_celle_avec_zero()
    {
        Assert.True(TorReleaseFeed.TryParse("16.0", out var a));
        Assert.True(TorReleaseFeed.TryParse("16.0.0", out var b));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Un_index_sans_version_donne_une_liste_vide()
    {
        Assert.Empty(TorReleaseFeed.ParseStableVersions("<html>rien</html>"));
        Assert.Empty(TorReleaseFeed.ParseStableVersions(""));
    }

    // ---- Verification de signature OpenPGP, sur de VRAIS fichiers de Tor ----
    // sha256sums-signed-build.txt(.asc) de Tor Browser 16.0.3, recuperes sur
    // dist.torproject.org le 2026-11-25 et verifies avec gpg (Good signature,
    // sous-cle 022D A248 ... D62D 07D9 liee a la cle principale EF6E 286D ...).

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Tor", name);

    private static string ReadSignature() => File.ReadAllText(FixturePath("sha256sums-signed-build.txt.asc"));

    private static TorSignatureResult Verify(
        byte[] data, string signature, string keyBlock, string fingerprint) =>
        TorSignatureVerifier.VerifyDetached(new MemoryStream(data), signature, keyBlock, fingerprint);

    private static byte[] ReadSignedData() => File.ReadAllBytes(FixturePath("sha256sums-signed-build.txt"));

    [Fact]
    public void La_vraie_signature_de_tor_est_acceptee_avec_la_cle_embarquee()
    {
        var result = Verify(
            ReadSignedData(), ReadSignature(),
            TorSigningKey.ArmoredPublicKeyBlock, TorTrustedRelease.PinnedSigningKeyFingerprint);

        Assert.Equal(TorSignatureOutcome.Valid, result.Outcome);
    }

    [Fact]
    public void Un_seul_octet_modifie_dans_le_fichier_signe_est_refuse()
    {
        var data = ReadSignedData();
        data[10] ^= 0x01;

        var result = Verify(
            data, ReadSignature(),
            TorSigningKey.ArmoredPublicKeyBlock, TorTrustedRelease.PinnedSigningKeyFingerprint);

        Assert.Equal(TorSignatureOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Un_fichier_tronque_est_refuse()
    {
        var data = ReadSignedData();

        var result = Verify(
            data[..(data.Length - 1)], ReadSignature(),
            TorSigningKey.ArmoredPublicKeyBlock, TorTrustedRelease.PinnedSigningKeyFingerprint);

        Assert.Equal(TorSignatureOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Une_signature_alteree_est_refusee_sans_planter()
    {
        var lines = ReadSignature().Split('\n').ToList();
        // Premiere ligne du corps base64 (apres l'en-tete et la ligne vide).
        var bodyIndex = lines.FindIndex(l => l.Length > 20 && !l.StartsWith("-----") && !l.StartsWith("="));
        var original = lines[bodyIndex];
        var replacement = original[10] == 'A' ? 'B' : 'A';
        lines[bodyIndex] = original[..10] + replacement + original[11..];

        var result = Verify(
            ReadSignedData(), string.Join('\n', lines),
            TorSigningKey.ArmoredPublicKeyBlock, TorTrustedRelease.PinnedSigningKeyFingerprint);

        Assert.NotEqual(TorSignatureOutcome.Valid, result.Outcome);
    }

    [Fact]
    public void Une_signature_vide_ou_du_texte_quelconque_est_refuse()
    {
        foreach (var junk in new[] { "", "pas une signature", "-----BEGIN PGP SIGNATURE-----\n\n-----END PGP SIGNATURE-----" })
        {
            var result = Verify(
                ReadSignedData(), junk,
                TorSigningKey.ArmoredPublicKeyBlock, TorTrustedRelease.PinnedSigningKeyFingerprint);

            Assert.Equal(TorSignatureOutcome.Invalid, result.Outcome);
        }
    }

    [Fact]
    public void Une_autre_empreinte_epinglee_est_refusee_meme_avec_une_signature_correcte()
    {
        var result = Verify(
            ReadSignedData(), ReadSignature(),
            TorSigningKey.ArmoredPublicKeyBlock, new string('A', 40));

        Assert.Equal(TorSignatureOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Un_bloc_de_cle_qui_n_est_pas_celui_de_tor_est_refuse()
    {
        var result = Verify(
            ReadSignedData(), ReadSignature(), "pas un bloc de cle", TorTrustedRelease.PinnedSigningKeyFingerprint);

        Assert.Equal(TorSignatureOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Une_sous_cle_absente_du_bloc_signale_une_cle_inconnue_pour_declencher_le_rafraichissement()
    {
        // Simule la rotation de la sous-cle de signature : bloc de cle depuis
        // lequel la sous-cle qui a signe Tor 16.0.3 a ete retiree.
        var stripped = RemoveSigningSubkey(TorSigningKey.ArmoredPublicKeyBlock, 0x316C1FACD62D07D9L);

        var result = Verify(
            ReadSignedData(), ReadSignature(), stripped, TorTrustedRelease.PinnedSigningKeyFingerprint);

        Assert.Equal(TorSignatureOutcome.SignerKeyUnknown, result.Outcome);
    }

    [Fact]
    public void Une_sous_cle_sans_signature_de_liaison_valide_est_refusee()
    {
        // Un attaquant qui fournirait un bloc ou la sous-cle n'est plus liee a
        // la cle principale (signature de liaison retiree) ne doit pas passer.
        var unbound = RemoveBindingSignatures(TorSigningKey.ArmoredPublicKeyBlock, 0x316C1FACD62D07D9L);

        var result = Verify(
            ReadSignedData(), ReadSignature(), unbound, TorTrustedRelease.PinnedSigningKeyFingerprint);

        Assert.Equal(TorSignatureOutcome.Invalid, result.Outcome);
    }

    private static string RemoveSigningSubkey(string armoredKeyBlock, long keyId)
    {
        var ring = ReadRing(armoredKeyBlock);
        var subkey = ring.GetPublicKey(keyId);
        return Armor(Org.BouncyCastle.Bcpg.OpenPgp.PgpPublicKeyRing.RemovePublicKey(ring, subkey));
    }

    private static string RemoveBindingSignatures(string armoredKeyBlock, long keyId)
    {
        var ring = ReadRing(armoredKeyBlock);
        var subkey = ring.GetPublicKey(keyId);
        var stripped = subkey;
        foreach (Org.BouncyCastle.Bcpg.OpenPgp.PgpSignature binding in
            subkey.GetSignaturesOfType(Org.BouncyCastle.Bcpg.OpenPgp.PgpSignature.SubkeyBinding).ToList())
        {
            stripped = Org.BouncyCastle.Bcpg.OpenPgp.PgpPublicKey.RemoveCertification(stripped, binding);
        }

        return Armor(Org.BouncyCastle.Bcpg.OpenPgp.PgpPublicKeyRing.InsertPublicKey(ring, stripped));
    }

    private static Org.BouncyCastle.Bcpg.OpenPgp.PgpPublicKeyRing ReadRing(string armoredKeyBlock)
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(armoredKeyBlock));
        using var decoded = Org.BouncyCastle.Bcpg.OpenPgp.PgpUtilities.GetDecoderStream(stream);
        var bundle = new Org.BouncyCastle.Bcpg.OpenPgp.PgpPublicKeyRingBundle(decoded);
        return bundle.GetKeyRings().Cast<Org.BouncyCastle.Bcpg.OpenPgp.PgpPublicKeyRing>().First();
    }

    private static string Armor(Org.BouncyCastle.Bcpg.OpenPgp.PgpPublicKeyRing ring)
    {
        using var output = new MemoryStream();
        using (var armored = new Org.BouncyCastle.Bcpg.ArmoredOutputStream(output))
        {
            ring.Encode(armored);
        }

        return Encoding.ASCII.GetString(output.ToArray());
    }
}
