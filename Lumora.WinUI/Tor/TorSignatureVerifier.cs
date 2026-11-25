using System.Text;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;

namespace Lumora.WinUI.Tor;

internal enum TorSignatureOutcome
{
    // Signature valide, produite par une sous-cle liee a la cle principale epinglee.
    Valid,
    // Signature bien formee mais produite par une cle absente du bloc fourni
    // (typiquement : rotation de la sous-cle de signature). L'appelant peut
    // retenter avec un bloc de cle a jour.
    SignerKeyUnknown,
    // Tout le reste : donnees alterees, signature mal formee, cle principale
    // inattendue, sous-cle revoquee/expiree/non liee, algorithme de hachage faible.
    Invalid,
}

internal readonly record struct TorSignatureResult(TorSignatureOutcome Outcome, string Message);

// Verification d'une signature OpenPGP detachee (fichier .asc) contre une cle
// principale dont l'EMPREINTE est epinglee. Le bloc de cle fourni n'est jamais
// cru sur parole : la cle principale doit avoir l'empreinte epinglee, et la
// cle qui signe doit etre soit cette cle principale, soit une sous-cle dont la
// signature de liaison (type 0x18) est verifiee par la cle principale, non
// revoquee, et valide a la date de la signature.
//
// Logique pure (aucun acces reseau ni disque) : testable avec de vraies
// signatures de Tor (voir TorSignatureVerifierTests).
internal static class TorSignatureVerifier
{
    private const int KeyFlagSign = 0x02;

    public static TorSignatureResult VerifyDetached(
        Stream data, string armoredSignature, string armoredKeyBlock, string pinnedPrimaryFingerprintHex)
    {
        try
        {
            var signatures = ReadSignatures(armoredSignature);
            if (signatures.Count == 0)
            {
                return Invalid("Fichier de signature vide ou illisible.");
            }

            var ring = FindPinnedKeyRing(armoredKeyBlock, pinnedPrimaryFingerprintHex);
            if (ring is null)
            {
                return Invalid("Le bloc de clé fourni ne contient pas la clé de signature officielle de Tor.");
            }

            var master = ring.GetPublicKey();

            PgpSignature? chosen = null;
            PgpPublicKey? signer = null;
            foreach (var candidate in signatures)
            {
                var key = ring.GetPublicKey(candidate.KeyId);
                if (key is null)
                {
                    continue;
                }

                chosen = candidate;
                signer = key;
                break;
            }

            if (chosen is null || signer is null)
            {
                return new TorSignatureResult(TorSignatureOutcome.SignerKeyUnknown,
                    "La signature a été produite par une clé inconnue du bloc de clé fourni.");
            }

            if (chosen.SignatureType != PgpSignature.BinaryDocument)
            {
                return Invalid("Type de signature inattendu (signature de document binaire attendue).");
            }

            if (chosen.HashAlgorithm is not (HashAlgorithmTag.Sha256 or HashAlgorithmTag.Sha384 or HashAlgorithmTag.Sha512))
            {
                return Invalid("Algorithme de hachage de la signature trop faible.");
            }

            if (signer.KeyId != master.KeyId)
            {
                var bindingProblem = CheckSubkeyBinding(master, signer, chosen.CreationTime);
                if (bindingProblem is not null)
                {
                    return Invalid(bindingProblem);
                }
            }

            chosen.InitVerify(signer);
            var buffer = new byte[81920];
            int read;
            while ((read = data.Read(buffer, 0, buffer.Length)) > 0)
            {
                chosen.Update(buffer, 0, read);
            }

            return chosen.Verify()
                ? new TorSignatureResult(TorSignatureOutcome.Valid, "Signature valide.")
                : Invalid("La signature ne correspond pas au fichier (fichier altéré ou signature falsifiée).");
        }
        catch (Exception ex) when (ex is PgpException or IOException or InvalidOperationException
            or ArgumentException or FormatException or NotSupportedException)
        {
            return Invalid($"Signature ou clé illisible ({ex.GetType().Name}).");
        }
    }

    private static TorSignatureResult Invalid(string message) =>
        new(TorSignatureOutcome.Invalid, message);

    private static List<PgpSignature> ReadSignatures(string armoredSignature)
    {
        var result = new List<PgpSignature>();
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(armoredSignature));
        using var decoded = PgpUtilities.GetDecoderStream(stream);
        var factory = new PgpObjectFactory(decoded);
        object? obj;
        while ((obj = factory.NextPgpObject()) is not null)
        {
            if (obj is PgpSignatureList list)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    result.Add(list[i]);
                }
            }
        }

        return result;
    }

    private static PgpPublicKeyRing? FindPinnedKeyRing(string armoredKeyBlock, string pinnedPrimaryFingerprintHex)
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(armoredKeyBlock));
        using var decoded = PgpUtilities.GetDecoderStream(stream);
        var bundle = new PgpPublicKeyRingBundle(decoded);
        foreach (PgpPublicKeyRing ring in bundle.GetKeyRings())
        {
            var fingerprint = Convert.ToHexString(ring.GetPublicKey().GetFingerprint());
            if (string.Equals(fingerprint, pinnedPrimaryFingerprintHex, StringComparison.OrdinalIgnoreCase))
            {
                return ring;
            }
        }

        return null;
    }

    // Retourne null si la sous-cle est valablement liee a la cle principale a
    // la date de signature, sinon un message d'echec. La signature de liaison
    // la plus recente (verifiee cryptographiquement par la cle principale)
    // fait foi pour la duree de validite.
    private static string? CheckSubkeyBinding(PgpPublicKey master, PgpPublicKey subkey, DateTime signatureTimeUtc)
    {
        if (subkey.HasRevocation())
        {
            return "La sous-clé de signature a été révoquée.";
        }

        PgpSignature? best = null;
        foreach (PgpSignature binding in subkey.GetSignaturesOfType(PgpSignature.SubkeyBinding))
        {
            if (binding.KeyId != master.KeyId)
            {
                continue;
            }

            binding.InitVerify(master);
            if (!binding.VerifyCertification(master, subkey))
            {
                continue;
            }

            var flags = binding.GetHashedSubPackets()?.GetKeyFlags() ?? 0;
            if (flags != 0 && (flags & KeyFlagSign) == 0)
            {
                continue;
            }

            if (best is null || binding.CreationTime > best.CreationTime)
            {
                best = binding;
            }
        }

        if (best is null)
        {
            return "La sous-clé de signature n'est pas liée à la clé principale de Tor.";
        }

        if (signatureTimeUtc < subkey.CreationTime)
        {
            return "La signature est antérieure à la création de la sous-clé de signature.";
        }

        var validSeconds = best.GetHashedSubPackets()?.GetKeyExpirationTime() ?? 0;
        if (validSeconds > 0 && signatureTimeUtc > subkey.CreationTime.AddSeconds(validSeconds))
        {
            return "La sous-clé de signature était expirée à la date de la signature.";
        }

        return null;
    }
}
