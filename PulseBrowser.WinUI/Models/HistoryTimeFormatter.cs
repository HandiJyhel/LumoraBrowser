namespace PulseBrowser.WinUI;

// Formatage relatif d'une date de visite dans le panneau Historique
// ("Aujourd'hui 14:32", "Hier 09:10", "lun. 18:00", "05/06/2026 11:00").
// Aucune dépendance UI.
internal static class HistoryTimeFormatter
{
    public static string Format(DateTimeOffset time, DateTimeOffset? now = null)
    {
        var reference = now ?? DateTimeOffset.Now;
        var diff = reference.Date - time.LocalDateTime.Date;
        if (diff.TotalDays < 1) return $"Aujourd'hui {time.LocalDateTime:HH:mm}";
        if (diff.TotalDays < 2) return $"Hier {time.LocalDateTime:HH:mm}";
        if (diff.TotalDays < 7)
        {
            return time.LocalDateTime.ToString("ddd HH:mm",
                System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));
        }

        return time.LocalDateTime.ToString("dd/MM/yyyy HH:mm");
    }
}
