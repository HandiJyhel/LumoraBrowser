using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Lumora.WinUI;

// Slides de bienvenue montrees avant meme la creation du premier profil (voir
// InitializeLoginOverlayAsync dans MainWindow.Profile.cs), et rejouables a la
// demande depuis Parametres > Mon Lumora ("Revoir l'ecran de bienvenue").
// Le flag global LumoraConfig.WelcomeSlidesShown (pas UiSettings, qui est par
// profil : ce moment se produit avant qu'aucun profil n'existe) memorise que
// la version "premier lancement" a deja ete vue.
public sealed partial class MainWindow
{
    private const int WelcomeSlideCount = 6;
    private const double WelcomeSlideDistance = 48;
    private const double WelcomeCursorStep = 16;
    private const double WelcomeTransitionMs = 260;

    private int _welcomeStep;
    private bool _welcomeIsReplay;
    private Storyboard? _welcomeCurrentPulse;

    private static readonly string[] WelcomePulseKeys =
    {
        "Welcome0Pulse", "Welcome1Pulse", "Welcome2Pulse", "Welcome3Pulse", "Welcome4Pulse", "Welcome5Pulse",
    };

    private StackPanel[] WelcomeSteps => new[] { WelcomeStep0, WelcomeStep1, WelcomeStep2, WelcomeStep3, WelcomeStep4, WelcomeStep5 };

    private TranslateTransform[] WelcomeStepTransforms => new[]
    {
        (TranslateTransform)WelcomeStep0.RenderTransform,
        (TranslateTransform)WelcomeStep1.RenderTransform,
        (TranslateTransform)WelcomeStep2.RenderTransform,
        (TranslateTransform)WelcomeStep3.RenderTransform,
        (TranslateTransform)WelcomeStep4.RenderTransform,
        (TranslateTransform)WelcomeStep5.RenderTransform,
    };

    // isReplay=false : premier lancement reel, la fin des slides enchaine sur
    // la creation du profil (ShowLoginPanel("create") + ShowLoginOverlayChrome()).
    // isReplay=true : ouverte a la demande depuis Parametres, la fin des
    // slides referme simplement l'overlay sans toucher au profil/a la session
    // en cours.
    private void ShowWelcomeSlides(bool isReplay)
    {
        _welcomeStep = 0;
        _welcomeIsReplay = isReplay;

        var steps = WelcomeSteps;
        var transforms = WelcomeStepTransforms;
        for (var i = 0; i < steps.Length; i++)
        {
            steps[i].Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed;
            steps[i].Opacity = 1;
            transforms[i].X = 0;
        }
        Canvas.SetLeft(WelcomeStepCursor, 0);

        UpdateWelcomeChrome();
        WelcomeOverlay.Visibility = Visibility.Visible;
        WelcomeOverlay.Focus(FocusState.Programmatic);
        ((Storyboard)WelcomeOverlay.Resources["WelcomeAmbienceStoryboard"]).Begin();
        BeginWelcomePulse(0);
    }

    // Retour utilisateur : le changement de diapositive devait vraiment se
    // "voir voyager" (effet page qui glisse), pas sauter d'un bloc. direction
    // +1 = Suivant (la nouvelle slide arrive de la droite, l'ancienne part a
    // gauche), -1 = Precedent (inverse).
    private void GoToWelcomeStep(int newStep, int direction)
    {
        var oldStep = _welcomeStep;
        _welcomeStep = newStep;

        AnimateWelcomeStepTransition(oldStep, newStep, direction);
        AnimateWelcomeCursor(newStep);
        BeginWelcomePulse(newStep);
        UpdateWelcomeChrome();
    }

    private void AnimateWelcomeStepTransition(int oldIndex, int newIndex, int direction)
    {
        var steps = WelcomeSteps;
        var transforms = WelcomeStepTransforms;
        var oldPanel = steps[oldIndex];
        var newPanel = steps[newIndex];
        var oldTransform = transforms[oldIndex];
        var newTransform = transforms[newIndex];

        newPanel.Visibility = Visibility.Visible;
        newPanel.Opacity = 0;
        newTransform.X = direction * WelcomeSlideDistance;

        var storyboard = new Storyboard();
        storyboard.Children.Add(WelcomeOpacityAnimation(oldPanel, 1, 0));
        storyboard.Children.Add(WelcomeTranslateAnimation(oldTransform, 0, -direction * WelcomeSlideDistance));
        storyboard.Children.Add(WelcomeOpacityAnimation(newPanel, 0, 1));
        storyboard.Children.Add(WelcomeTranslateAnimation(newTransform, direction * WelcomeSlideDistance, 0));

        storyboard.Completed += (_, _) =>
        {
            oldPanel.Visibility = Visibility.Collapsed;
            oldPanel.Opacity = 1;
            oldTransform.X = 0;
        };
        storyboard.Begin();
    }

    private void AnimateWelcomeCursor(int step)
    {
        var animation = new DoubleAnimation
        {
            To = step * WelcomeCursorStep,
            Duration = new Duration(TimeSpan.FromMilliseconds(WelcomeTransitionMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(animation, WelcomeStepCursor);
        Storyboard.SetTargetProperty(animation, "(Canvas.Left)");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private static DoubleAnimation WelcomeOpacityAnimation(UIElement target, double from, double to)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(WelcomeTransitionMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");
        return animation;
    }

    private static DoubleAnimation WelcomeTranslateAnimation(TranslateTransform target, double from, double to)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(WelcomeTransitionMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "X");
        return animation;
    }

    // Halo + anneau de l'icone en cours "respirent" doucement tant que la
    // diapositive reste affichee - une seule animation active a la fois.
    private void BeginWelcomePulse(int step)
    {
        _welcomeCurrentPulse?.Stop();
        _welcomeCurrentPulse = (Storyboard)WelcomeOverlay.Resources[WelcomePulseKeys[step]];
        _welcomeCurrentPulse.Begin();
    }

    private void UpdateWelcomeChrome()
    {
        // AutomationProperties.Name (pas le Text, garde volontairement invisible/
        // quasi transparent a l'ecran) : seul signal donne au lecteur d'ecran, le
        // rail de pastilles n'en porte aucun a lui seul.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            WelcomeStepIndicator, $"Étape {_welcomeStep + 1} sur {WelcomeSlideCount}");

        WelcomePrevButton.IsEnabled = _welcomeStep > 0;
        WelcomeNextButton.Content = _welcomeStep == WelcomeSlideCount - 1 ? "Commencer" : "Suivant";
    }

    private void WelcomePrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_welcomeStep > 0)
        {
            GoToWelcomeStep(_welcomeStep - 1, -1);
        }
    }

    private void WelcomeNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_welcomeStep < WelcomeSlideCount - 1)
        {
            GoToWelcomeStep(_welcomeStep + 1, 1);
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
