namespace PulseBrowser.WinUI;

internal sealed record BuildAuthenticityInfo(
    string Version,
    string Channel,
    string Sha256,
    string Sigstore,
    string WindowsSignature,
    string ProfileMode,
    string VerificationFile);

internal static class BuildAuthenticity
{
    public const string VerificationFileName = "VERIFICATION.txt";
    public const string MissingValue = "Non genere pour ce build local";
    public const string UnsignedSigstoreValue = "Non signee pour ce build";
    public const string UnsignedWindowsValue = "Non signee Authenticode";

    public static BuildAuthenticityInfo Load(string baseDirectory, string version)
    {
        var verificationPath = Path.Combine(baseDirectory, VerificationFileName);
        if (!File.Exists(verificationPath))
        {
            return new BuildAuthenticityInfo(
                version,
                "Build de developpement local",
                MissingValue,
                UnsignedSigstoreValue,
                UnsignedWindowsValue,
                ProfileMode(),
                verificationPath);
        }

        var values = ReadValues(verificationPath);
        return new BuildAuthenticityInfo(
            ValueOrDefault(values, "Version", version),
            ValueOrDefault(values, "Canal", "Build de developpement"),
            ValueOrDefault(values, "SHA256", MissingValue),
            ValueOrDefault(values, "Signature Sigstore", UnsignedSigstoreValue),
            ValueOrDefault(values, "Signature Windows", UnsignedWindowsValue),
            ValueOrDefault(values, "Profil", ProfileMode()),
            verificationPath);
    }

    private static Dictionary<string, string> ReadValues(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            var separator = line.IndexOf(':');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (key.Length > 0 && value.Length > 0)
                result[key] = value;
        }

        return result;
    }

    private static string ValueOrDefault(Dictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    private static string ProfileMode() =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PulseProfilePaths.ProfileDirectoryEnvironmentVariable))
            ? "Profil local utilisateur"
            : "Profil isole de test";
}
