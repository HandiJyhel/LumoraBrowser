using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace Lumora.WinUI;

// Diagnostic "gel post-connexion Google" (2026-08-30, session "release qui
// freeze") : apres une connexion Google reussie sur un site tiers (ChatGPT),
// plus moyen de cliquer les boutons de la barre d'outils NI d'utiliser les
// raccourcis clavier (Ctrl+T), alors que le contenu web (deja affiche)
// continuait de repondre normalement au clic. Diagnostic complet (dumps
// memoire + pilotage UIA, voir MEMORY.md) a ecarte deadlock, exception,
// verrous connus et bug de logique - la logique de l'app fonctionne a 100%
// quand elle est declenchee sans passer par un vrai clic/une vraie touche.
//
// Hypothese retenue (NON confirmee par trace live - l'utilisateur a refuse
// de reproduire sur un build Debug, raisonnement juste : un bug de timing
// peut ne jamais se produire en Debug) : aucun gestionnaire n'existait pour
// Window.Activated. Une fenetre transitoire (la popup OAuth Google, une
// boite de dialogue systeme...) qui a eu la main peut repartir sans que
// WinUI ne retrouve un focus clavier coherent sur la fenetre principale -
// les accelerateurs clavier sur Content et le hit-test de la barre d'outils
// en dependent, contrairement au contenu WebView2 qui gere son propre focus
// interne independamment.
public sealed partial class MainWindow
{
    private void RegisterFocusRecoveryOnActivation()
    {
        Activated += MainWindow_Activated;
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        var focused = Content?.XamlRoot is { } xamlRoot ? FocusManager.GetFocusedElement(xamlRoot) : null;
        WinUiRuntimeTrace.Write(
            $"MainWindow_Activated: state={args.WindowActivationState}, focusedElement={(focused is FrameworkElement fe ? fe.Name : focused?.GetType().Name) ?? "null"}");

        if (focused is not null)
        {
            // Un element a deja le focus (barre d'adresse en cours d'edition,
            // bouton, page web...) : ne rien faire, sinon on volerait le focus a
            // une saisie en cours a chaque simple alt-tab.
            return;
        }

        // Plus aucun element ne tient le focus : c'est exactement l'etat
        // suspecte apres la fermeture d'une fenetre transitoire. On le
        // retablit explicitement sur l'onglet actif (ou la racine si aucun
        // moteur n'existe encore) pour que WinUI reparte d'un etat de focus
        // propre.
        if (_browserView is not null)
        {
            _browserView.Focus(FocusState.Programmatic);
        }
        else
        {
            Content?.Focus(FocusState.Programmatic);
        }
    }
}
