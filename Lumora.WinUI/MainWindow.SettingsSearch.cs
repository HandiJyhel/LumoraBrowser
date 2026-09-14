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
    //
    // TargetControlName (2026-09-11, chantier "Reglages sur-mesure") : passe
    // partielle vers la granularite controle-par-controle, sur les reglages
    // les plus probables a etre cherches par leur nom precis plutot que par
    // categorie (deja tous convertis en application instantanee - inutile de
    // sauter dessus s'il faut encore chercher un bouton Appliquer). Reste
    // null pour toutes les entrees section/sous-onglet non converties : la
    // recherche continue de fonctionner pour elles exactement comme avant,
    // juste sans l'atterrissage precis.
    private sealed record SettingsSearchEntry(string Section, string? SubGroup, string Label, string Path, string Keywords, string? TargetControlName = null);

    private static readonly SettingsSearchEntry[] SettingsSearchIndex =
    [
        // ── Entrees a granularite controle precis (voir TargetControlName ci-dessus) ──
        new("appearance", "theme", "Thème (sombre, clair, système)", "Mon Lumora › Thème et couleurs",
            "thème sombre clair système", "ThemeModeCombo"),
        new("appearance", "theme", "Mode d'usage", "Mon Lumora › Thème et couleurs",
            "mode d'usage neutre équilibre focus lecture création recherche nuit", "UsageModeCombo"),
        new("appearance", "layout", "Taille de l'interface", "Mon Lumora › Disposition",
            "taille interface densité confortable standard petit boutons", "UiDensityCombo"),
        new("appearance", "layout", "Barre de favoris visible", "Mon Lumora › Disposition",
            "barre favoris visible masquer", "BookmarksBarSwitch"),
        new("appearance", "layout", "Mise en veille des onglets inactifs", "Mon Lumora › Disposition",
            "mise en veille onglets inactifs mémoire processeur délai inactivité", "TabSuspensionEnabledSwitch"),
        new("appearance", "newtab", "Titre affiché du nouvel onglet", "Mon Lumora › Nouvel onglet",
            "titre affiché nom logiciel personnalisé nouvel onglet", "NewTabTitleBox"),
        // Pas de TargetControlName ici : ListView.Focus() echoue sur
        // ContextMenuItemsList (trouve en verifiant en direct, cause non
        // elucidee - peut-etre lie a son remplissage dynamique). Atterrir sur
        // le bon sous-onglet fonctionne parfaitement sans cette derniere
        // etape ; pas de quoi bloquer l'entree pour ca.
        new("appearance", "advanced", "Menu contextuel (clic droit)", "Mon Lumora › Avancé",
            "menu contextuel clic droit personnaliser masquer réordonner"),
        new("profile", null, "Verrouillage automatique par inactivité", "Profils locaux",
            "verrouillage automatique session inactivité délai", "SessionTimeoutCombo"),
        new("profile", null, "Sons & ambiance", "Profils locaux › Sons & ambiance",
            "son clic bascule pack arcade ambiance cascade mer pluie musique fond relaxant"),
        new("navigation", null, "Moteur de recherche", "Espace de travail",
            "moteur recherche google duckduckgo bing brave", "SearchEngineCombo"),
        new("privacy", "ads", "Bloqueur de publicités et traceurs", "Confidentialité › Publicités",
            "bloqueur publicité pub traceurs adblock", "NetworkBlockerSwitch"),
        new("accessibility", "advanced", "Rappel de pause", "Accessibilité › Avancé",
            "rappel pause temps usage continu cognitif attention", "AccessibilityBreakReminderCombo"),
        new("accessibility", "advanced", "Remplacer les sons par un flash visuel", "Accessibilité › Avancé",
            "son notification flash visuel auditif silencieux muet", "AccessibilitySoundsAsVisualFlashSwitch"),

        // ── Entrees a granularite section/sous-onglet (historique, 21 entrees) ──
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
        new("accessibility", "advanced", "Avancé — Nouvelles aides", "Accessibilité › Avancé",
            "réordonner sans clic maintenu survol dwell zoom mémorisé par site curseur agrandi contrasté mode simplifié rappel de pause sons flash visuel"),
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
        WinUiRuntimeTrace.Write($"SettingsSearchBox_TextChanged: query='{query}' matches={matches.Count}" +
            (matches.Count > 0 ? $" premier='{matches[0].Label}' target='{matches[0].TargetControlName}'" : ""));

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
            ("appearance", "advanced") => AppearanceSubNavAdvanced,
            ("appearance", "discovery") => AppearanceSubNavDiscovery,
            ("accessibility", "profiles") => AccessibilitySubNavProfiles,
            ("accessibility", "display") => AccessibilitySubNavDisplay,
            ("accessibility", "webcontent") => AccessibilitySubNavWebContent,
            ("accessibility", "reading") => AccessibilitySubNavReading,
            ("accessibility", "keyboard") => AccessibilitySubNavKeyboard,
            ("accessibility", "advanced") => AccessibilitySubNavAdvanced,
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

        BringSearchTargetIntoFocus(entry.TargetControlName);
    }

    // Atterrissage precis (2026-09-11) : une fois la bonne page affichee,
    // fait defiler jusqu'au controle exact et lui donne le focus - le
    // rectangle de focus WinUI sert lui-meme de "surlignage", sans storyboard
    // maison a ecrire/maintenir. Reste silencieux (pas d'exception) pour tout
    // nom absent de la table : les entrees sans TargetControlName (recherche
    // a granularite section) continuent de fonctionner exactement comme
    // avant, seul l'atterrissage precis est un bonus quand il est defini.
    private void BringSearchTargetIntoFocus(string? controlName)
    {
        if (string.IsNullOrEmpty(controlName)) return;

        Control? target = controlName switch
        {
            "ThemeModeCombo" => ThemeModeCombo,
            "UsageModeCombo" => UsageModeCombo,
            "UiDensityCombo" => UiDensityCombo,
            "BookmarksBarSwitch" => BookmarksBarSwitch,
            "TabSuspensionEnabledSwitch" => TabSuspensionEnabledSwitch,
            "NewTabTitleBox" => NewTabTitleBox,
            "ContextMenuItemsList" => ContextMenuItemsList,
            "SessionTimeoutCombo" => SessionTimeoutCombo,
            "SearchEngineCombo" => SearchEngineCombo,
            "NetworkBlockerSwitch" => NetworkBlockerSwitch,
            "AccessibilityBreakReminderCombo" => AccessibilityBreakReminderCombo,
            "AccessibilitySoundsAsVisualFlashSwitch" => AccessibilitySoundsAsVisualFlashSwitch,
            _ => null
        };
        if (target is null)
        {
            WinUiRuntimeTrace.Write($"BringSearchTargetIntoFocus: controlName='{controlName}' non resolu");
            return;
        }

        // Differe au prochain passage de mise en page (2026-09-11, trouve en
        // verifiant en direct) : appele juste apres avoir bascule la
        // Visibility de plusieurs groupes (SettingsNav_Click/
        // AppearanceSubNav_Click), le controle cible n'a pas encore ete
        // mesure/arrange par XAML - Focus() echoue silencieusement (renvoie
        // false) si on l'appelle dans le meme passage synchrone. Un aller-
        // retour DispatcherQueue suffit a laisser la mise en page se faire.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            target.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0.2 });
            var focused = target.Focus(FocusState.Programmatic);
            WinUiRuntimeTrace.Write($"BringSearchTargetIntoFocus: controlName='{controlName}' focus reussi={focused}");
        });
    }
}
