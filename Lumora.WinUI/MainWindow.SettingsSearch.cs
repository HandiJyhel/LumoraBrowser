using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // ── Recherche du panneau Reglages (0.94.0.0-dev, "Reglages, piste A") ──────
    // Remplace le rangement par categories (rail a 4 groupes, retire dans le
    // meme lot) : on tape le nom d'un reglage, on saute directement dessus,
    // meme s'il vit dans un sous-onglet cache (Mon Lumora/Accessibilite/
    // Confidentialite en ont chacun 5 - voir AppearanceSubNav_Click/
    // AccessibilitySubNav_Click/PrivacySubNav_Click dans MainWindow.xaml.cs).
    //
    // Index a la main, a la granularite section/sous-onglet (~21 entrees) et
    // non pas controle par controle (~105 TextBlocks de description dans le
    // panneau) : construire un index exhaustif toucherait a une bien plus
    // grande surface de XAML, avec un vrai risque de confondre description
    // statique et texte d'etat dynamique (ex. SettingsPendingText,
    // AccessibilityRescueStatusText) - laisse pour une passe separee.
    private sealed record SettingsSearchEntry(string Section, string? SubGroup, string Label, string Path, string Keywords);

    private static readonly SettingsSearchEntry[] SettingsSearchIndex =
    [
        new("overview", null, "Vue d'ensemble", "Vue d'ensemble", "aperçu tableau de bord accueil"),
        new("navigation", null, "Espace de travail", "Espace de travail",
            "recherche moteur google duckduckgo bing brave suggestions adresse traduction traduire assistant ia phi-3 historique sémantique intelligent onglets verticaux palette commande ctrl+k raccourci"),
        new("appearance", "identity", "Identité", "Mon Lumora › Identité",
            "avatar photo fond d'écran wallpaper image"),
        new("appearance", "theme", "Thème et couleurs", "Mon Lumora › Thème et couleurs",
            "thème sombre clair système couleur palette accent mode d'usage neutre équilibre focus lecture création recherche nuit personnalité"),
        new("appearance", "layout", "Disposition", "Mon Lumora › Disposition",
            "disposition position onglets favoris halo atelier flux compact plein écran taille interface densité animations"),
        new("appearance", "newtab", "Nouvel onglet", "Mon Lumora › Nouvel onglet",
            "nouvel onglet titre raccourcis curseur recherche"),
        new("appearance", "discovery", "Découverte", "Mon Lumora › Découverte",
            "écran de bienvenue découverte revoir présentation premier lancement"),
        new("profile", null, "Profils locaux", "Profils locaux",
            "profil pin verrouillage identité récupération utilisateur changer"),
        new("accessibility", "profiles", "Profils prêts à l'emploi", "Accessibilité › Profils",
            "profil rapide vision fatiguée mode secours personnalisé aucune aide"),
        new("accessibility", "display", "Affichage de Lumora", "Accessibilité › Affichage",
            "contraste renforcé texte plus lisible transitions focus clavier boutons plus grands"),
        new("accessibility", "webcontent", "Contenu des pages web", "Accessibilité › Contenu des pages",
            "lumière bleue espacement texte dyslexie daltonien couleurs saturation"),
        new("accessibility", "reading", "Aides à la lecture", "Accessibilité › Aides à la lecture",
            "lecture à voix haute loupe captcha guide immersif vocal audio"),
        new("accessibility", "keyboard", "Navigation clavier", "Accessibilité › Navigation clavier",
            "raccourcis clavier ctrl+alt tab échap lecteur d'écran"),
        new("startup", null, "Ouverture", "Ouverture",
            "démarrage session précédente accueil page restaurer"),
        new("privacy", "ads", "Publicités et traceurs", "Confidentialité › Publicités",
            "bloqueur publicité pub traceurs adblock"),
        new("privacy", "tracking", "Anti-pistage technique", "Confidentialité › Anti-pistage",
            "empreinte fingerprint pistage tracking smartscreen"),
        new("privacy", "connection", "Sécurité de connexion", "Confidentialité › Connexion",
            "https sécurité connexion certificat"),
        new("privacy", "hygiene", "Hygiène de session", "Confidentialité › Hygiène",
            "cookies nettoyage historique session effacer télémétrie url"),
        new("privacy", "exceptions", "Exceptions", "Confidentialité › Exceptions",
            "exceptions liste blanche domaines ignorés whitelist"),
        new("vault", null, "Coffre et données", "Coffre et données",
            "mots de passe portefeuille sauvegarde coffre passkeys totp"),
        new("storage", null, "Stockage local", "Stockage local",
            "stockage disque dossier données emplacement"),
    ];

    private void SettingsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SettingsSearchBox.Text.Trim();
        if (query.Length == 0)
        {
            SettingsSearchResultsPanel.Visibility = Visibility.Collapsed;
            SettingsSectionsHost.Visibility = Visibility.Visible;
            return;
        }

        SettingsSectionsHost.Visibility = Visibility.Collapsed;
        SettingsSearchResultsPanel.Visibility = Visibility.Visible;
        SettingsSearchResultsPanel.Children.Clear();

        var matches = SettingsSearchIndex
            .Select(entry => (entry, score: ScoreSettingsSearchEntry(entry, query)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Select(x => x.entry)
            .ToList();

        if (matches.Count == 0)
        {
            SettingsSearchResultsPanel.Children.Add(new TextBlock
            {
                Text = "Aucun résultat.",
                Style = (Style)RootShell.Resources["NovaPanelDescriptionStyle"]
            });
            return;
        }

        foreach (var entry in matches)
        {
            var content = new StackPanel { Spacing = 2, Padding = new Thickness(10, 8, 10, 8) };
            content.Children.Add(new TextBlock { Text = entry.Label, FontWeight = FontWeights.SemiBold });
            content.Children.Add(new TextBlock { Text = "Dans " + entry.Path, FontSize = 12, Opacity = 0.68 });

            var button = new Button
            {
                Style = (Style)RootShell.Resources["NovaModuleFlyoutActionStyle"],
                Content = content
            };
            // Sans ceci, le bouton reste sans nom pour un lecteur d'ecran (le
            // contenu StackPanel/TextBlock n'est pas remonte automatiquement
            // en Name UIA) - trouve en verification reelle (0.94.0.0-dev).
            AutomationProperties.SetName(button, $"{entry.Label}, dans {entry.Path}");
            button.Click += (_, _) => NavigateToSettingsSearchEntry(entry);
            SettingsSearchResultsPanel.Children.Add(button);
        }
    }

    private static int ScoreSettingsSearchEntry(SettingsSearchEntry entry, string query)
    {
        var score = 0;
        if (entry.Label.Equals(query, StringComparison.CurrentCultureIgnoreCase)) score += 150;
        if (entry.Label.StartsWith(query, StringComparison.CurrentCultureIgnoreCase)) score += 90;
        if (entry.Label.Contains(query, StringComparison.CurrentCultureIgnoreCase)) score += 60;
        if (entry.Path.Contains(query, StringComparison.CurrentCultureIgnoreCase)) score += 30;
        if (entry.Keywords.Contains(query, StringComparison.CurrentCultureIgnoreCase)) score += 45;
        return score;
    }

    private void NavigateToSettingsSearchEntry(SettingsSearchEntry entry)
    {
        var target = entry.Section switch
        {
            "overview" => SettingsNavOverview,
            "appearance" => SettingsNavAppearance,
            "navigation" => SettingsNavNavigation,
            "privacy" => SettingsNavPrivacy,
            "vault" => SettingsNavVault,
            "profile" => SettingsNavProfile,
            "accessibility" => SettingsNavAccessibility,
            "startup" => SettingsNavStartup,
            "storage" => SettingsNavStorage,
            _ => null
        };
        if (target is null) return;

        // Vide la recherche en premier : declenche SettingsSearchBox_TextChanged
        // (query vide) qui remet SettingsSectionsHost visible AVANT qu'on choisisse
        // la bonne section juste en dessous - meme flux que si l'utilisateur avait
        // efface la recherche a la main puis clique dans le rail.
        SettingsSearchBox.Text = string.Empty;

        target.IsChecked = true;
        SettingsNav_Click(target, new RoutedEventArgs());

        if (entry.SubGroup is null) return;

        RadioButton? sub = (entry.Section, entry.SubGroup) switch
        {
            ("appearance", "identity") => AppearanceSubNavIdentity,
            ("appearance", "theme") => AppearanceSubNavTheme,
            ("appearance", "layout") => AppearanceSubNavLayout,
            ("appearance", "newtab") => AppearanceSubNavNewTab,
            ("appearance", "discovery") => AppearanceSubNavDiscovery,
            ("accessibility", "profiles") => AccessibilitySubNavProfiles,
            ("accessibility", "display") => AccessibilitySubNavDisplay,
            ("accessibility", "webcontent") => AccessibilitySubNavWebContent,
            ("accessibility", "reading") => AccessibilitySubNavReading,
            ("accessibility", "keyboard") => AccessibilitySubNavKeyboard,
            ("privacy", "ads") => PrivacySubNavAds,
            ("privacy", "tracking") => PrivacySubNavTracking,
            ("privacy", "connection") => PrivacySubNavConnection,
            ("privacy", "hygiene") => PrivacySubNavHygiene,
            ("privacy", "exceptions") => PrivacySubNavExceptions,
            _ => null
        };
        if (sub is null) return;

        sub.IsChecked = true;
        switch (entry.Section)
        {
            case "appearance": AppearanceSubNav_Click(sub, new RoutedEventArgs()); break;
            case "accessibility": AccessibilitySubNav_Click(sub, new RoutedEventArgs()); break;
            case "privacy": PrivacySubNav_Click(sub, new RoutedEventArgs()); break;
        }
    }
}
