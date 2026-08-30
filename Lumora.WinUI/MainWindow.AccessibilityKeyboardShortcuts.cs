using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Lumora.WinUI;

// Raccourcis de navigation de base (nouvel onglet, fermer l'onglet, retour,
// avancer, recharger, barre d'adresse, changer d'onglet, plein ecran) :
// avant ce fichier, seuls Ctrl+K/Ctrl+Shift+N/Ctrl+Shift+T existaient et ces
// actions n'etaient atteignables qu'en tabulant depuis le debut de la
// fenetre a travers tous les onglets ouverts - cout croissant avec le
// nombre d'onglets, ruineux pour un utilisateur clavier seul ou un
// dispositif switch/eye-tracking (audit accessibilite moteur/motricite,
// palier 0.93.x).
public sealed partial class MainWindow
{
    private void RegisterAccessibilityKeyboardShortcuts()
    {
        RegisterGlobalAccelerator(VirtualKey.T, VirtualKeyModifiers.Control,
            () => NewTabMenu_Click(this, new RoutedEventArgs()));
        RegisterGlobalAccelerator(VirtualKey.W, VirtualKeyModifiers.Control,
            CloseCurrentTabAccelerator);
        RegisterGlobalAccelerator(VirtualKey.L, VirtualKeyModifiers.Control,
            FocusAddressBarAccelerator);
        RegisterGlobalAccelerator(VirtualKey.F5, VirtualKeyModifiers.None,
            () => _browserView?.Reload());
        RegisterGlobalAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu,
            () => BackButton_Click(this, new RoutedEventArgs()));
        RegisterGlobalAccelerator(VirtualKey.Right, VirtualKeyModifiers.Menu,
            () => ForwardButton_Click(this, new RoutedEventArgs()));
        RegisterGlobalAccelerator(VirtualKey.Tab, VirtualKeyModifiers.Control,
            () => SwitchToRelativeTab(+1));
        RegisterGlobalAccelerator(VirtualKey.Tab, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            () => SwitchToRelativeTab(-1));
        RegisterGlobalAccelerator(VirtualKey.F11, VirtualKeyModifiers.None,
            ToggleFullScreenMode);
    }

    private void RegisterGlobalAccelerator(VirtualKey key, VirtualKeyModifiers modifiers, Action onInvoked)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += (_, args) =>
        {
            // Instrumentation temporaire (diagnostic gel post-connexion Google,
            // 2026-08-30) : confirme si l'accelerateur est meme declenche, et
            // l'etat des deux overlays qui peuvent l'avaler silencieusement.
            WinUiRuntimeTrace.Write(
                $"Accelerator {modifiers}+{key} invoked, LoginOverlay={LoginOverlay.Visibility}, SetupWizardOverlay={SetupWizardOverlay.Visibility}");

            // Ne pas intercepter pendant la connexion au profil ou l'assistant
            // de creation : ces ecrans ont leur propre logique de clavier.
            if (LoginOverlay.Visibility == Visibility.Visible ||
                SetupWizardOverlay.Visibility == Visibility.Visible)
            {
                return;
            }

            args.Handled = true;
            onInvoked();
        };
        Content.KeyboardAccelerators.Add(accelerator);
    }

    private void CloseCurrentTabAccelerator()
    {
        if (CurrentTab() is { } tab)
        {
            CloseTab(tab);
        }
    }

    private void FocusAddressBarAccelerator()
    {
        AddressBox.Focus(FocusState.Programmatic);
        AddressBox.SelectAll();
    }

    private void SwitchToRelativeTab(int direction)
    {
        if (_tabs.Count == 0) return;

        var current = CurrentTab();
        var index = current is null ? 0 : _tabs.IndexOf(current);
        index = ((index + direction) % _tabs.Count + _tabs.Count) % _tabs.Count;
        SelectTab(_tabs[index]);
    }
}
