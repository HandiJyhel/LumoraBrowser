using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace Lumora.WinUI;

// Point de lumiere du bouclier : chaque fois qu'un traceur est reellement
// bloque, un tout petit point sort du bouclier de la barre d'outils, derive
// un peu et s'eteint en ~3 secondes. La seule touche de "feerie" de l'interface,
// volontairement discrete. Jamais au-dessus du contenu d'un site (le moteur web
// passe devant les elements XAML) : il vit uniquement dans la barre d'outils.
public sealed partial class MainWindow
{
    // Au plus un point toutes les 700 ms : une page peut bloquer des dizaines
    // de requetes d'un coup, le compteur (badge) reste exact, pas le point.
    private static readonly TimeSpan ShieldFireflyMinInterval = TimeSpan.FromMilliseconds(700);

    private int _shieldFireflyLastTotal;
    private DateTime _shieldFireflyLastPlayUtc = DateTime.MinValue;
    private Storyboard? _shieldFireflyStoryboard;
    private DispatcherTimer? _shieldFireflyStaticTimer;

    private void PlayShieldFireflyIfBlocked(int pageTotal)
    {
        var increased = pageTotal > _shieldFireflyLastTotal;
        _shieldFireflyLastTotal = pageTotal;
        if (!increased || ShieldFireflyHost is null) return;
        if (_uiSettings.AccessibilityHighContrast) return;

        var now = DateTime.UtcNow;
        if (now - _shieldFireflyLastPlayUtc < ShieldFireflyMinInterval) return;
        _shieldFireflyLastPlayUtc = now;

        try
        {
            if (_uiSettings.AccessibilityReduceMotion)
            {
                PlayShieldFireflyStatic();
                return;
            }

            PlayShieldFireflyMoving();
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"ShieldFirefly: {error.GetType().Name}");
        }
    }

    // Reduire les animations : un point fixe pendant ~2,5 s, sans deplacement.
    private void PlayShieldFireflyStatic()
    {
        _shieldFireflyStoryboard?.Stop();
        ShieldFireflyShift.X = 0;
        ShieldFireflyShift.Y = 8;
        ShieldFireflyHost.Opacity = 1;

        _shieldFireflyStaticTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2500) };
        _shieldFireflyStaticTimer.Stop();
        _shieldFireflyStaticTimer.Tick -= ShieldFireflyStaticTimer_Tick;
        _shieldFireflyStaticTimer.Tick += ShieldFireflyStaticTimer_Tick;
        _shieldFireflyStaticTimer.Start();
    }

    private void ShieldFireflyStaticTimer_Tick(object? sender, object e)
    {
        _shieldFireflyStaticTimer?.Stop();
        ShieldFireflyHost.Opacity = 0;
    }

    private void PlayShieldFireflyMoving()
    {
        _shieldFireflyStaticTimer?.Stop();
        _shieldFireflyStoryboard?.Stop();

        var duration = TimeSpan.FromMilliseconds(3200);
        var storyboard = new Storyboard();

        var opacity = new DoubleAnimationUsingKeyFrames { Duration = duration };
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 0 });
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(450), Value = 1 });
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(1450), Value = 0.35 });
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(2250), Value = 0.95 });
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = duration, Value = 0 });
        Storyboard.SetTarget(opacity, ShieldFireflyHost);
        Storyboard.SetTargetProperty(opacity, "Opacity");

        var x = new DoubleAnimation { From = 0, To = 11, Duration = duration, EnableDependentAnimation = true };
        Storyboard.SetTarget(x, ShieldFireflyShift);
        Storyboard.SetTargetProperty(x, "X");

        var y = new DoubleAnimation { From = 4, To = 24, Duration = duration, EnableDependentAnimation = true };
        Storyboard.SetTarget(y, ShieldFireflyShift);
        Storyboard.SetTargetProperty(y, "Y");

        storyboard.Children.Add(opacity);
        storyboard.Children.Add(x);
        storyboard.Children.Add(y);
        storyboard.Completed += (_, _) => ShieldFireflyHost.Opacity = 0;
        _shieldFireflyStoryboard = storyboard;
        storyboard.Begin();
    }
}
