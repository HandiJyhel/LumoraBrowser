using Xunit;

namespace Lumora.Tests;

// Verrouille la correction de la collision de nom entre le mode de navigation
// "Equilibre" (MainWindow.UsageMode.cs, UiSettings.UsageMode) et le profil de
// confort "Equilibre" (MainWindow.ComfortProfiles.cs, UiSettings.
// AccessibilityComfortProfile) : meme libelle affiche, meme cle technique
// brute "balanced", mais deux systemes de reglages totalement disjoints -
// trouve en audit le 2026-07-20 a la demande de l'utilisateur ("je ne
// comprends pas l'interet [...] certains modes n'ont pas d'interet"). Le
// profil de confort ne configurait d'ailleurs rien du tout (etat par
// defaut) : renomme "Aucune aide" plutot que supprime, pour rester la cle de
// reinitialisation explicite du systeme de profils.
public sealed class AccessibilityComfortNamingTests
{
    [Fact]
    public void Le_profil_de_confort_vide_est_renomme_aucune_aide_pas_equilibre()
    {
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.ComfortProfiles.cs");

        Assert.Contains("\"Aucune aide\"", source, StringComparison.Ordinal);
        // Le preset lui-meme (pas les commentaires explicatifs qui peuvent
        // legitimement citer l'ancien nom pour documenter le renommage) ne
        // doit plus porter le libelle "Equilibre".
        Assert.DoesNotContain("\"Equilibre\",", source, StringComparison.Ordinal);
    }

    [Fact]
    public void La_cle_technique_balanced_est_conservee_pour_ne_pas_casser_les_reglages_sauvegardes()
    {
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.ComfortProfiles.cs");

        // La cle persistee (UiSettings.AccessibilityComfortProfile) ne doit
        // pas changer : seul le libelle affiche est corrige.
        Assert.Contains("\"balanced\" or \"vision\" or \"rescue\" or \"custom\"", source, StringComparison.Ordinal);
    }

    // "calm" et "reading" retires le 2026-07-20 (tri des profils de confort a
    // la demande de l'utilisateur : un profil a un seul reglage, ou une
    // combinaison artificielle, n'aide personne). Un ancien fichier de
    // reglages qui contient encore une de ces deux cles ne doit pas planter :
    // il doit degrader proprement vers "custom".
    [Fact]
    public void Les_anciennes_cles_calm_et_reading_degradent_proprement_vers_custom()
    {
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.ComfortProfiles.cs");

        var normalizeIndex = source.IndexOf("private static string NormalizeAccessibilityComfortProfile", StringComparison.Ordinal);
        Assert.True(normalizeIndex >= 0, "Methode NormalizeAccessibilityComfortProfile introuvable.");

        var nextMethodIndex = source.IndexOf("private static AccessibilityComfortProfilePreset? FindAccessibilityComfortPreset", normalizeIndex + 1, StringComparison.Ordinal);
        Assert.True(nextMethodIndex > normalizeIndex, "Fin de methode introuvable.");

        var methodBody = source[normalizeIndex..nextMethodIndex];
        Assert.DoesNotContain("\"calm\"", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"reading\"", methodBody, StringComparison.Ordinal);
        Assert.Contains("_ => \"custom\"", methodBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_xaml_du_panneau_confort_utilise_aussi_le_nouveau_libelle()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.Contains("<TextBlock x:Name=\"AccessibilityQuickCurrentText\" Text=\"Aucune aide\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_mode_de_navigation_equilibre_garde_son_nom_car_ce_nest_pas_lui_le_probleme()
    {
        // Le mode de navigation "Equilibre" (UsageMode) est un vrai preset
        // (style de nouvel onglet, palette, modules epingles) : contrairement
        // au profil de confort du meme nom, il n'y avait aucune raison de le
        // renommer.
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.Contains("<TextBlock x:Name=\"UsageModeCurrentText\" Text=\"Équilibre\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_contraste_eleve_signale_qu_il_eclipse_le_mode_de_navigation()
    {
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.UsageMode.cs");

        Assert.Contains("var eclipsedByHighContrast = _uiSettings.AccessibilityHighContrast;", source, StringComparison.Ordinal);
        Assert.Contains("Couleurs remplacées tant que le contraste élevé (Confort) est actif.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_signal_d_eclipse_se_met_a_jour_des_qu_un_reglage_d_accessibilite_change()
    {
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.SettingsTheme.cs");

        Assert.Contains("UpdateUsageModeButtonUi();", source, StringComparison.Ordinal);
    }

    // Bug distinct trouve en verification live le 2026-07-20 : activer un
    // profil de confort via le raccourci rapide (footer/Ctrl+Alt+6-9)
    // changeait bien le chrome natif WinUI, mais une page d'accueil Lumora
    // deja ouverte gardait ses anciennes couleurs/tailles - NewTabHome.cs
    // lit _uiSettings au moment du rendu HTML, jamais redeclenche par
    // ApplyAccessibilitySettings() seul. Symptome signale par l'utilisateur :
    // "Vision fatiguee" semblait ne rien faire du tout. Le bouton
    // "Appliquer les changements" des Reglages (ApplySettingsChangesButton_Click)
    // le faisait deja correctement ; seul le chemin rapide l'oubliait.
    //
    // Corrige le 2026-07-20 (refonte Confort) en extrayant un point
    // d'application unique, ApplyAccessibilityComfortSideEffects(), reutilise
    // par le preset, le retour du mode secours (ApplyAccessibilityComfortSnapshot,
    // qui avait exactement le meme oubli) et chaque toggle individuel des
    // Reglages : ce test verrouille que RefreshNovaHomePages() vit desormais
    // dans ce point unique, et que tous les chemins l'appellent.
    [Fact]
    public void Le_point_d_application_unique_du_confort_rafraichit_les_pages_d_accueil_deja_ouvertes()
    {
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.ComfortProfiles.cs");

        var sideEffectsIndex = source.IndexOf("private void ApplyAccessibilityComfortSideEffects()", StringComparison.Ordinal);
        Assert.True(sideEffectsIndex >= 0, "Methode ApplyAccessibilityComfortSideEffects introuvable.");

        var nextMethodIndex = source.IndexOf("private void UpdateAccessibilityComfortProfileFromControls", sideEffectsIndex + 1, StringComparison.Ordinal);
        Assert.True(nextMethodIndex > sideEffectsIndex, "Fin de methode introuvable.");

        var methodBody = source[sideEffectsIndex..nextMethodIndex];
        Assert.Contains("RefreshNovaHomePages();", methodBody, StringComparison.Ordinal);

        Assert.Contains("ApplyAccessibilityComfortSideEffects();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_retour_du_mode_secours_partage_le_meme_point_d_application_que_les_presets()
    {
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.AccessibilityRescue.cs");

        var restoreIndex = source.IndexOf("private void ApplyAccessibilityComfortSnapshot(", StringComparison.Ordinal);
        Assert.True(restoreIndex >= 0, "Methode ApplyAccessibilityComfortSnapshot introuvable.");

        var methodBody = source[restoreIndex..];
        Assert.Contains("ApplyAccessibilityComfortSideEffects();", methodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshNovaHomePages();", methodBody, StringComparison.Ordinal);
    }

    // Bug de contraste trouve dans la meme verification live : la couleur du
    // texte d'indication ("placeholder") de la barre de recherche de l'accueil
    // Lumora restait fixe (#81786d, brun-gris) meme en contraste eleve, alors
    // que le fond de la barre devient blanc pur dans ce mode - correspond
    // probablement a "on voit plus rien" signale par l'utilisateur.
    //
    // Assertion mise a jour le 2026-08-10 : le correctif original codait la
    // couleur en dur dans la regle CSS elle-meme (ternaire inline), mais
    // NewTabThemeVariablesCss() a depuis ete reorganise pour piloter toutes
    // les couleurs de l'accueil via des variables CSS (--nt-*), y compris en
    // contraste eleve (bloc :root dedie). Le test verifiait donc un motif de
    // code qui n'existe plus, alors que le comportement reel (texte
    // d'indication lisible en contraste eleve) est toujours correct - verifie
    // desormais la regle CSS (utilise la variable) et la variable elle-meme
    // dans les deux branches, plutot qu'un unique ternaire inline.
    [Fact]
    public void Le_texte_d_indication_de_la_recherche_suit_le_contraste_eleve()
    {
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.NewTabHome.cs");

        Assert.Contains(
            ".search input::placeholder{color:var(--nt-search-placeholder)}",
            source, StringComparison.Ordinal);
        // Contraste eleve : fond blanc pur, texte d'indication gris fonce
        // lisible (bloc :root dedie, aucune dependance au theme clair/sombre).
        Assert.Contains("--nt-search-placeholder:#4a4a4a", source, StringComparison.Ordinal);
        // Hors contraste eleve : depend du theme clair/sombre, jamais fixe.
        Assert.Contains(
            "--nt-search-placeholder:{{(isDark ? \"#81786d\" : \"#7f705f\")}}",
            source, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Fichier projet introuvable: {string.Join(Path.DirectorySeparatorChar, segments)}");
    }
}
