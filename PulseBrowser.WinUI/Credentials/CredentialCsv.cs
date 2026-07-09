namespace PulseBrowser.WinUI.Credentials;

// Import/export CSV du gestionnaire de mots de passe (format Chrome/Firefox).
// Aucune dépendance UI — testable par dotnet test.
internal static class CredentialCsv
{
    // Repère les colonnes url/username/password par en-tête, tolère les variantes
    // Chrome ("login_uri", "login_username", "login_password") et Firefox
    // ("url", "username", "password").
    public static List<(string Origin, string Username, string Password)> Parse(string text)
    {
        var result = new List<(string, string, string)>();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines.Length < 2) return result;

        var header = SplitLine(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToList();
        int urlIdx = header.FindIndex(h => h is "url" or "login_uri" or "website" or "site");
        int userIdx = header.FindIndex(h => h is "username" or "login" or "login_username" or "email");
        int passIdx = header.FindIndex(h => h is "password" or "login_password");
        if (urlIdx < 0 || passIdx < 0) return result;

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cols = SplitLine(lines[i]);
            if (cols.Count <= Math.Max(urlIdx, passIdx)) continue;
            var url = cols[urlIdx].Trim();
            var user = userIdx >= 0 && userIdx < cols.Count ? cols[userIdx].Trim() : string.Empty;
            var pass = cols[passIdx];
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(pass)) continue;
            result.Add((NormalizeOrigin(url), user, pass));
        }
        return result;
    }

    public static string NormalizeOrigin(string url)
    {
        try { var u = new Uri(url); return $"{u.Scheme}://{u.Host}"; }
        catch { return url; }
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
}
