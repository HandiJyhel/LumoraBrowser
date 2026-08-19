namespace Lumora.WinUI;

// Formatage pur (aucune dependance UI) pour le tableau de bord "Mon compte"
// (panneau Profils locaux, session "Recuperation" 2026-08-15) : taille des
// donnees du profil et anciennete de la derniere sauvegarde. Meme esprit que
// HistoryTimeFormatter (Models/HistoryTimeFormatter.cs) - logique isolee,
// testable sans WinUI.
internal static class AccountDashboardFormatter
{
    // "0 o" / "512 o" / "48,3 Ko" / "312 Mo" / "1,2 Go". Unite choisie par la
    // plus grande dont la valeur reste >= 1 (pas de "0,0 Go" pour un profil
    // tout neuf).
    public static string FormatDataSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} o";

        double value = bytes;
        string[] units = ["Ko", "Mo", "Go", "To"];
        var unitIndex = -1;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')} {units[unitIndex]}";
    }

    // Utilise par RefreshAccountDashboard : true des qu'une sauvegarde a deja
    // ete faite avec le compte actuel (LastBackupAtUnix > 0).
    public static bool HasBackup(long lastBackupAtUnix) => lastBackupAtUnix > 0;

    // "aujourd'hui", "hier", "il y a 3 jours", "il y a 2 semaines",
    // "il y a 4 mois", "il y a 1 an" - granularite decroissante, coherente
    // avec ce qu'attend un utilisateur pour une date de sauvegarde (pas
    // besoin de l'heure exacte, contrairement a HistoryTimeFormatter).
    public static string FormatRelativeAge(DateTimeOffset at, DateTimeOffset now)
    {
        var days = (int)(now.Date - at.LocalDateTime.Date).TotalDays;
        if (days <= 0) return "aujourd'hui";
        if (days == 1) return "hier";
        if (days < 7) return $"il y a {days} jours";
        if (days < 30)
        {
            var weeks = days / 7;
            return weeks == 1 ? "il y a 1 semaine" : $"il y a {weeks} semaines";
        }
        if (days < 365)
        {
            var months = days / 30;
            return months <= 1 ? "il y a 1 mois" : $"il y a {months} mois";
        }

        var years = days / 365;
        return years <= 1 ? "il y a 1 an" : $"il y a {years} ans";
    }
}
