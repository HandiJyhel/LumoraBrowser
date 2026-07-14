using System.Globalization;
using System.Text;

namespace Lumora.WinUI;

// Suggestions de la barre d'adresse : classe PURE (aucune dépendance UI),
// compilée aussi dans Lumora.Tests. L'appelant fournit les candidats (onglets
// ouverts, favoris, historique agrégé) ; le moteur filtre, note, dédoublonne
// et classe. Tout est calculé localement, rien ne sort de la machine : la
// frappe de l'utilisateur n'est jamais envoyée à un service de suggestions.
public enum AddressSuggestionKind
{
    OpenTab,
    Bookmark,
    History
}

public sealed record AddressSuggestionCandidate(
    AddressSuggestionKind Kind,
    string Title,
    string Url,
    int TabId = -1,
    int VisitCount = 0,
    DateTimeOffset LastVisit = default);

public sealed record AddressSuggestion(
    AddressSuggestionKind Kind,
    string Title,
    string Url,
    int TabId,
    int Score);

public static class AddressSuggestionEngine
{
    public const int DefaultMaxResults = 8;

    public static IReadOnlyList<AddressSuggestion> Suggest(
        string? query,
        IEnumerable<AddressSuggestionCandidate> candidates,
        DateTimeOffset now,
        int maxResults = DefaultMaxResults)
    {
        var tokens = Fold(query ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0 || maxResults <= 0)
            return Array.Empty<AddressSuggestion>();

        // Dédoublonnage par URL canonique : un onglet ouvert gagne sur un favori,
        // qui gagne sur l'historique ; à priorité égale, le meilleur score reste.
        var bestByUrl = new Dictionary<string, (AddressSuggestion Suggestion, DateTimeOffset LastVisit)>();

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Url)) continue;
            if (candidate.Url.StartsWith("lumora://", StringComparison.OrdinalIgnoreCase)) continue;

            var score = MatchScore(tokens, candidate, now);
            if (score <= 0) continue;

            var title = string.IsNullOrWhiteSpace(candidate.Title) ? candidate.Url : candidate.Title.Trim();
            var suggestion = new AddressSuggestion(candidate.Kind, title, candidate.Url, candidate.TabId, score);
            var key = CanonicalUrlKey(candidate.Url);

            if (!bestByUrl.TryGetValue(key, out var existing) ||
                IsBetter(suggestion, candidate.LastVisit, existing.Suggestion, existing.LastVisit))
            {
                bestByUrl[key] = (suggestion, candidate.LastVisit);
            }
        }

        return bestByUrl.Values
            .OrderByDescending(entry => entry.Suggestion.Score)
            .ThenBy(entry => KindPriority(entry.Suggestion.Kind))
            .ThenByDescending(entry => entry.LastVisit)
            .ThenBy(entry => entry.Suggestion.Title, StringComparer.CurrentCultureIgnoreCase)
            .Take(maxResults)
            .Select(entry => entry.Suggestion)
            .ToList();
    }

    // Regroupe les visites brutes de l'historique (une entrée par visite) en un
    // candidat par URL, avec compteur de visites et date de dernière visite.
    public static IReadOnlyList<AddressSuggestionCandidate> AggregateHistory(
        IEnumerable<(string Url, string Title, DateTimeOffset VisitedAt)> visits)
    {
        var byUrl = new Dictionary<string, (string Title, int Count, DateTimeOffset Last)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (url, title, visitedAt) in visits)
        {
            if (string.IsNullOrWhiteSpace(url)) continue;

            if (byUrl.TryGetValue(url, out var current))
            {
                // Le titre suit la visite la plus récente (les sites renomment leurs pages).
                var isNewer = visitedAt >= current.Last;
                byUrl[url] = (
                    isNewer && !string.IsNullOrWhiteSpace(title) ? title : current.Title,
                    current.Count + 1,
                    isNewer ? visitedAt : current.Last);
            }
            else
            {
                byUrl[url] = (string.IsNullOrWhiteSpace(title) ? url : title, 1, visitedAt);
            }
        }

        return byUrl
            .Select(pair => new AddressSuggestionCandidate(
                AddressSuggestionKind.History,
                pair.Value.Title,
                pair.Key,
                VisitCount: pair.Value.Count,
                LastVisit: pair.Value.Last))
            .ToList();
    }

    private static int MatchScore(string[] tokens, AddressSuggestionCandidate candidate, DateTimeOffset now)
    {
        var host = Fold(HostOf(candidate.Url));
        var title = Fold(candidate.Title);
        var url = Fold(candidate.Url);

        // Chaque mot tapé doit correspondre quelque part, sinon le candidat sort.
        var total = 0;
        foreach (var token in tokens)
        {
            var tokenScore = TokenScore(token, host, title, url);
            if (tokenScore == 0) return 0;
            total += tokenScore;
        }

        var score = total / tokens.Length;

        // Un onglet déjà ouvert évite un doublon, un favori est un choix délibéré.
        score += candidate.Kind switch
        {
            AddressSuggestionKind.OpenTab => 30,
            AddressSuggestionKind.Bookmark => 15,
            _ => 0
        };

        if (candidate.Kind == AddressSuggestionKind.History)
        {
            score += Math.Min(candidate.VisitCount, 10) * 2;
            var age = now - candidate.LastVisit;
            if (age <= TimeSpan.FromHours(2)) score += 12;
            else if (age <= TimeSpan.FromDays(7)) score += 8;
            else if (age <= TimeSpan.FromDays(30)) score += 4;
        }

        return score;
    }

    private static int TokenScore(string token, string host, string title, string url)
    {
        var best = 0;
        if (host.Length > 0)
        {
            if (host.StartsWith(token, StringComparison.Ordinal)) best = 100;
            else if (host.Contains("." + token, StringComparison.Ordinal)) best = 75;
            else if (host.Contains(token, StringComparison.Ordinal)) best = 65;
        }

        if (best < 80 && WordStartsWith(title, token)) best = 80;
        if (best < 60 && title.Contains(token, StringComparison.Ordinal)) best = 60;
        if (best < 40 && url.Contains(token, StringComparison.Ordinal)) best = 40;
        return best;
    }

    private static bool WordStartsWith(string text, string token)
    {
        var index = text.IndexOf(token, StringComparison.Ordinal);
        while (index >= 0)
        {
            if (index == 0 || !char.IsLetterOrDigit(text[index - 1])) return true;
            index = text.IndexOf(token, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool IsBetter(
        AddressSuggestion candidate, DateTimeOffset candidateLastVisit,
        AddressSuggestion current, DateTimeOffset currentLastVisit)
    {
        var priorityDelta = KindPriority(candidate.Kind).CompareTo(KindPriority(current.Kind));
        if (priorityDelta != 0) return priorityDelta < 0;
        if (candidate.Score != current.Score) return candidate.Score > current.Score;
        return candidateLastVisit > currentLastVisit;
    }

    private static int KindPriority(AddressSuggestionKind kind) => kind switch
    {
        AddressSuggestionKind.OpenTab => 0,
        AddressSuggestionKind.Bookmark => 1,
        _ => 2
    };

    private static string HostOf(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host.Length == 0)
            return string.Empty;

        return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..]
            : uri.Host;
    }

    private static string CanonicalUrlKey(string url)
    {
        var key = url.Trim().ToLowerInvariant().TrimEnd('/');
        if (key.StartsWith("https://", StringComparison.Ordinal)) key = key[8..];
        else if (key.StartsWith("http://", StringComparison.Ordinal)) key = key[7..];
        if (key.StartsWith("www.", StringComparison.Ordinal)) key = key[4..];
        return key;
    }

    // Minuscules + suppression des accents : "Météo" correspond à "meteo".
    private static string Fold(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
