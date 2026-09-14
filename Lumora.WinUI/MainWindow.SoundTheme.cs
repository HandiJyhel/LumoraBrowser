using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Lumora.WinUI;

// Glue UI <-> Sound.SoundThemeService pour "Sons & ambiance" (chantier
// "Ajustement n2", 2026-09-11, demande explicite utilisateur). La logique de
// lecture audio elle-meme vit dans Sound/SoundThemeService.cs (nouveau
// fichier, demande explicite pour ne pas alourdir davantage ce fichier) -
// ici, uniquement : lire/ecrire UiSettings, synchroniser les DEUX jeux de
// controles (carte Reglages > Profils locaux, et mini-lecteur de la barre
// du bas), et repercuter vers le service.
public sealed partial class MainWindow
{
    private void InitializeSoundThemeService()
    {
        _soundThemeService.AttachTo(RootShell);
        // Choisir un pack dans la carte des Reglages ne doit pas sonoriser
        // avec l'ANCIEN pack via l'ecoute automatique (voir SoundThemeService)
        // - ApplySoundEffectsPackChange appelle PreviewClick explicitement
        // avec le pack a jour a la place.
        _soundThemeService.ExcludeFromAutoClickSound(SoundThemeSettingsCard);
        RunSoundSelfTestIfRequested();
    }

    // Diagnostic reel sans dependre du pilotage automatise de l'UI, qui a
    // echoue de facon repetee cette session (2026-09-11) : avec
    // LUMORA_SOUND_SELFTEST=1, declenche une sequence de lectures directes
    // (pas de clic simule) et trace le resultat reel de chaque MediaPlayer
    // dans winui-runtime-trace.log - voir les evenements cables dans
    // SoundThemeService (constructeur).
    private void RunSoundSelfTestIfRequested()
    {
        if (Environment.GetEnvironmentVariable("LUMORA_SOUND_SELFTEST") != "1")
        {
            return;
        }

        WinUiRuntimeTrace.Write("SoundSelfTest: start");
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        var step = 0;
        timer.Tick += (_, _) =>
        {
            step++;
            switch (step)
            {
                case 1:
                    WinUiRuntimeTrace.Write("SoundSelfTest: step1 pack=base PreviewClick");
                    _soundThemeService.SetEffectsPack("base");
                    _soundThemeService.PreviewClick();
                    break;
                case 2:
                    WinUiRuntimeTrace.Write("SoundSelfTest: step2 pack=arcade PreviewClick");
                    _soundThemeService.SetEffectsPack("arcade");
                    _soundThemeService.PreviewClick();
                    break;
                case 3:
                    WinUiRuntimeTrace.Write("SoundSelfTest: step3 PreviewToggle");
                    _soundThemeService.PreviewToggle();
                    break;
                case 4:
                    WinUiRuntimeTrace.Write("SoundSelfTest: step4 SetEnabled(true) + ambiance cascade");
                    _soundThemeService.SetEnabled(true);
                    _soundThemeService.SetAmbiance("cascade", 0.35);
                    break;
                case 5:
                    // Teste le VRAI chemin de detection (garde Enabled +
                    // remontee d'arbre), pas juste PreviewClick qui le
                    // court-circuite - avec un vrai bouton de l'app,
                    // AccessibilityMenuButton (pied de page, toujours present
                    // que le panneau soit ouvert ou non).
                    WinUiRuntimeTrace.Write("SoundSelfTest: step5 SimulateInteractionForSelfTest(AccessibilityMenuButton), Enabled devrait etre true");
                    _soundThemeService.SimulateInteractionForSelfTest(AccessibilityMenuButton);
                    break;
                case 6:
                    WinUiRuntimeTrace.Write("SoundSelfTest: step6 SetEnabled(false) puis meme simulation - devrait rester silencieux");
                    _soundThemeService.SetEnabled(false);
                    _soundThemeService.SimulateInteractionForSelfTest(AccessibilityMenuButton);
                    break;
                default:
                    WinUiRuntimeTrace.Write("SoundSelfTest: done");
                    timer.Stop();
                    break;
            }
        };
        timer.Start();
    }

    private void LoadSoundThemeSettings()
    {
        _soundThemeService.SetEffectsPack(_uiSettings.SoundEffectsPack);
        _soundThemeService.SetAmbiance(_uiSettings.SoundAmbianceId, _uiSettings.SoundAmbianceVolume);
        _soundThemeService.SetEnabled(_uiSettings.SoundThemeEnabled);
        RefreshSoundThemeUi();
        UpdateElementSoundMuteState();
        WinUiRuntimeTrace.Write($"LoadSoundThemeSettings: enabled={_uiSettings.SoundThemeEnabled} pack={_uiSettings.SoundEffectsPack} ambiance={_uiSettings.SoundAmbianceId} volume={_uiSettings.SoundAmbianceVolume:F2} serviceEnabled={_soundThemeService.Enabled}");
    }

    private void SaveSoundThemeSettings() => _uiSettings.Save(_profile.UiSettingsFile);

    // Point central pour couper/restaurer les sons systeme WinUI par defaut
    // (2026-09-11, correction) : DEUX reglages veulent chacun controler
    // ElementSoundPlayer (accessibilite "Sons -> flash visuel" ET "Sons &
    // ambiance" ici) - sans coordination, activer l'un pouvait ecraser le
    // choix de l'autre, ou "Sons & ambiance" desactive laissait quand meme
    // entendre le son systeme par defaut a chaque clic (retour utilisateur
    // reel : "les bruits sont desactives mais il y a quand meme le bruit du
    // clic"). Coupe si L'UN OU L'AUTRE est actif ; restaure sinon. A
    // rappeler apres CHAQUE changement de l'un des deux reglages, dans les
    // deux sens (active <-> desactive).
    private void UpdateElementSoundMuteState()
    {
        var mute = _uiSettings.SoundThemeEnabled || _uiSettings.AccessibilitySoundsAsVisualFlashEnabled;
        ElementSoundPlayer.State = mute ? ElementSoundPlayerState.Off : ElementSoundPlayerState.On;
    }

    // Remet les DEUX jeux de controles (carte + mini-lecteur) d'apres
    // _uiSettings - a appeler apres tout changement, d'ou qu'il vienne, pour
    // qu'ils ne divergent jamais l'un de l'autre.
    private void RefreshSoundThemeUi()
    {
        var wasSuppressed = _suppressUiSettingsSave;
        _suppressUiSettingsSave = true;
        try
        {
            SoundThemeEnabledSwitch.IsOn = _uiSettings.SoundThemeEnabled;
            SoundEffectsPackRadio.IsEnabled = _uiSettings.SoundThemeEnabled;
            SoundAmbianceRadio.IsEnabled = _uiSettings.SoundThemeEnabled;
            SoundAmbianceVolumeSlider.IsEnabled = _uiSettings.SoundThemeEnabled;
            SelectRadioByTag(SoundEffectsPackRadio, _uiSettings.SoundEffectsPack, "base");
            SelectRadioByTag(SoundAmbianceRadio, _uiSettings.SoundAmbianceId, "none");
            SoundAmbianceVolumeSlider.Value = _uiSettings.SoundAmbianceVolume * 100;

            SelectRadioByTag(SoundThemeFooterPackRadio, _uiSettings.SoundEffectsPack, "base");
            SelectRadioByTag(SoundThemeFooterAmbianceRadio, _uiSettings.SoundAmbianceId, "none");
            SoundThemeFooterVolumeSlider.Value = _uiSettings.SoundAmbianceVolume * 100;

            UpdateSoundThemeFooterVisibility();
        }
        finally
        {
            _suppressUiSettingsSave = wasSuppressed;
        }
    }

    // N'existe QUE si l'option est active (demande explicite : pas grise,
    // absent) - jamais un simple IsEnabled=false laissant le bouton visible.
    private void UpdateSoundThemeFooterVisibility()
    {
        SoundThemeFooterButton.Visibility = _uiSettings.SoundThemeEnabled ? Visibility.Visible : Visibility.Collapsed;
        SoundThemeFooterLabel.Text = _uiSettings.SoundAmbianceId switch
        {
            "cascade" => "Cascade",
            "mer" => "Mer",
            "pluie" => "Pluie douce",
            _ => "Aucune ambiance",
        };
    }

    private void SoundThemeEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        WinUiRuntimeTrace.Write($"SoundThemeEnabledSwitch_Toggled: IsOn={SoundThemeEnabledSwitch.IsOn} suppressed={_suppressUiSettingsSave}");
        if (_suppressUiSettingsSave) return;
        _uiSettings.SoundThemeEnabled = SoundThemeEnabledSwitch.IsOn;
        SoundEffectsPackRadio.IsEnabled = _uiSettings.SoundThemeEnabled;
        SoundAmbianceRadio.IsEnabled = _uiSettings.SoundThemeEnabled;
        SoundAmbianceVolumeSlider.IsEnabled = _uiSettings.SoundThemeEnabled;
        _soundThemeService.SetEnabled(_uiSettings.SoundThemeEnabled);
        UpdateSoundThemeFooterVisibility();
        UpdateElementSoundMuteState();
        SaveSoundThemeSettings();
        WinUiRuntimeTrace.Write($"SoundThemeEnabledSwitch_Toggled: saved enabled={_uiSettings.SoundThemeEnabled} serviceEnabled={_soundThemeService.Enabled}");
        UpdateStatusText(_uiSettings.SoundThemeEnabled
            ? "Sons et ambiance activés."
            : "Sons et ambiance désactivés.");
    }

    private void SoundEffectsPackRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        ApplySoundEffectsPackChange(SoundEffectsPackRadio);
    }

    private void SoundThemeFooterPackRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        ApplySoundEffectsPackChange(SoundThemeFooterPackRadio);
    }

    private void ApplySoundEffectsPackChange(RadioButtons source)
    {
        if (source.SelectedItem is not RadioButton rb || rb.Tag is not string tag) return;
        _uiSettings.SoundEffectsPack = tag;
        _soundThemeService.SetEffectsPack(tag);
        RefreshSoundThemeUi();
        SaveSoundThemeSettings();
        // Retour immediat avec le BON pack (voir ExcludeFromAutoClickSound) -
        // sans ca l'ecoute automatique aurait deja joue l'ancien pack au
        // moment meme du clic, donnant l'impression que rien ne change.
        _soundThemeService.PreviewClick();
    }

    private void SoundAmbianceRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        ApplySoundAmbianceChange(SoundAmbianceRadio);
    }

    private void SoundThemeFooterAmbianceRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        ApplySoundAmbianceChange(SoundThemeFooterAmbianceRadio);
    }

    private void ApplySoundAmbianceChange(RadioButtons source)
    {
        if (source.SelectedItem is not RadioButton rb || rb.Tag is not string tag) return;
        _uiSettings.SoundAmbianceId = tag;
        _soundThemeService.SetAmbiance(tag, _uiSettings.SoundAmbianceVolume);
        RefreshSoundThemeUi();
        SaveSoundThemeSettings();
    }

    private void SoundAmbianceVolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        ApplySoundAmbianceVolumeChange(e.NewValue);
    }

    private void SoundThemeFooterVolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        ApplySoundAmbianceVolumeChange(e.NewValue);
    }

    private void ApplySoundAmbianceVolumeChange(double sliderValue)
    {
        _uiSettings.SoundAmbianceVolume = Math.Clamp(sliderValue / 100.0, 0.0, 1.0);
        _soundThemeService.SetAmbianceVolume(_uiSettings.SoundAmbianceVolume);
        RefreshSoundThemeUi();
        SaveSoundThemeSettings();
    }
}
