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
        _suppressUiSettingsSave = false;
        UpdateWizardStep();
        SetupWizardOverlay.Visibility = Visibility.Visible;
    }

    private void UpdateWizardStep()
    {
        WizardStepIndicator.Text = $"Etape {_wizardStep + 1} / 3";
        WizardStep0.Visibility = _wizardStep == 0 ? Visibility.Visible : Visibility.Collapsed;
        WizardStep1.Visibility = _wizardStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        WizardStep2.Visibility = _wizardStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        WizardPrevButton.IsEnabled = _wizardStep > 0;
        WizardNextButton.Content = _wizardStep == 2 ? "Terminer" : "Suivant";

        if (_wizardStep == 2)
        {
            WizardSummarySearch.Text = "Moteur de recherche : " + _uiSettings.SearchEngine switch
            {
                "duckduckgo" => "DuckDuckGo",
                "brave"      => "Brave Search",
                "bing"       => "Bing",
                _            => "Google",
            };
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
        if (_wizardStep < 2)
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

    private void FinishWizard()
    {
        _uiSettings.SetupWizardCompleted = true;
        _uiSettings.Save(_profile.UiSettingsFile);
        SetupWizardOverlay.Visibility = Visibility.Collapsed;
    }
}
