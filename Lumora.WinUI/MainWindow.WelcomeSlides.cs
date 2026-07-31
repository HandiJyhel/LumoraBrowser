using Microsoft.UI.Xaml;

namespace Lumora.WinUI;

// Slides de bienvenue montrees avant meme la creation du premier profil (voir
// InitializeLoginOverlayAsync dans MainWindow.Profile.cs), et rejouables a la
// demande depuis Parametres > Mon Lumora ("Revoir l'ecran de bienvenue").
// Le flag global LumoraConfig.WelcomeSlidesShown (pas UiSettings, qui est par
// profil : ce moment se produit avant qu'aucun profil n'existe) memorise que
// la version "premier lancement" a deja ete vue.
public sealed partial class MainWindow
{
    private const int WelcomeSlideCount = 4;
    private int _welcomeStep;
    private bool _welcomeIsReplay;

    // isReplay=false : premier lancement reel, la fin des slides enchaine sur
    // la creation du profil (ShowLoginPanel("create") + ShowLoginOverlayChrome()).
    // isReplay=true : ouverte a la demande depuis Parametres, la fin des
    // slides referme simplement l'overlay sans toucher au profil/a la session
    // en cours.
    private void ShowWelcomeSlides(bool isReplay)
    {
        _welcomeStep = 0;
        _welcomeIsReplay = isReplay;
        UpdateWelcomeStep();
        WelcomeOverlay.Visibility = Visibility.Visible;
        WelcomeOverlay.Focus(FocusState.Programmatic);
    }

    private void UpdateWelcomeStep()
    {
        WelcomeStepIndicator.Text = $"{_welcomeStep + 1} / {WelcomeSlideCount}";
        WelcomeStep0.Visibility = _welcomeStep == 0 ? Visibility.Visible : Visibility.Collapsed;
        WelcomeStep1.Visibility = _welcomeStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        WelcomeStep2.Visibility = _welcomeStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        WelcomeStep3.Visibility = _welcomeStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        WelcomePrevButton.IsEnabled = _welcomeStep > 0;
        WelcomeNextButton.Content = _welcomeStep == WelcomeSlideCount - 1 ? "Commencer" : "Suivant";
    }

    private void WelcomePrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_welcomeStep > 0)
        {
            _welcomeStep--;
            UpdateWelcomeStep();
        }
    }

    private void WelcomeNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_welcomeStep < WelcomeSlideCount - 1)
        {
            _welcomeStep++;
            UpdateWelcomeStep();
        }
        else
        {
            CompleteWelcomeSlides();
        }
    }

    private void WelcomeSkipButton_Click(object sender, RoutedEventArgs e)
    {
        CompleteWelcomeSlides();
    }

    // Parametres > Mon Lumora : rejoue les memes slides a la demande, sans
    // toucher au profil ni a la session en cours (isReplay=true).
    private void ReplayWelcomeSlidesButton_Click(object sender, RoutedEventArgs e)
    {
        ShowWelcomeSlides(isReplay: true);
    }

    private void CompleteWelcomeSlides()
    {
        WelcomeOverlay.Visibility = Visibility.Collapsed;

        if (_welcomeIsReplay)
        {
            return;
        }

        var config = LumoraConfig.Load();
        if (!config.WelcomeSlidesShown)
        {
            config.WelcomeSlidesShown = true;
            config.Save();
        }

        ShowLoginPanel("create");
        ShowLoginOverlayChrome();
    }
}
