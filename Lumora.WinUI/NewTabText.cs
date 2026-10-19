using System.Globalization;

namespace Lumora.WinUI;

// Textes de la page Nouvel onglet (mode Neutre) : salutation, date, phrase
// d'accueil personnalisee, age de la derniere page ouverte. Fonctions pures,
// aucune dependance UI, pour pouvoir les tester sans lancer l'application.
internal static class NewTabText
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    // Couleurs douces des pastilles (raccourcis sans icone, avatar-initiale).
    // Une lettre blanche en 19-22 px gras reste lisible sur chacune d'elles.
    private static readonly string[] TileColors =
    {
        "#8a4b3a", "#4c6a7a", "#9a7a2c", "#a65a2a", "#6a6a6a",
        "#9a3f3f", "#3f6f86", "#5a6f4a", "#6b5a8a", "#3f7a6f"
    };

    public static string GreetingMoment(int hour) => hour switch
    {
        >= 5 and < 12 => "Bonjour",
        >= 12 and < 18 => "Bon après-midi",
        >= 18 and < 23 => "Bonsoir",
        _ => "Bonne nuit"
    };

    // "Bonne nuit, Camille." - sans prenom : "Bonne nuit."
    public static string Greeting(int hour, string? firstName)
    {
        var name = string.IsNullOrWhiteSpace(firstName) ? string.Empty : ", " + firstName.Trim();
        return GreetingMoment(hour) + name + ".";
    }

    // "Lundi 19 octobre, 3 h 06."
    public static string DateLine(DateTime now)
    {
        var day = now.ToString("dddd d MMMM", French);
        if (day.Length > 0) day = char.ToUpper(day[0], French) + day[1..];
        return $"{day}, {now.Hour} h {now.Minute:00}.";
    }

    // La phrase d'accueil est stockee dans UiSettings.NewTabTitle. Vide ou
    // egale au nom du produit (valeur par defaut) : pas de phrase perso, on
    // affiche la salutation automatique.
    public static string? ResolvePhrase(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return null;
        var phrase = stored.Trim();
        return string.Equals(phrase, BrandingText.ProductName, StringComparison.OrdinalIgnoreCase) ? null : phrase;
    }

    // "il y a 12 minutes", "il y a 1 heure", "hier", "il y a 3 jours".
    // null au-dela de 30 jours : la ligne "Tu etais sur..." n'a plus de sens.
    public static string? ResumeAge(DateTimeOffset visited, DateTimeOffset now)
    {
        var age = now - visited;
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;
        if (age < TimeSpan.FromMinutes(1)) return "à l'instant";
        if (age < TimeSpan.FromHours(1))
        {
            var minutes = (int)age.TotalMinutes;
            return minutes == 1 ? "il y a 1 minute" : $"il y a {minutes} minutes";
        }
        if (age < TimeSpan.FromHours(12))
        {
            var hours = (int)age.TotalHours;
            return hours == 1 ? "il y a 1 heure" : $"il y a {hours} heures";
        }

        var days = (int)(now.LocalDateTime.Date - visited.LocalDateTime.Date).TotalDays;
        if (days <= 0)
        {
            var hours = Math.Max(1, (int)age.TotalHours);
            return hours == 1 ? "il y a 1 heure" : $"il y a {hours} heures";
        }
        if (days == 1) return "hier";
        if (days <= 30) return $"il y a {days} jours";
        return null;
    }

    // "Tu etais sur <a>titre</a>, il y a 12 minutes." - null si la visite date de
    // plus d'un mois. Titre et adresse sont echappes (un titre de page est une
    // donnee externe : jamais insere tel quel dans le HTML).
    public static string? ResumeHtml(string url, string? title, DateTimeOffset visited, DateTimeOffset now)
    {
        var age = ResumeAge(visited, now);
        if (age is null || string.IsNullOrWhiteSpace(url)) return null;

        var label = string.IsNullOrWhiteSpace(title) ? url : title.Trim();
        if (label.Length > 80) label = label[..79] + "…";

        var link = $"<a href=\"{NewTabMarkup.HtmlAttribute(url)}\">{NewTabMarkup.HtmlText(label)}</a>";
        return age == "à l'instant"
            ? $"Tu viens d'ouvrir {link}."
            : $"Tu étais sur {link}, {age}.";
    }

    // Couleur stable pour un nom donne (meme raccourci, meme couleur).
    public static string TileColor(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return TileColors[0];
        var sum = 0;
        foreach (var c in name.Trim().ToLowerInvariant()) sum = unchecked(sum * 31 + c);
        return TileColors[(sum & int.MaxValue) % TileColors.Length];
    }

    public static string Initial(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        foreach (var c in name.Trim())
        {
            if (char.IsLetterOrDigit(c)) return char.ToUpper(c, French).ToString();
        }
        return "?";
    }
}
