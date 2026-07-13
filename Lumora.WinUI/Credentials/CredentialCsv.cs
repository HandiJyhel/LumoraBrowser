namespace Lumora.WinUI.Credentials;

internal sealed record CredentialImportItem(
    string Origin,
    string Username,
    string Password,
    string LoginUrl = "",
    string Label = "");

// Import/export CSV du gestionnaire de mots de passe (format Chrome/Firefox).
// Aucune dépendance UI — testable par dotnet test.
internal static class CredentialCsv
{
    // Repère les colonnes url/username/password par en-tête, tolère les variantes
    // Chrome ("login_uri", "login_username", "login_password") et Firefox
    // ("url", "username", "password").
    public static List<CredentialImportItem> Parse(string text)
    {
        var result = new List<CredentialImportItem>();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines.Length < 2) return result;

        var header = SplitLine(lines[0]).Select(NormalizeHeader).ToList();
        int urlIdx = header.FindIndex(h => h is "url" or "urls" or "loginuri" or "loginurl" or "website" or "site" or "uri");
        int userIdx = header.FindIndex(h => h is "username" or "user" or "login" or "loginusername" or "email" or "mail");
        int passIdx = header.FindIndex(h => h is "password" or "loginpassword" or "pass");
        int nameIdx = header.FindIndex(h => h is "name" or "title" or "itemname" or "label");
        int typeIdx = header.FindIndex(h => h is "type" or "kind");
        if (urlIdx < 0 || passIdx < 0) return result;

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cols = SplitLine(lines[i]);
            if (cols.Count <= Math.Max(urlIdx, passIdx)) continue;

            if (typeIdx >= 0 && typeIdx < cols.Count && !IsLoginType(cols[typeIdx]))
                continue;

            var url = FirstUrl(cols[urlIdx].Trim());
            var user = userIdx >= 0 && userIdx < cols.Count ? cols[userIdx].Trim() : string.Empty;
            var pass = cols[passIdx];
            var label = nameIdx >= 0 && nameIdx < cols.Count ? cols[nameIdx].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(pass)) continue;
            result.Add(new CredentialImportItem(
                NormalizeOrigin(url),
                user,
                pass,
                NormalizeLoginUrl(url),
                label));
        }
        return result;
    }

    public static string NormalizeOrigin(string url)
    {
        try { var u = new Uri(url); return $"{u.Scheme}://{u.Host}"; }
        catch { return url; }
    }

    public static string NormalizeLoginUrl(string url)
    {
        try
        {
            var u = new Uri(url);
            return u.Scheme is "http" or "https" ? u.ToString() : string.Empty;
        }
        catch { return string.Empty; }
    }

    public static List<string> SplitLine(string line)
    {
        var fields = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(ch);
            }
            else
            {
                if (ch == '"') inQuotes = true;
                else if (ch == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(ch);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }

    public static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static string NormalizeHeader(string value)
    {
        var cleaned = value.Trim().TrimStart('\uFEFF').ToLowerInvariant();
        return new string(cleaned.Where(char.IsLetterOrDigit).ToArray());
    }

    private static bool IsLoginType(string value)
    {
        var type = NormalizeHeader(value);
        return string.IsNullOrWhiteSpace(type) ||
               type is "login" or "password" or "credential";
    }

    private static string FirstUrl(string value)
    {
        foreach (var candidate in value
                     .Split(new[] { '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                     .Select(part => part.Trim().Trim(';')))
        {
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
                uri.Scheme is "http" or "https")
            {
                return uri.ToString();
            }
        }

        return value;
    }
}
