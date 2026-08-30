using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Windows.Foundation;

namespace Lumora.WinUI;

// Menu contextuel (clic droit) des pages web (chantier identite visuelle,
// 2026-08-10 - demande explicite utilisateur : "je veux qu'il soit pareil
// sur les pages web"). Le menu natif Chromium/WebView2 n'est pas stylisable
// (rendu par le moteur, hors de portee de XAML) : on le supprime
// (args.Handled = true) et on reconstruit NOTRE PROPRE menu a partir de
// args.MenuItems - la vraie liste contextuelle fournie par Chromium (lien,
// image, selection...), executee ensuite via args.SelectedCommandId. Aucune
// logique de clic droit reinventee a la main : seule la presentation change.
public sealed partial class MainWindow
{
    private void CoreWebView2_ContextMenuRequested(BrowserTabState tab, CoreWebView2ContextMenuRequestedEventArgs args)
    {
        if (!_tabs.Contains(tab) || tab.View is not { } view)
        {
            return;
        }

        args.Handled = true;
        var deferral = args.GetDeferral();

        // Pas de FlyoutPresenterStyle a poser ici : MenuFlyout n'expose pas
        // cette propriete (contrairement a Flyout) - la forme galbee vient
        // du style implicite TargetType="MenuFlyoutPresenter" (MainWindow.xaml).
        var flyout = new MenuFlyout();
        var completed = false;
        void Complete()
        {
            if (completed)
            {
                return;
            }
            completed = true;
            deferral.Complete();
        }

        var sawSeparator = false;
        foreach (var item in args.MenuItems)
        {
            var element = BuildContextMenuItem(item, args, Complete, ref sawSeparator);
            if (element is not null)
            {
                flyout.Items.Add(element);
            }
        }

        flyout.Closed += (_, _) => Complete();

        var point = new Point(args.Location.X, args.Location.Y);
        flyout.ShowAt(view, new FlyoutShowOptions { Position = point });
    }

    private MenuFlyoutItemBase? BuildContextMenuItem(
        CoreWebView2ContextMenuItem item,
        CoreWebView2ContextMenuRequestedEventArgs args,
        Action complete,
        ref bool sawSeparator)
    {
        switch (item.Kind)
        {
            case CoreWebView2ContextMenuItemKind.Separator:
                {
                    var sep = new MenuFlyoutSeparator();
                    // Premier separateur "signature" : degrade Lumora plutot
                    // qu'un trait neutre - un seul, pour ne pas diluer l'effet
                    // (meme principe que la maquette validee).
                    if (!sawSeparator)
                    {
                        sawSeparator = true;
                        sep.Background = (Brush)RootShell.Resources["NovaIdentityMarkBrush"];
                    }
                    return sep;
                }

            case CoreWebView2ContextMenuItemKind.Submenu:
                {
                    var sub = new MenuFlyoutSubItem { Text = item.Label, IsEnabled = item.IsEnabled };
                    var sawSeparatorInSub = false;
                    foreach (var child in item.Children)
                    {
                        var childElement = BuildContextMenuItem(child, args, complete, ref sawSeparatorInSub);
                        if (childElement is not null)
                        {
                            sub.Items.Add(childElement);
                        }
                    }
                    return sub;
                }

            case CoreWebView2ContextMenuItemKind.CheckBox:
            case CoreWebView2ContextMenuItemKind.Radio:
                {
                    var toggle = new ToggleMenuFlyoutItem
                    {
                        Text = item.Label,
                        IsEnabled = item.IsEnabled,
                        IsChecked = item.IsChecked,
                    };
                    toggle.Click += (_, _) =>
                    {
                        args.SelectedCommandId = item.CommandId;
                        complete();
                    };
                    return toggle;
                }

            default: // Command
                {
                    var mfi = new MenuFlyoutItem { Text = item.Label, IsEnabled = item.IsEnabled };
                    mfi.Click += (_, _) =>
                    {
                        args.SelectedCommandId = item.CommandId;
                        complete();
                    };
                    return mfi;
                }
        }
    }
}
