using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace PulseBrowser.WinUI;

// Format .pulsebackup :
//   [0-7]   Magic "PULSEBAK"
//   [8]     Version 0x01
//   [9-24]  Salt PBKDF2 (16 octets)
//   [25-40] IV AES (16 octets)
//   [41+]   Payload AES-256-CBC(PBKDF2-SHA256(password), zip)
//
// Le zip contient les fichiers de navigation décryptés (texte brut).
// Le coffre Rust n'est pas inclus (DPAPI lié au compte Windows).

internal static class PulseBackup
{
    private static readonly byte[] Magic = "PULSEBAK"u8.ToArray();
    private const byte FormatVersion = 0x01;
    private const int SaltSize = 16;
    private const int IvSize = 16;
    private const int KeySize = 32; // AES-256
    private const int Pbkdf2Iterations = 100_000;

    private static readonly (string RelativePath, string ZipEntry)[] NavFiles =
    [
        ("bookmarks.pulse",   "navigation/bookmarks.txt"),
        ("history.pulse",     "navigation/history.txt"),
        ("tabs.pulse",        "navigation/tabs.txt"),
        ("ui-settings.pulse", "navigation/ui-settings.txt"),
    ];

    public static void Export(string destPath, string password, PulseProfilePaths profile)
    {
        using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifest = $"{{\"format\":1,\"created_at\":\"{DateTime.UtcNow:O}\"}}";
            WriteEntry(zip, "manifest.json", manifest);

            foreach (var (rel, entry) in NavFiles)
            {
                var content = PulseFile.TryReadAllText(Path.Combine(profile.NavigationDir, rel));
                if (content is not null)
                    WriteEntry(zip, entry, content);
            }

            var profileContent = PulseFile.TryReadAllText(profile.ProfileFile);
            if (profileContent is not null)
                WriteEntry(zip, "profile.txt", profileContent);
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var iv   = RandomNumberGenerator.GetBytes(IvSize);
        var key  = DeriveKey(password, salt);

        byte[] encrypted;
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV  = iv;
            using var encryptor = aes.CreateEncryptor();
            using var buf = new MemoryStream();
            using (var cs = new CryptoStream(buf, encryptor, CryptoStreamMode.Write))
                cs.Write(zipStream.ToArray());
            encrypted = buf.ToArray();
        }

        using var file = new BinaryWriter(File.Open(destPath, FileMode.Create, FileAccess.Write));
        file.Write(Magic);
        file.Write(FormatVersion);
        file.Write(salt);
        file.Write(iv);
        file.Write(encrypted);
    }

    public static void Import(string srcPath, string password, PulseProfilePaths profile)
    {
        using var file = new BinaryReader(File.OpenRead(srcPath));

        var magic = file.ReadBytes(8);
        if (!magic.SequenceEqual(Magic))
            throw new InvalidDataException("Ce fichier n'est pas une sauvegarde Pulse valide.");

        var version = file.ReadByte();
        if (version != FormatVersion)
            throw new InvalidDataException($"Version de sauvegarde non supportee ({version}).");

        var salt      = file.ReadBytes(SaltSize);
        var iv        = file.ReadBytes(IvSize);
        var remaining = (int)(file.BaseStream.Length - file.BaseStream.Position);
        var encrypted = file.ReadBytes(remaining);

        byte[] zipBytes;
        try
        {
            var key = DeriveKey(password, salt);
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV  = iv;
            using var decryptor = aes.CreateDecryptor();
            using var buf = new MemoryStream();
            using (var cs = new CryptoStream(new MemoryStream(encrypted), decryptor, CryptoStreamMode.Read))
                cs.CopyTo(buf);
            zipBytes = buf.ToArray();
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

            foreach (var (rel, entry) in NavFiles)
            {
                var zipEntry = zip.GetEntry(entry);
                if (zipEntry is null) continue;
                using var reader = new StreamReader(zipEntry.Open(), Encoding.UTF8);
                PulseFile.WriteAllText(Path.Combine(profile.NavigationDir, rel), reader.ReadToEnd());
            }

            var profileEntry = zip.GetEntry("profile.txt");
            if (profileEntry is not null)
            {
                using var reader = new StreamReader(profileEntry.Open(), Encoding.UTF8);
                PulseFile.WriteAllText(profile.ProfileFile, reader.ReadToEnd());
            }
        }
        catch (InvalidDataException)
        {
            throw new CryptographicException("Mot de passe incorrect ou sauvegarde corrompue.");
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize);
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
