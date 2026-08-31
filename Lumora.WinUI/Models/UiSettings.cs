using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Lumora.WinUI;

internal sealed class UiSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    // Version de schema courante. Une propriete renommee/retypee de facon
    // incompatible doit s'accompagner d'une nouvelle entree dans
    // MigrationSteps (cle = ancienne version) plutot que d'un renommage nu :
    // sinon JsonSerializer.Deserialize<UiSettings> peut lever une exception
    // qui remonte au catch-all de Load() et reinitialise TOUS les reglages a
    // Default(), pas seulement la propriete concernee.
    public const int CurrentSchemaVersion = 2;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public bool BookmarksBarVisible { get; set; } = true;
    public string BookmarksBarPosition { get; set; } = "top";
    public bool VerticalTabsEnabled { get; set; }
    public string TabStripPosition { get; set; } = "top";
    public bool VerticalTabsCompact { get; set; }
    // 230 -> 310 (2026-08-12) : a 230, la rangee d'actions du rail elargi
    // ("+ Nouvel onglet" / muet / "Reduire") debordait de l'espace interieur
    // disponible - le bouton "Reduire" se faisait comprimer a ~26px et
    // perdait son libelle texte, coupe par le bord du rail (mesure en reel,
    // skill verify). 310 est le minimum mesure (icone+texte des 3 boutons =
    // ~287px + ~16px de marges interieures du rail) plus une petite marge,
    // sans depasser VerticalTabsMaxWidth (320, MainWindow.xaml.cs). Doit
    // rester coherent avec VerticalTabsDefaultWidth (meme fichier) : c'est
    // CETTE valeur-ci qui est reellement appliquee au demarrage
    // (ApplyUiSettings lit _uiSettings.VerticalTabsWidth), le champ
    // VerticalTabsDefaultWidth ne sert que d'initialiseur avant le
    // chargement des reglages.
    public double VerticalTabsWidth { get; set; } = 310;
    public bool CompactModeEnabled { get; set; }
    public bool CompactModeHidesBookmarks { get; set; }
    public bool FullScreenAutoHideChrome { get; set; } = true;
    // Taille des elements de chrome (boutons de la barre d'outils, ligne
    // d'outils, barre d'adresse, barre de favoris) : "comfortable" (tailles
    // historiques de Lumora), "standard" (nouveau defaut, plus proche des
    // navigateurs du marche) ou "dense" (le plus petit). Reglage independant
    // du Mode d'usage et de CompactModeEnabled (qui designe le mode plein
    // ecran/immersif "Interface compacte", pas une densite de boutons/texte)
    // - ne pas confondre les deux.
    public string UiDensity { get; set; } = "standard";
    public string WindowBackdrop { get; set; } = "solid";
    // "dark" (identite historique Lumora), "light" ou "system" (suit le reglage
    // clair/sombre de Windows au demarrage et a chaque changement de reglage).
    public string ThemeMode { get; set; } = "dark";
    public string AccentPalette { get; set; } = "lumora";
    public int WindowTransparency { get; set; } = 18;
    public string StartupMode { get; set; } = "restore";
    public string StartupUrl { get; set; } = string.Empty;
    public string SearchEngine { get; set; } = "google";
    // Suggestions locales de la barre d'adresse (onglets ouverts, favoris,
    // historique du profil). Activées par défaut : tout est calculé sur
    // l'appareil, la frappe n'est jamais envoyée à un service distant.
    public bool AddressBarSuggestionsEnabled { get; set; } = true;
    public bool CommandPaletteEnabled { get; set; } = true;
    public bool CommandPaletteOpenFromWebPages { get; set; }
    public bool CommandPaletteOpenFromTextFields { get; set; }
    public string NewTabTitle { get; set; } = BrandingText.ProductName;
    public string NewTabStyle { get; set; } = "signature";
    // "neutral", "balanced", "focus", "reading", "creative", "research", "night".
    // Sert a donner a Lumora une posture d'usage visible, sans creer encore des
    // espaces separes avec leurs propres onglets.
    public string UsageMode { get; set; } = "neutral";
    public string LastIntroducedUsageMode { get; set; } = string.Empty;
    // Interrupteur simple (2026-08-10, demande explicite utilisateur) : le
    // compagnon n'est plus rattache a l'affichage d'un mode d'usage precis
    // ("peu importe le mode, il est toujours la"), juste active ou pas. Vrai
    // par defaut pour ne rien changer au comportement existant tant que
    // personne n'a explicitement desactive.
    public bool CompanionEnabled { get; set; } = true;
    public string CompanionBalancedMemo { get; set; } = string.Empty;
    public string CompanionFocusObjective { get; set; } = string.Empty;
    public string CompanionReadingNote { get; set; } = string.Empty;
    public string CompanionCreativePostIt { get; set; } = string.Empty;
    public string CompanionResearchTrail { get; set; } = string.Empty;
    public string CompanionNightReminder { get; set; } = string.Empty;
    // Couleur personnalisee par mode d'usage ("#RRGGBB", vide = couleur Nova
    // par defaut du mode, codee en dur dans ResolveModeChromePalette). Le
    // second ton du degrade (CoolAccent/WarmAccent) est toujours derive
    // automatiquement de cette seule couleur (DeriveModeAccentTones,
    // MainWindow.ModeAccentColor.cs), jamais stocke separement. "Equilibre"
    // (balanced) n'a pas d'entree ici : hors perimetre de ce palier, garde sa
    // palette par defaut.
    public string ModeAccentColorNeutral { get; set; } = string.Empty;
    public string ModeAccentColorFocus { get; set; } = string.Empty;
    public string ModeAccentColorReading { get; set; } = string.Empty;
    public string ModeAccentColorCreative { get; set; } = string.Empty;
    public string ModeAccentColorResearch { get; set; } = string.Empty;
    public string ModeAccentColorNight { get; set; } = string.Empty;
    // "subtle", "luminous" ou "dynamic". Le reglage accessibilite
    // AccessibilityReduceMotion garde toujours la priorite au rendu statique.
    public string PersonalizationMotionStyle { get; set; } = "luminous";
    public bool NewTabFocusSearchOnOpen { get; set; }
    public bool NewTabShortcutsVisible { get; set; }
    public List<NewTabShortcut> NewTabShortcuts { get; set; } = [];
    // Verrouillage auto par défaut à 10 min (valeur présente dans le sélecteur).
    // Les profils existants conservent la valeur enregistrée dans leur ui-settings.
    public int SessionTimeoutMinutes { get; set; } = 10;
    // Mise en veille des onglets inactifs (2026-08-31, retour utilisateur :
    // optimiser mémoire/CPU). Active par défaut (c'est la demande d'origine),
    // seuil 30 min - voir MainWindow.TabSuspension.cs. 0 ou
    // TabSuspensionEnabled=false désactive entièrement le minuteur, sans
    // affecter le déclenchement manuel (bouton Verrouiller, qui met aussi les
    // onglets en pause quel que soit ce réglage).
    public bool TabSuspensionEnabled { get; set; } = true;
    public int TabSuspensionThresholdMinutes { get; set; } = 30;
    public bool NetworkBlockerEnabled { get; set; } = true;
    public bool TelemetryBlockerEnabled { get; set; } = true;
    // SmartScreen vérifie la réputation des sites en envoyant chaque URL à
    // Microsoft : désactivé par défaut (philosophie locale-first).
    public bool SmartScreenEnabled { get; set; }

    // Renforcement anti-publicité (0.77) : popups (popunders, clics détournés
    // vers un domaine listé) et navigations de l'onglet vers un domaine listé.
    // Actifs par défaut : c'est la protection attendue d'un navigateur moderne ;
    // désactivables ici, et la whitelist par site les lève site par site.
    public bool PopupBlockerEnabled { get; set; } = true;
    public bool StrictAdBlockEnabled { get; set; } = true;
    public bool ParameterCleanerEnabled { get; set; } = true;
    public bool HttpsEnforcerEnabled { get; set; } = true;
    public bool CnameUncloakerEnabled { get; set; } = true;
    // Empeche WebRTC de reveler l'IP locale/publique reelle via des candidats ICE
    // non proxifies, meme quand un site utilise WebRTC sans rapport avec un appel
    // video (fingerprinting). Drapeau Chromium fixe au demarrage du moteur
    // WebView2 : un changement ne s'applique qu'au redemarrage de Lumora.
    public bool WebRtcLeakProtectionEnabled { get; set; } = true;
    // Position fictive renvoyee a navigator.geolocation (cartes, meteo, recherche
    // "pres de moi") au lieu de la vraie position de l'appareil. Contrairement au
    // reglage WebRTC, s'applique immediatement aux onglets ouverts (simple script
    // injecte, pas un drapeau de demarrage du moteur). Paris par defaut.
    public bool GeolocationSpoofingEnabled { get; set; } = true;
    public double GeolocationSpoofLatitude { get; set; } = 48.8566;
    public double GeolocationSpoofLongitude { get; set; } = 2.3522;
    // Bruit leger sur Canvas/WebGL/AudioContext + normalisation
    // hardwareConcurrency/deviceMemory : rend le pistage par empreinte
    // (fingerprinting) inter-sites plus difficile. Aucun intermediaire
    // reseau, aucun cout de vitesse, actif par defaut comme les autres
    // protections gratuites.
    public bool FingerprintProtectionEnabled { get; set; } = true;
    public bool CosmeticFilterEnabled   { get; set; } = true;
    public bool ConsentManagerEnabled   { get; set; } = true;
    public bool AccessibilityHighContrast { get; set; }
    public bool AccessibilityLargeText { get; set; }
    public bool AccessibilityReduceMotion { get; set; }
    public bool AccessibilityVisibleFocus { get; set; } = true;
    // Agrandit les boutons de la barre de navigation (32 -> 44px, cible confort
    // AAA) pour les difficultes motrices. Desactive par defaut : la taille
    // compacte reste la norme, cette aide est choisie par l'utilisateur et non
    // imposee - retour direct du 2026-07-29 apres un premier essai ou la
    // taille agrandie avait ete mise par defaut pour tout le monde.
    public bool AccessibilityLargeTargets { get; set; }
    // Teinte plus chaude (moins de lumiere bleue) sur les pages web visitees,
    // pour la fatigue visuelle en usage prolonge ou en soiree. Volontairement
    // distinct de AccessibilityHighContrast : la basse vision a besoin de
    // contraste maximal, la fatigue visuelle a besoin de l'inverse (moins
    // d'agressivite visuelle). N'affecte pour l'instant que le contenu web,
    // pas encore la chrome native Lumora.
    public bool AccessibilityReduceBlueLight { get; set; }
    // "balanced", "calm", "vision", "reading", "rescue" ou "custom". Sert a
    // memoriser la posture de confort choisie, tout en laissant l'utilisateur
    // retoucher chaque interrupteur manuellement ensuite. "rescue" n'est plus
    // selectionnable depuis la liste de profils des Reglages (c'est une
    // action d'urgence a part, pas un profil qu'on choisit) mais reste une
    // valeur valide, atteignable via la carte "Mode secours" ou le footer.
    public string AccessibilityComfortProfile { get; set; } = "balanced";
    // Espacement du texte des pages web (WCAG 1.4.12), applique par script
    // injecte a chaque navigation (independant du profil de confort ci-dessus,
    // qui ne touche que la chrome native Lumora). "normal" (defaut, aucun
    // changement), "comfortable" ou "wide".
    public string AccessibilityTextSpacing { get; set; } = "normal";
    // Sature/durcit le contraste des pages web visitees pour aider certains
    // daltoniens a distinguer des couleurs proches. Aide pratique, pas une
    // correction colorimetrique scientifique.
    public bool AccessibilityColorBoostEnabled { get; set; }
    // Lecture a voix haute : synthese vocale 100% locale (Windows), aucun
    // texte de page envoye a un serveur externe. Fonction de confort, pas une
    // protection : opt-in comme le micro et l'assistant IA, pour ne pas encombrer
    // la barre d'adresse d'un bouton que l'utilisateur n'a pas demande. Les
    // protections (bloqueur pub/traqueurs, HTTPS, cookies...) restent, elles,
    // actives par defaut. L'utilisateur active la lecture dans les Reglages.
    public bool ReadAloudEnabled { get; set; }
    // Loupe de lecture : capture de la page affichee dans un panneau zoomable,
    // pour les contenus difficiles a lire (captcha visuel deforme, texte
    // minuscule...). Aucune transcription, aucune resolution automatique :
    // une image locale agrandie, l'utilisateur lit et agit lui-meme. Opt-in
    // comme les autres aides de confort.
    public bool AccessibilityReadingLensEnabled { get; set; }
    // Guide de lecture immersif : assombrit legerement le reste de la page et
    // maintient une bande lisible qui suit la lecture dans le viewport. Vise
    // la fatigue visuelle et la perte de ligne sur les longues pages.
    public bool AccessibilityReadingGuideEnabled { get; set; }
    // Hauteur de la bande du guide de lecture, en pixels CSS. Valeurs
    // attendues par l'interface : 120, 160, 220, 300.
    public int AccessibilityReadingGuideBandHeight { get; set; } = 160;
    // Traduction neuronale locale des pages (modele telecharge une fois puis
    // traduction 100% hors ligne). Activee par defaut : simple telechargement
    // generique, pas de donnee personnelle envoyee, sur le meme principe que
    // les listes de filtrage du bloqueur de pubs/traqueurs.
    public bool TranslationEnabled { get; set; } = true;
    // Assistant IA local (Phi-3-mini) pour reformuler une recherche vague.
    // Desactive par defaut : telechargement unique ~2,7 Go, activation
    // explicite requise (contrairement a la traduction, poids nettement
    // plus lourd et fonctionnalite plus proche de l'experimental).
    public bool SearchAssistEnabled { get; set; }
    // Recherche semantique locale dans l'historique : indexe le contenu texte
    // des pages visitees via des embeddings calcules localement (modele
    // multilingual-e5-small, ONNX), permet de retrouver une page par le sens
    // plutot que par mot-clé exact. Desactivee par defaut : telechargement
    // unique ~120 Mo, activation explicite requise (meme principe que
    // SearchAssistEnabled). Le contenu capture est chiffre au meme titre que
    // le reste de l'historique (voir SemanticHistoryIndex).
    public bool HistorySemanticSearchEnabled { get; set; }
    public List<string> PinnedModuleIds { get; set; } = [];
    // Menu Demarrer (ModulesFlyout) : rangee epinglee + historique d'usage
    // pour "Recent et frequent". Vocabulaire d'ids distinct de
    // PinnedModuleIds ci-dessus (surface differente, voir StartMenuTiles.cs).
    // Retour utilisateur du 2026-08-08 : "par defaut, ce que l'utilisateur
    // voit quoi qu'il arrive, c'est le bloqueur de pub et le coffre [...]
    // apres, tout le reste est desepingle" - remplace l'ancienne liste
    // (Favoris/Coffre/Mode lecture/Portefeuille/Parametres) qui epinglait 5
    // tuiles sans que l'utilisateur en ait fait le choix. Ne s'applique
    // qu'aux NOUVEAUX profils (valeur par defaut du champ) - un profil deja
    // existant garde l'epinglage qu'il a sur disque, jamais ecrase au
    // chargement.
    public static readonly string[] DefaultPinnedStartMenuTileIds =
    [
        StartMenuTileIds.AdBlocker,
        StartMenuTileIds.Vault
    ];
    public List<string> PinnedStartMenuTileIds { get; set; } = [.. DefaultPinnedStartMenuTileIds];
    public List<StartMenuTileUsage> StartMenuTileUsage { get; set; } = [];
    // Visibilité des sections TOTP / Passkey dans le Coffre (0.93.48, retour
    // utilisateur : quelqu'un qui utilise déjà une autre appli de 2FA/passkeys
    // n'a pas à se coltiner ces sections dans chaque fiche). Changer l'un ou
    // l'autre redemande le mot de passe/PIN du profil, comme l'ouverture du
    // Coffre (voir VaultTotpFeatureToggle_Toggled/VaultPasskeyFeatureToggle_Toggled,
    // MainWindow.VaultAccess.cs) - jamais un simple interrupteur muet.
    // Désactiver ne supprime jamais rien du fichier du coffre : réactiver fait
    // tout réapparaître à l'identique, y compris sur les fiches qui avaient
    // déjà des données au moment de la désactivation.
    // Désactivées par défaut depuis 0.94.3.0-dev (remplace "activées par défaut
    // pour tout le monde" ci-dessus, retour utilisateur : la plupart des gens ne
    // s'en servent jamais, panneau "Fonctions du Coffre" trop chargé par défaut) -
    // restent activables en un clic, avec désormais une explication en clair
    // affichée dès l'activation (Border sous chaque interrupteur, MainWindow.xaml).
    // Les profils créés avant ce palier gardent la valeur déjà enregistrée dans
    // leur ui-settings.lumora (true) : ce nouveau défaut ne s'applique qu'aux
    // profils qui n'ont encore jamais écrit ce champ.
    public bool VaultTotpFeatureEnabled { get; set; }
    public bool VaultPasskeyFeatureEnabled { get; set; }

    // Préférences du générateur de mots de passe du coffre, partagées entre le
    // dialogue "Nouvel identifiant" et la barre de suggestion automatique.
    public int VaultGeneratorLength { get; set; } = 20;
    public bool VaultGeneratorUseSymbols { get; set; } = true;
    public string VaultGeneratorMode { get; set; } = "random"; // "random" ou "passphrase"
    public int VaultGeneratorPassphraseWords { get; set; } = 5;
    public bool SetupWizardCompleted    { get; set; }
    public List<string> PrivacyWhitelist { get; set; } = new();
    // Mode compatibilite connexion : domaines racines ou l'utilisateur autorise
    // Lumora a relacher uniquement les garde-fous qui cassent l'authentification
    // demandee (ex. Google Identity), sans ouvrir Ads/Analytics globalement.
    public List<string> LoginCompatibilitySites { get; set; } = new();
    public List<string> LoginDiagnosticSites { get; set; } = new();
    public List<SitePermissionRule> SitePermissions { get; set; } = new();
    public List<SiteComfortRule> SiteComfortRules { get; set; } = new();
    // Sessions éphémères : au démarrage, purge des cookies de la session précédente,
    // sauf pour les domaines racines listés comme sites de confiance. Défaut
    // basculé à false le 2026-08-13 (retour utilisateur : "trop compliqué, trop
    // long" pour un utilisateur de base) - les sessions restent connectées par
    // défaut, comme n'importe quel navigateur classique, conformément à
    // l'exigence fondatrice du projet (AGENTS.md : "sans forcer l'utilisateur à
    // se reconnecter inutilement"). La purge au démarrage devient une option de
    // confidentialité explicite (Réglages > Vie privée > Hygiène de session),
    // pour qui la veut vraiment - le scénario qu'elle protège (quelqu'un
    // d'autre reprend la machine après fermeture de Lumora) reste couvert par
    // Incognito et par le verrouillage auto du coffre, qui ne dépendent pas de
    // ce réglage. Voir MigrationSteps[1] : les profils déjà enregistrés avec
    // l'ancien défaut (true) sont basculés à false une seule fois au chargement.
    public bool SessionPurgeEnabled { get; set; } = false;
    public List<string> TrustedSessionSites { get; set; } = new();
    // Vrai dès qu'une purge réelle a été expliquée une première fois à l'utilisateur
    // (InfoBar affichée une seule fois, jamais republiée automatiquement ensuite).
    public bool SessionPurgeExplained { get; set; }
    // Domaines racines pour lesquels l'utilisateur a refusé la proposition « Rester
    // connecté ? » au login : on ne le lui repropose plus à chaque connexion.
    public List<string> SessionKeepDeclinedSites { get; set; } = new();
    // Réorganisation personnalisée de la barre d'outils (0.93.34.0-dev) : ordre
    // des boutons selon les préférences de l'utilisateur (drag-drop en mode édition).
    // Clés = x:Name des boutons (ex: "ReaderModeButton"). Absent ou vide = ordre par défaut.
    public List<string> ToolbarButtonOrder { get; set; } = new();
    // Positions des séparateurs visuels dans la section des modules (indice après lequel ajouter un séparateur).
    // Absent ou vide = pas de séparateur personnalisé (ordre linéaire simple).
    public List<int> ToolbarSeparatorPositions { get; set; } = new();
    // Tableau de bord "Mon compte" (0.93.45.0-dev) : date de la derniere
    // sauvegarde .lum reussie avec CE compte (Unix seconds, 0 = jamais) et
    // nom du fichier produit - affiches dans Parametres > Profils locaux,
    // ecrits par ExportBackupButton_Click (MainWindow.SettingsStorage.cs)
    // juste apres un LumoraBackup.Export reussi.
    public long LastBackupAtUnix { get; set; }
    public string LastBackupFileName { get; set; } = string.Empty;

    public static UiSettings Default() => new();

    public static UiSettings Load(string path, string? legacyPath = null)
    {
        try
        {
            if (!File.Exists(path))
            {
                // Migration depuis .json legacy
                if (legacyPath is not null && File.Exists(legacyPath))
                {
                    var legacyJson = MigrateJson(File.ReadAllText(legacyPath));
                    var migrated = JsonSerializer.Deserialize<UiSettings>(legacyJson, JsonOptions) ?? Default();
                    migrated.ApplyLegacyBrandingMigration();
                    migrated.ApplyPinnedModulesMigration(legacyJson);
                    migrated.RemoveLegacyDefaultShortcuts();
                    migrated.Save(path);
                    try { File.Delete(legacyPath); } catch { }
                    return migrated;
                }

                return Default();
            }

            var rawJson = LumoraFile.TryReadAllText(path);
            if (rawJson is null) return Default();
            var json = MigrateJson(rawJson);
            var settings = JsonSerializer.Deserialize<UiSettings>(json, JsonOptions) ?? Default();
            settings.ApplyLegacyBrandingMigration();
            settings.ApplyPinnedModulesMigration(json);
            settings.RemoveLegacyDefaultShortcuts();
            return settings;
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"UI settings load skipped: {error.GetType().Name}");
            return Default();
        }
    }

    public void Save(string path)
    {
        try
        {
            SchemaVersion = CurrentSchemaVersion;
            LumoraFile.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"UI settings save skipped: {error.GetType().Name}");
        }
    }

    // Cle N = etape qui migre une version N vers N+1, appliquee sur le JSON
    // brut AVANT la deserialisation typee. Version 1 = la version de reference
    // qui introduit ce mecanisme (aucune etape). Pour une future propriete
    // renommee, ajouter par exemple :
    // [1] = root => { if (root.Remove("AncienNom", out var v)) root["NouveauNom"] = v; },
    private static readonly IReadOnlyDictionary<int, Action<JsonObject>> MigrationSteps =
        new Dictionary<int, Action<JsonObject>>
        {
            // v1 -> v2 (2026-08-13) : SessionPurgeEnabled bascule de true a false
            // par defaut (voir commentaire sur la propriete). Un profil deja
            // enregistre AVANT ce changement porte forcement "true" en clair dans
            // son JSON (Save() serialise toutes les proprietes) - impossible de
            // distinguer ce true "herite de l'ancien defaut" d'un true choisi
            // expressement une fois deja ecrit sur disque. Logiciel non publie,
            // aucune base d'utilisateurs dont un choix explicite serait a
            // preserver : tout true herite est traite comme l'ancien defaut et
            // bascule. Un profil qui a explicitement mis false n'est pas touche
            // (deja la valeur cible).
            [1] = root =>
            {
                if ((bool?)root["SessionPurgeEnabled"] == true)
                    root["SessionPurgeEnabled"] = false;
            },
        };

    // Applique en sequence chaque etape necessaire entre fromVersion et
    // toVersion. internal (pas private) pour permettre aux tests de verifier
    // le chainage avec une table de migrations synthetique, sans dependre
    // d'un vrai renommage futur.
    internal static JsonObject ApplyMigrations(
        JsonObject root, int fromVersion, int toVersion,
        IReadOnlyDictionary<int, Action<JsonObject>> steps)
    {
        var version = fromVersion;
        while (version < toVersion && steps.TryGetValue(version, out var step))
        {
            step(root);
            version++;
        }

        root["SchemaVersion"] = toVersion;
        return root;
    }

    private static string MigrateJson(string json)
    {
        JsonObject? root;
        try { root = JsonNode.Parse(json) as JsonObject; }
        catch { return json; } // JSON illisible : laisse Deserialize echouer comme avant (catch-all de Load).

        if (root is null) return json;

        var version = (int?)root["SchemaVersion"] ?? 0; // absent = version 0 (avant ce mecanisme).
        ApplyMigrations(root, version, CurrentSchemaVersion, MigrationSteps);
        return root.ToJsonString();
    }

    private void RemoveLegacyDefaultShortcuts()
    {
        if (NewTabShortcuts.Count != 4) return;

        var legacyDefaults = new[]
        {
            new NewTabShortcut("Accueil", "lumora://accueil"),
            new NewTabShortcut("Google", "https://www.google.com"),
            new NewTabShortcut("YouTube", "https://www.youtube.com"),
            new NewTabShortcut("GitHub", "https://github.com")
        };

        for (var i = 0; i < legacyDefaults.Length; i++)
        {
            if (!string.Equals(NewTabShortcuts[i].Title, legacyDefaults[i].Title, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(NewTabShortcuts[i].Url, legacyDefaults[i].Url, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        NewTabShortcuts.Clear();
        NewTabShortcutsVisible = false;
    }

    // Retour utilisateur du 2026-08-08 (2e session) : la regle "rien
    // d'epingle par defaut, seuls Bloqueur de pub/Coffre/Favoris restent
    // visibles" avait ete appliquee a PinnedStartMenuTileIds (ci-dessus)
    // mais pas ici - un profil migre depuis un ancien reglage recevait
    // encore 5 modules pre-epingles d'office, contredisant la regle deja
    // en place ailleurs. Alignee : plus aucun pre-epinglage, meme logique
    // que le champ (liste vide par defaut pour un profil neuf, ligne 208).
    private void ApplyPinnedModulesMigration(string json)
    {
        if (HasJsonProperty(json, nameof(PinnedModuleIds))) return;

        PinnedModuleIds = [];
    }

    private static bool HasJsonProperty(string json, string propertyName)
    {
        try
        {
            return JsonNode.Parse(json) is JsonObject obj && obj.ContainsKey(propertyName);
        }
        catch
        {
            return true;
        }
    }

    private void ApplyLegacyBrandingMigration()
    {
        NewTabTitle = BrandingText.NormalizeLegacyProductTitle(NewTabTitle);
    }
}

internal sealed record NewTabShortcut(string Title, string Url);
