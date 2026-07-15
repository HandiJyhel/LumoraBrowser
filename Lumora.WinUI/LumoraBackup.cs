using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Lumora.WinUI;

// Format .lumorabackup :
//   [0-6]   Magic "NOVABAK"
//   [7]     Version
//   [8-23]  Salt (16 octets)
//   Version 0x02 (actuelle) : clé Argon2id, payload AES-256-GCM authentifié.
//     [24-35] Nonce (12) ‖ [36-51] Tag (16) ‖ [52+] ciphertext
//   Version 0x01 (héritée, lue seulement) : clé PBKDF2-SHA256 100k, AES-256-CBC.
//     [24-39] IV (16) ‖ [40+] ciphertext
//
// Le zip contient les fichiers de navigation décryptés (texte brut).
// Le coffre n'est pas inclus (chiffré séparément).

internal static class LumoraBackup
{
    private static readonly byte[] Magic = "NOVABAK"u8.ToArray();
    private const byte FormatVersion = 0x02;
    private const byte LegacyFormatVersion = 0x01;
    private const int SaltSize = 16;
    private const int IvSize = 16;      // AES-CBC hérité (import v1)
    private const int GcmNonce = 12;
    private const int GcmTag = 16;
    private const int KeySize = 32;     // AES-256
    private const int LegacyPbkdf2Iterations = 100_000;

    // Argon2id — aligné sur le coffre principal (voir VaultStore).
    private const int Argon2MemoryKib = 65536; // 64 Mio
    private const int Argon2Iterations = 3;
    private const int Argon2Parallelism = 4;

    public static void Export(string destPath, string password, LumoraProfilePaths profile)
    {
        using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifest = $"{{\"format\":1,\"created_at\":\"{DateTime.UtcNow:O}\"}}";
            WriteEntry(zip, "manifest.json", manifest);

            WriteProfileEntry(zip, profile.BookmarksFile, "navigation/bookmarks.txt");
            WriteProfileEntry(zip, profile.HistoryFile, "navigation/history.txt");
            WriteProfileEntry(zip, profile.NotesFile, "navigation/notes.txt");
            WriteProfileEntry(zip, profile.AnnotationsFile, "navigation/annotations.txt");
            WriteProfileEntry(zip, profile.TabsFile, "navigation/tabs.txt");
            WriteProfileEntry(zip, profile.UiSettingsFile, "navigation/ui-settings.txt");

            var profileContent = LumoraFile.TryReadAllText(profile.ProfileFile);
            if (profileContent is not null)
                WriteEntry(zip, "profile.txt", profileContent);
        }

        var salt  = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(GcmNonce);
        var key   = DeriveKey(password, salt);

        var plaintext = zipStream.ToArray();
        var cipher    = new byte[plaintext.Length];
        var tag       = new byte[GcmTag];
        using (var aes = new AesGcm(key, GcmTag))
            aes.Encrypt(nonce, plaintext, cipher, tag);

        using var file = new BinaryWriter(File.Open(destPath, FileMode.Create, FileAccess.Write));
        file.Write(Magic);
        file.Write(FormatVersion);
        file.Write(salt);
        file.Write(nonce);
        file.Write(tag);
        file.Write(cipher);
    }

    public static void Import(string srcPath, string password, LumoraProfilePaths profile)
    {
        using var file = new BinaryReader(File.OpenRead(srcPath));

        var magic = file.ReadBytes(Magic.Length);
        if (!magic.SequenceEqual(Magic))
            throw new InvalidDataException("Ce fichier n'est pas une sauvegarde Lumora valide.");

        var version = file.ReadByte();
        if (version != FormatVersion && version != LegacyFormatVersion)
            throw new InvalidDataException($"Version de sauvegarde non supportee ({version}).");

        var salt = file.ReadBytes(SaltSize);

        byte[] zipBytes;
        try
        {
            zipBytes = version == FormatVersion
                ? DecryptV2(file, password, salt)
                : DecryptV1Legacy(file, password, salt);
        }
        catch
        {
            throw new CryptographicException("Mot de passe incorrect ou sauvegarde corrompue.");
        }

        try
        {
            using var zipStream = new MemoryStream(zipBytes);
            using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);

            Directory.CreateDirectory(profile.NavigationDir);

            ReadProfileEntry(zip, "navigation/bookmarks.txt", profile.BookmarksFile);
            ReadProfileEntry(zip, "navigation/history.txt", profile.HistoryFile);
            ReadProfileEntry(zip, "navigation/notes.txt", profile.NotesFile);
            ReadProfileEntry(zip, "navigation/annotations.txt", profile.AnnotationsFile);
            ReadProfileEntry(zip, "navigation/tabs.txt", profile.TabsFile);
            ReadProfileEntry(zip, "navigation/ui-settings.txt", profile.UiSettingsFile);

            var profileEntry = zip.GetEntry("profile.txt");
            if (profileEntry is not null)
            {
                using var reader = new StreamReader(profileEntry.Open(), Encoding.UTF8);
                LumoraFile.WriteAllText(profile.ProfileFile, reader.ReadToEnd());
            }
        }
        catch (InvalidDataException)
        {
            throw new CryptographicException("Mot de passe incorrect ou sauvegarde corrompue.");
        }
    }

    private static byte[] DecryptV2(BinaryReader file, string password, byte[] salt)
    {
        var nonce     = file.ReadBytes(GcmNonce);
        var tag       = file.ReadBytes(GcmTag);
        var remaining = (int)(file.BaseStream.Length - file.BaseStream.Position);
        var cipher    = file.ReadBytes(remaining);

        var key   = DeriveKey(password, salt);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, GcmTag);
        aes.Decrypt(nonce, cipher, tag, plain); // lève si mot de passe incorrect / altération
        return plain;
    }

    private static byte[] DecryptV1Legacy(BinaryReader file, string password, byte[] salt)
    {
        var iv        = file.ReadBytes(IvSize);
        var remaining = (int)(file.BaseStream.Length - file.BaseStream.Position);
        var encrypted = file.ReadBytes(remaining);

        var key = DeriveKeyLegacy(password, salt);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV  = iv;
        using var decryptor = aes.CreateDecryptor();
        using var buf = new MemoryStream();
        using (var cs = new CryptoStream(new MemoryStream(encrypted), decryptor, CryptoStreamMode.Read))
            cs.CopyTo(buf);
        return buf.ToArray();
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt                = salt,
            MemorySize          = Argon2MemoryKib,
            Iterations          = Argon2Iterations,
            DegreeOfParallelism = Argon2Parallelism
        };
        return argon2.GetBytes(KeySize);
    }

    private static byte[] DeriveKeyLegacy(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            Encoding.UTF8.GetBytes(password), salt, LegacyPbkdf2Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize);
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static void WriteProfileEntry(ZipArchive zip, string path, string entry)
    {
        var content = LumoraFile.TryReadAllText(path);
        if (content is not null)
            WriteEntry(zip, entry, content);
    }

    private static void ReadProfileEntry(ZipArchive zip, string entryName, string destinationPath)
    {
        var zipEntry = zip.GetEntry(entryName);
        if (zipEntry is null) return;
        using var reader = new StreamReader(zipEntry.Open(), Encoding.UTF8);
        LumoraFile.WriteAllText(destinationPath, reader.ReadToEnd());
    }
}
