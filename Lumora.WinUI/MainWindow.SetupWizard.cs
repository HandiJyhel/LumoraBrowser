using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private int _wizardStep;

    private void ShowSetupWizard()
    {
        _wizardStep = 0;
        _suppressUiSettingsSave = true;
        WizardSearchGoogle.IsChecked    = _uiSettings.SearchEngine is not ("duckduckgo" or "brave" or "bing");
        WizardSearchDuckDuckGo.IsChecked = _uiSettings.SearchEngine == "duckduckgo";
        WizardSearchBrave.IsChecked     = _uiSettings.SearchEngine == "brave";
        WizardSearchBing.IsChecked      = _uiSettings.SearchEngine == "bing";
        SelectWizardUsageMode(_uiSettings.UsageMode);
        SelectWizardModules();
        _suppressUiSettingsSave = false;
        UpdateWizardStep();
        SetupWizardOverlay.Visibility = Visibility.Visible;
    }

    private void UpdateWizardStep()
    {
        WizardStepIndicator.Text = $"Etape {_wizardStep + 1} / 4";
        WizardStep0.Visibility = _wizardStep == 0 ? Visibility.Visible : Visibility.Collapsed;
        WizardStep1.Visibility = _wizardStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        WizardStep2.Visibility = _wizardStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        WizardStep3.Visibility = _wizardStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        WizardPrevButton.IsEnabled = _wizardStep > 0;
        WizardNextButton.Content = _wizardStep == 3 ? "Terminer" : "Suivant";

        if (_wizardStep == 3)
        {
            WizardSummarySearch.Text = "Moteur de recherche : " + _uiSettings.SearchEngine switch
            {
                "duckduckgo" => "DuckDuckGo",
                "brave"      => "Brave Search",
                "bing"       => "Bing",
                _            => "Google",
            };
            WizardSummaryUsage.Text = "Mode d'usage : " + UsageModeDisplayName(_uiSettings.UsageMode);
            WizardSummaryModules.Text = _uiSettings.PinnedModuleIds.Count == 0
                ? "Modules visibles : aucun module impose"
                : "Modules visibles : " + string.Join(", ", _uiSettings.PinnedModuleIds.Select(ModuleDisplayName));
        }
    }

    private void WizardPrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_wizardStep > 0)
        {
            _wizardStep--;
            UpdateWizardStep();
        }
    }

    private void WizardNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_wizardStep < 3)
        {
            _wizardStep++;
            UpdateWizardStep();
        }
        else
        {
            FinishWizard();
        }
    }

    private void WizardSearch_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        if (sender is RadioButton rb && rb.Tag is string engine)
            _uiSettings.SearchEngine = engine;
    }

    private void WizardUsage_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        if (sender is RadioButton rb && rb.Tag is string usageMode)
        {
            _uiSettings.UsageMode = usageMode;
            if (string.Equals(usageMode, "neutral", StringComparison.OrdinalIgnoreCase))
            {
                _uiSettings.PinnedModuleIds.Clear();
                SelectWizardModules();
            }
        }
    }

    private void WizardModule_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.PinnedModuleIds = WizardModuleCheckboxes()
            .Where(box => box.IsChecked == true)
            .Select(box => box.Tag?.ToString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToList();
    }

    private void SelectWizardUsageMode(string? usageMode)
    {
        var mode = (usageMode ?? "neutral").ToLowerInvariant();
        WizardUsageNeutral.IsChecked = mode == "neutral";
        WizardUsageBalanced.IsChecked = mode == "balanced";
        WizardUsageFocus.IsChecked = mode == "focus";
        WizardUsageReading.IsChecked = mode == "reading";
        WizardUsageCreative.IsChecked = mode == "creative";
        WizardUsageResearch.IsChecked = mode == "research";
        WizardUsageNight.IsChecked = mode == "night";
    }

    private void SelectWizardModules()
    {
        var pinned = _uiSettings.PinnedModuleIds;
        foreach (var box in WizardModuleCheckboxes())
        {
            var moduleId = box.Tag?.ToString() ?? string.Empty;
            box.IsChecked = pinned.Contains(moduleId, StringComparer.OrdinalIgnoreCase);
        }
    }

    private IEnumerable<CheckBox> WizardModuleCheckboxes()
    {
        yield return WizardModuleReader;
        yield return WizardModuleNotes;
        yield return WizardModuleReadAloud;
        yield return WizardModuleVideoDownload;
        yield return WizardModuleSearchAssist;
        yield return WizardModuleTranslate;
        yield return WizardModuleWebApps;
        yield return WizardModuleDictation;
    }

    private void FinishWizard()
    {
        _uiSettings.SetupWizardCompleted = true;
        _uiSettings.Save(_profile.UiSettingsFile);
        SetupWizardOverlay.Visibility = Visibility.Collapsed;
        ApplyUiSettings();
        RefreshNovaHomePages();
    }

    private static string UsageModeDisplayName(string? usageMode) =>
        (usageMode ?? "neutral").ToLowerInvariant() switch
        {
            "neutral" => "Neutre",
            "focus" => "Focus",
            "reading" => "Lecture",
            "creative" => "Creation",
            "research" => "Recherche",
            "night" => "Nuit",
            _ => "Equilibre"
        };

    private static string ModuleDisplayName(string moduleId) =>
        moduleId switch
        {
            "reader" => "lecture",
            "notes" => "notes",
            "readAloud" => "lecture vocale",
            "videoDownload" => "telechargement video",
            "searchAssist" => "assistant recherche",
            "translate" => "traduction",
            "webApps" => "applications web",
            "dictation" => "dictee",
            "detachVideo" => "video detachee",
            _ => moduleId
        };
}
