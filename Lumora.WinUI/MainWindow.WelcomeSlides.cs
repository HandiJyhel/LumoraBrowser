using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;

namespace Lumora.WinUI;

// Slides de bienvenue montrees avant meme la creation du premier profil (voir
// InitializeLoginOverlayAsync dans MainWindow.Profile.cs), et rejouables a la
// demande depuis Parametres > Mon Lumora ("Revoir l'ecran de bienvenue").
// Le flag global LumoraConfig.WelcomeSlidesShown (pas UiSettings, qui est par
// profil : ce moment se produit avant qu'aucun profil n'existe) memorise que
// la version "premier lancement" a deja ete vue.
public sealed partial class MainWindow
{
    private const int WelcomeSlideCount = 5;
    private int _welcomeStep;
    private bool _welcomeIsReplay;
    private Storyboard? _welcomeCurrentPulse;

    private static readonly string[] WelcomePulseKeys =
    {
        "Welcome0Pulse", "Welcome1Pulse", "Welcome2Pulse", "Welcome3Pulse", "Welcome4Pulse",
    };

    private StackPanel[] WelcomeSteps => new[] { WelcomeStep0, WelcomeStep1, WelcomeStep2, WelcomeStep3, WelcomeStep4 };
    private Rectangle[] WelcomeDots => new[] { WelcomeDot0, WelcomeDot1, WelcomeDot2, WelcomeDot3, WelcomeDot4 };

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
        ((Storyboard)WelcomeOverlay.Resources["WelcomeAmbienceStoryboard"]).Begin();
    }

    private void UpdateWelcomeStep()
    {
        // AutomationProperties.Name (pas le Text, garde volontairement invisible/
        // quasi transparent a l'ecran) : seul signal donne au lecteur d'ecran, les
        // pilules de progression n'en portent aucun a elles seules.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            WelcomeStepIndicator, $"Étape {_welcomeStep + 1} sur {WelcomeSlideCount}");

        var steps = WelcomeSteps;
        for (var i = 0; i < steps.Length; i++)
        {
            steps[i].Visibility = i == _welcomeStep ? Visibility.Visible : Visibility.Collapsed;
        }

        var dots = WelcomeDots;
        for (var i = 0; i < dots.Length; i++)
        {
            var isCurrent = i == _welcomeStep;
            dots[i].Width = isCurrent ? 20 : 8;
            dots[i].Fill = isCurrent
                ? (Microsoft.UI.Xaml.Media.Brush)RootShell.Resources["NovaAccentBrush"]
                : (Microsoft.UI.Xaml.Media.Brush)RootShell.Resources["NovaChromeStrokeBrush"];
        }

        WelcomePrevButton.IsEnabled = _welcomeStep > 0;
        WelcomeNextButton.Content = _welcomeStep == WelcomeSlideCount - 1 ? "Commencer" : "Suivant";

        // Halo + anneau de l'icone en cours "respirent" doucement tant que la
        // diapositive reste affichee - une seule animation active a la fois.
        _welcomeCurrentPulse?.Stop();
        _welcomeCurrentPulse = (Storyboard)WelcomeOverlay.Resources[WelcomePulseKeys[_welcomeStep]];
        _welcomeCurrentPulse.Begin();
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
        ((Storyboard)WelcomeOverlay.Resources["WelcomeAmbienceStoryboard"]).Stop();
        _welcomeCurrentPulse?.Stop();
        _welcomeCurrentPulse = null;

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
