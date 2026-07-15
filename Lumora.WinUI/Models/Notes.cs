using System.Text;

namespace Lumora.WinUI;

// ── Bloc-notes local ─────────────────────────────────────────────────────────
// Classe PURE (aucune dépendance UI), compilée aussi dans Lumora.Tests.
// Des notes libres, éventuellement rattachées à une page web (URL), stockées
// chiffrées DPAPI dans le profil (notes.lumora) — rien ne quitte l'appareil.
// Même patron que BookmarkStore : lecture/écriture complètes du fichier,
// mode invité en mémoire de session uniquement.

public sealed record Note(
    string Id,
    string Title,
    string Content,
    string Url,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class NoteStore
{
    private readonly string _notesFile;
    private bool _isGuest;
    private List<Note>? _guestNotes;

    public NoteStore(string notesFile) => _notesFile = notesFile;

    public void SetGuestMode(bool isGuest)
    {
        _isGuest = isGuest;
        if (!isGuest) _guestNotes = null;
    }

    // Notes triées de la plus récemment modifiée à la plus ancienne.
    public List<Note> AllNotes()
    {
        if (_isGuest) return Sort(new List<Note>(_guestNotes ??= new List<Note>()));

        var content = LumoraFile.TryReadAllText(_notesFile) ?? string.Empty;
        var notes = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseLine)
            .Where(note => note is not null)
            .Cast<Note>()
            .ToList();
        return Sort(notes);
    }

    public Note Add(string title, string content, string url = "")
    {
        var notes = AllNotes();
        var now = NowToMillisecond();
        var created = new Note(NextNoteId(notes), title, content, CleanUrl(url), now, now);
        notes.Add(created);
        WriteNotes(notes);
        return created;
    }

    // Met à jour titre/contenu (et l'URL si fournie) ; UpdatedAt suit la
    // modification réelle : une écriture à contenu identique ne change rien.
    public Note? Update(string id, string title, string content, string? url = null)
    {
        var notes = AllNotes();
        var index = notes.FindIndex(note => note.Id == id);
        if (index < 0) return null;

        var existing = notes[index];
        var newUrl = url is null ? existing.Url : CleanUrl(url);
        if (existing.Title == title && existing.Content == content && existing.Url == newUrl)
        {
            return existing;
        }

        var updated = existing with
        {
            Title = title,
            Content = content,
            Url = newUrl,
            UpdatedAt = NowToMillisecond()
        };
        notes[index] = updated;
        WriteNotes(notes);
        return updated;
    }

    public bool Remove(string id)
    {
        var notes = AllNotes();
        var removed = notes.RemoveAll(note => note.Id == id);
        if (removed > 0) WriteNotes(notes);
        return removed > 0;
    }

    // Titre affichable : titre saisi, sinon première ligne du contenu, sinon
    // libellé neutre. Jamais vide (liste et lecteurs d'écran).
    public static string DisplayTitle(Note note)
    {
        if (!string.IsNullOrWhiteSpace(note.Title)) return note.Title.Trim();

        var firstLine = note.Content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0);
        if (string.IsNullOrWhiteSpace(firstLine)) return "Note sans titre";

        return firstLine.Length <= 60 ? firstLine : firstLine[..57] + "...";
    }

    // Filtre de recherche : titre, contenu et URL, insensible à la casse.
    public static bool Matches(Note note, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var trimmed = query.Trim();
        return note.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
               note.Content.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
               note.Url.Contains(trimmed, StringComparison.OrdinalIgnoreCase);
    }

    // ── Interne ──────────────────────────────────────────────────────────────

    // Le format sérialise en millisecondes Unix : l'horodatage est tronqué dès
    // la création pour qu'un aller-retour disque rende des dates identiques.
    private static DateTimeOffset NowToMillisecond() =>
        DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.Now.ToUnixTimeMilliseconds()).ToLocalTime();

    private static List<Note> Sort(List<Note> notes) =>
        notes.OrderByDescending(note => note.UpdatedAt).ThenBy(note => note.Id, StringComparer.Ordinal).ToList();

    // Même règle que BookmarkStore.IsWebUrl (dupliquée pour garder cette classe
    // compilable seule dans Lumora.Tests) : seules les pages web se rattachent.
    private static string CleanUrl(string url)
    {
        var trimmed = (url ?? string.Empty).Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? trimmed
            : string.Empty;
    }

    private static string NextNoteId(IReadOnlyList<Note> notes)
    {
        var max = 0;
        foreach (var note in notes)
        {
            if (note.Id.StartsWith("note-", StringComparison.Ordinal) &&
                int.TryParse(note.Id["note-".Length..], out var value) && value > max)
            {
                max = value;
            }
        }

        return $"note-{max + 1}";
    }

    private void WriteNotes(IReadOnlyList<Note> notes)
    {
        if (_isGuest)
        {
            _guestNotes = notes.ToList();
            return;
        }

        var builder = new StringBuilder();
        foreach (var note in notes)
        {
            builder.Append(EncodeField(note.Id)).Append('\t')
                .Append(EncodeField(note.Title)).Append('\t')
                .Append(EncodeField(note.Content)).Append('\t')
                .Append(EncodeField(note.Url)).Append('\t')
                .Append(note.CreatedAt.ToUnixTimeMilliseconds()).Append('\t')
                .Append(note.UpdatedAt.ToUnixTimeMilliseconds()).Append('\n');
        }

        LumoraFile.WriteAllText(_notesFile, builder.ToString());
    }

    private static Note? ParseLine(string line)
    {
        var parts = line.Split('\t');
        if (parts.Length < 6 ||
            !long.TryParse(parts[4], out var createdMs) ||
            !long.TryParse(parts[5], out var updatedMs))
        {
            return null;
        }

        return new Note(
            DecodeField(parts[0]),
            DecodeField(parts[1]),
            DecodeField(parts[2]),
            DecodeField(parts[3]),
            DateTimeOffset.FromUnixTimeMilliseconds(createdMs).ToLocalTime(),
            DateTimeOffset.FromUnixTimeMilliseconds(updatedMs).ToLocalTime());
    }

    // Percent-encoding octet par octet (même famille que BookmarkStore) : les
    // tabulations et retours à la ligne du contenu survivent au format TSV.
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
