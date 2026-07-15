using System.Text;

namespace Lumora.WinUI;

// ── Annotations de pages web ─────────────────────────────────────────────────
// Classe PURE (aucune dépendance UI), compilée aussi dans Lumora.Tests.
// Un surlignage fait en mode lecture : extrait exact + contexte avant/après
// (l'ancrage du standard W3C Web Annotation), commentaire facultatif, le tout
// rattaché à l'URL de la page. Stocké chiffré DPAPI dans le profil
// (annotations.lumora) — rien ne quitte l'appareil. Même patron que NoteStore :
// lecture/écriture complètes du fichier, mode invité en mémoire de session.

public sealed record PageAnnotation(
    string Id,
    string Url,
    string PageTitle,
    string Quote,
    string Prefix,
    string Suffix,
    string Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// Vue groupée pour le panneau Notes : une page annotée, son nombre de
// surlignages et la date de la dernière annotation.
public sealed record AnnotatedPage(
    string Url,
    string PageTitle,
    int Count,
    DateTimeOffset UpdatedAt);

public sealed class AnnotationStore
{
    private readonly string _annotationsFile;
    private bool _isGuest;
    private List<PageAnnotation>? _guestAnnotations;

    public AnnotationStore(string annotationsFile) => _annotationsFile = annotationsFile;

    public void SetGuestMode(bool isGuest)
    {
        _isGuest = isGuest;
        if (!isGuest) _guestAnnotations = null;
    }

    // Toutes les annotations, de la plus récemment modifiée à la plus ancienne.
    public List<PageAnnotation> AllAnnotations()
    {
        if (_isGuest)
        {
            return SortNewestFirst(new List<PageAnnotation>(_guestAnnotations ??= new List<PageAnnotation>()));
        }

        var content = LumoraFile.TryReadAllText(_annotationsFile) ?? string.Empty;
        var annotations = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseLine)
            .Where(annotation => annotation is not null)
            .Cast<PageAnnotation>()
            .ToList();
        return SortNewestFirst(annotations);
    }

    // Les annotations d'une page, dans l'ordre où elles ont été créées : c'est
    // l'ordre naturel de lecture pour les réappliquer et les lister.
    public List<PageAnnotation> ForPage(string url)
    {
        var key = NormalizeUrl(url);
        if (key.Length == 0) return new List<PageAnnotation>();

        return AllAnnotations()
            .Where(annotation => annotation.Url == key)
            .OrderBy(annotation => annotation.CreatedAt)
            .ThenBy(annotation => annotation.Id, StringComparer.Ordinal)
            .ToList();
    }

    public int CountForPage(string url)
    {
        var key = NormalizeUrl(url);
        return key.Length == 0 ? 0 : AllAnnotations().Count(annotation => annotation.Url == key);
    }

    // Pages annotées, la plus récemment travaillée en premier. Le titre retenu
    // est celui de l'annotation la plus récente (le plus à jour).
    public List<AnnotatedPage> AnnotatedPages()
    {
        return AllAnnotations()
            .GroupBy(annotation => annotation.Url, StringComparer.Ordinal)
            .Select(group =>
            {
                var newest = group.OrderByDescending(annotation => annotation.UpdatedAt).First();
                return new AnnotatedPage(group.Key, newest.PageTitle, group.Count(), newest.UpdatedAt);
            })
            .OrderByDescending(page => page.UpdatedAt)
            .ThenBy(page => page.Url, StringComparer.Ordinal)
            .ToList();
    }

    // Rend null si la page n'est pas une URL web ou si l'extrait est vide :
    // une annotation sans texte surligné n'ancre rien.
    public PageAnnotation? Add(string url, string pageTitle, string quote, string prefix, string suffix, string comment)
    {
        var key = NormalizeUrl(url);
        if (key.Length == 0 || string.IsNullOrWhiteSpace(quote)) return null;

        var annotations = AllAnnotations();
        var now = NowToMillisecond();
        var created = new PageAnnotation(
            NextAnnotationId(annotations), key, (pageTitle ?? string.Empty).Trim(),
            quote, prefix ?? string.Empty, suffix ?? string.Empty,
            (comment ?? string.Empty).Trim(), now, now);
        annotations.Add(created);
        WriteAnnotations(annotations);
        return created;
    }

    // Seul le commentaire est modifiable : l'extrait et son contexte sont
    // l'ancre dans la page, les changer casserait le repositionnement.
    public PageAnnotation? UpdateComment(string id, string comment)
    {
        var annotations = AllAnnotations();
        var index = annotations.FindIndex(annotation => annotation.Id == id);
        if (index < 0) return null;

        var existing = annotations[index];
        var trimmed = (comment ?? string.Empty).Trim();
        if (existing.Comment == trimmed) return existing;

        var updated = existing with { Comment = trimmed, UpdatedAt = NowToMillisecond() };
        annotations[index] = updated;
        WriteAnnotations(annotations);
        return updated;
    }

    public bool Remove(string id)
    {
        var annotations = AllAnnotations();
        var removed = annotations.RemoveAll(annotation => annotation.Id == id);
        if (removed > 0) WriteAnnotations(annotations);
        return removed > 0;
    }

    // Oublie toute la page d'un coup (suppression depuis le panneau Notes).
    public int RemoveForPage(string url)
    {
        var key = NormalizeUrl(url);
        if (key.Length == 0) return 0;

        var annotations = AllAnnotations();
        var removed = annotations.RemoveAll(annotation => annotation.Url == key);
        if (removed > 0) WriteAnnotations(annotations);
        return removed;
    }

    // Clé de regroupement d'une page : URL web absolue sans fragment (le #…
    // désigne un endroit de la même page, pas une autre page).
    public static string NormalizeUrl(string url)
    {
        var trimmed = (url ?? string.Empty).Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return string.Empty;
        }

        return uri.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.Fragment, UriFormat.UriEscaped);
    }

    // Filtre de recherche : extrait, commentaire, titre de page et URL.
    public static bool Matches(PageAnnotation annotation, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var trimmed = query.Trim();
        return annotation.Quote.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
               annotation.Comment.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
               annotation.PageTitle.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
               annotation.Url.Contains(trimmed, StringComparison.OrdinalIgnoreCase);
    }

    // Extrait court pour les listes : première ligne non vide, tronquée.
    public static string DisplayQuote(PageAnnotation annotation)
    {
        var firstLine = annotation.Quote
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0) ?? string.Empty;
        if (firstLine.Length == 0) return "Extrait surligne";

        return firstLine.Length <= 90 ? firstLine : firstLine[..87] + "...";
    }

    // ── Interne ──────────────────────────────────────────────────────────────

    // Le format sérialise en millisecondes Unix : l'horodatage est tronqué dès
    // la création pour qu'un aller-retour disque rende des dates identiques.
    private static DateTimeOffset NowToMillisecond() =>
        DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.Now.ToUnixTimeMilliseconds()).ToLocalTime();

    private static List<PageAnnotation> SortNewestFirst(List<PageAnnotation> annotations) =>
        annotations.OrderByDescending(annotation => annotation.UpdatedAt)
            .ThenBy(annotation => annotation.Id, StringComparer.Ordinal)
            .ToList();

    private static string NextAnnotationId(IReadOnlyList<PageAnnotation> annotations)
    {
        var max = 0;
        foreach (var annotation in annotations)
        {
            if (annotation.Id.StartsWith("ann-", StringComparison.Ordinal) &&
                int.TryParse(annotation.Id["ann-".Length..], out var value) && value > max)
            {
                max = value;
            }
        }

        return $"ann-{max + 1}";
    }

    private void WriteAnnotations(IReadOnlyList<PageAnnotation> annotations)
    {
        if (_isGuest)
        {
            _guestAnnotations = annotations.ToList();
            return;
        }

        var builder = new StringBuilder();
        foreach (var annotation in annotations)
        {
            builder.Append(EncodeField(annotation.Id)).Append('\t')
                .Append(EncodeField(annotation.Url)).Append('\t')
                .Append(EncodeField(annotation.PageTitle)).Append('\t')
                .Append(EncodeField(annotation.Quote)).Append('\t')
                .Append(EncodeField(annotation.Prefix)).Append('\t')
                .Append(EncodeField(annotation.Suffix)).Append('\t')
                .Append(EncodeField(annotation.Comment)).Append('\t')
                .Append(annotation.CreatedAt.ToUnixTimeMilliseconds()).Append('\t')
                .Append(annotation.UpdatedAt.ToUnixTimeMilliseconds()).Append('\n');
        }

        LumoraFile.WriteAllText(_annotationsFile, builder.ToString());
    }

    private static PageAnnotation? ParseLine(string line)
    {
        var parts = line.Split('\t');
        if (parts.Length < 9 ||
            !long.TryParse(parts[7], out var createdMs) ||
            !long.TryParse(parts[8], out var updatedMs))
        {
            return null;
        }

        return new PageAnnotation(
            DecodeField(parts[0]),
            DecodeField(parts[1]),
            DecodeField(parts[2]),
            DecodeField(parts[3]),
            DecodeField(parts[4]),
            DecodeField(parts[5]),
            DecodeField(parts[6]),
            DateTimeOffset.FromUnixTimeMilliseconds(createdMs).ToLocalTime(),
            DateTimeOffset.FromUnixTimeMilliseconds(updatedMs).ToLocalTime());
    }

    // Percent-encoding octet par octet (même famille que NoteStore) : les
    // tabulations et retours à la ligne des extraits survivent au format TSV.
    private static string EncodeField(string value)
    {
        var builder = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            if ((b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z') || (b >= '0' && b <= '9') || b is (byte)'-' or (byte)'_' or (byte)'.' or (byte)'~')
            {
                builder.Append((char)b);
            }
            else
            {
                builder.Append('%').Append(b.ToString("X2"));
            }
        }

        return builder.ToString();
    }

    private static string DecodeField(string value)
    {
        var bytes = new List<byte>();
        for (var index = 0; index < value.Length;)
        {
            if (value[index] == '%' && index + 2 < value.Length && byte.TryParse(value.Substring(index + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var decoded))
            {
                bytes.Add(decoded);
                index += 3;
            }
            else
            {
                bytes.Add((byte)value[index]);
                index += 1;
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
