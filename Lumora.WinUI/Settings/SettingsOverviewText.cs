using System.Globalization;

namespace Lumora.WinUI;

// Ou mene un mot cle souligne de la Vue d'ensemble des Reglages.
internal enum OverviewLink { None, Appearance, Privacy, Storage }

// Action proposee sous les phrases : un seul bouton plein quand il y a quelque
// chose a faire, un simple lien sinon.
internal enum OverviewAction { None, ExportBackup, CreateProfile }

internal readonly record struct OverviewSegment(string Text, OverviewLink Link = OverviewLink.None);

internal sealed record OverviewContent(
    IReadOnlyList<OverviewSegment> Segments,
    OverviewAction Action,
    bool ActionIsPrimary)
{
    public string PlainText => string.Concat(Segments.Select(segment => segment.Text));
}

// Phrases de la Vue d'ensemble des Reglages. Fonction pure : tout ce qui est
// affiche vient d'une valeur reellement lue dans l'application, rien n'est
// invente. Le compteur du bloqueur est en memoire (il repart a zero a chaque
// lancement), d'ou la formulation "depuis l'ouverture".
internal static class SettingsOverviewText
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static OverviewContent Compose(
        bool isGuest,
        bool isDarkTheme,
        string accentName,
        int blockedTotal,
        DateTimeOffset? lastBackupAt,
        bool canBackup,
        DateTimeOffset now)
    {
        var segments = new List<OverviewSegment>();

        if (isGuest)
        {
            segments.Add(new("Tu navigues en invité : rien n'est gardé quand tu fermes Lumora. "));
            AddBlocker(segments, blockedTotal);
            return new OverviewContent(segments, OverviewAction.CreateProfile, ActionIsPrimary: true);
        }

        segments.Add(new("Lumora est en "));
        segments.Add(new(isDarkTheme ? "thème sombre" : "thème clair", OverviewLink.Appearance));
        segments.Add(new(", couleur "));
        segments.Add(new(accentName, OverviewLink.Appearance));
        segments.Add(new(". "));
        AddBlocker(segments, blockedTotal);

        if (!canBackup)
        {
            // Profil sans mot de passe : aucune sauvegarde possible, on ne le
            // lui reproche pas.
            return new OverviewContent(segments, OverviewAction.None, ActionIsPrimary: false);
        }

        segments.Add(new(" "));
        if (lastBackupAt is null)
        {
            segments.Add(new("Tu n'as jamais exporté de "));
            segments.Add(new("sauvegarde", OverviewLink.Storage));
            segments.Add(new("."));
            return new OverviewContent(segments, OverviewAction.ExportBackup, ActionIsPrimary: true);
        }

        segments.Add(new("Ta dernière "));
        segments.Add(new("sauvegarde", OverviewLink.Storage));
        segments.Add(new($" date {BackupDate(lastBackupAt.Value, now)}."));
        return new OverviewContent(segments, OverviewAction.ExportBackup, ActionIsPrimary: false);
    }

    private static void AddBlocker(List<OverviewSegment> segments, int total)
    {
        if (total <= 0)
        {
            segments.Add(new("Le bloqueur n'a encore rien arrêté depuis l'ouverture."));
            return;
        }

        segments.Add(new("Depuis l'ouverture, le bloqueur a arrêté "));
        var label = total == 1 ? "1 requête" : total.ToString("N0", French) + " requêtes";
        segments.Add(new(label, OverviewLink.Privacy));
        segments.Add(new("."));
    }

    // "du 12 octobre" (meme annee) ou "du 12 octobre 2025".
    private static string BackupDate(DateTimeOffset at, DateTimeOffset now)
    {
        var local = at.LocalDateTime;
        var format = local.Year == now.LocalDateTime.Year ? "d MMMM" : "d MMMM yyyy";
        var date = local.ToString(format, French);
        return local.Day == 1 ? "du " + date.Replace("1 ", "1er ", StringComparison.Ordinal) : "du " + date;
    }
}
