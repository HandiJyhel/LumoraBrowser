using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Lumora.WinUI;

// Vision : "Memoriser le zoom par site" (chantier "Ajustement n2",
// 2026-09-11, maquette Artifact "Lumora Sur-Mesure"). Ajoute Ctrl+Plus/
// Ctrl+Moins/Ctrl+0 - absents de Lumora jusqu'ici, la SEULE facon de zoomer
// une page etait d'ouvrir le Controle de site et de choisir un pourcentage
// dans un menu deroulant. Plutot qu'un mecanisme parallele (voir
// Accessibility/ZoomStepPolicy.cs pour le detail technique), ces raccourcis
// pilotent directement SiteComfortPolicy - la meme memoire par domaine que
// ce menu deroulant, deja fiable et deja branchee sur la navigation
// (MainWindow.SiteComfort.cs). "Chaque site rouvre au niveau de zoom ou il a
// ete laisse" est donc vrai des l'activation, sans nouvelle donnee a
// persister.
public sealed partial class MainWindow
{
    private void RegisterAccessibilityZoomShortcuts()
    {
        RegisterGlobalAccelerator((VirtualKey)187 /* OEM Plus, "+"/"=" */, VirtualKeyModifiers.Control,
            () => StepSiteZoom(zoomIn: true));
        RegisterGlobalAccelerator(VirtualKey.Add, VirtualKeyModifiers.Control,
            () => StepSiteZoom(zoomIn: true));
        RegisterGlobalAccelerator((VirtualKey)189 /* OEM Minus, "-" */, VirtualKeyModifiers.Control,
            () => StepSiteZoom(zoomIn: false));
        RegisterGlobalAccelerator(VirtualKey.Subtract, VirtualKeyModifiers.Control,
            () => StepSiteZoom(zoomIn: false));
        RegisterGlobalAccelerator(VirtualKey.Number0, VirtualKeyModifiers.Control,
            ResetSiteZoom);
    }

    private void StepSiteZoom(bool zoomIn)
    {
        if (!_uiSettings.AccessibilityZoomPerSiteEnabled) return;
        if (CurrentSite() is not { } site) return;

        var current = SiteComfortPolicy.ZoomPercentFor(_uiSettings.SiteComfortRules, site.RootDomain);
        var next = zoomIn
            ? Accessibility.ZoomStepPolicy.StepUp(current)
            : Accessibility.ZoomStepPolicy.StepDown(current);
        ApplySiteZoomPercent(site, next);
    }

    private void ResetSiteZoom()
    {
        if (!_uiSettings.AccessibilityZoomPerSiteEnabled) return;
        if (CurrentSite() is not { } site) return;

        ApplySiteZoomPercent(site, Accessibility.ZoomStepPolicy.DefaultPercent);
    }

    // Meme sequence que SiteControlComfortZoomCombo_SelectionChanged
    // (MainWindow.SiteComfort.cs) : une seule autorite pour "quel zoom ce
    // domaine memorise-t-il", que ce soit le menu deroulant ou le clavier
    // qui la declenche.
    private void ApplySiteZoomPercent(CurrentSiteInfo site, int zoomPercent)
    {
        SiteComfortPolicy.SetZoomPercent(_uiSettings.SiteComfortRules, site.RootDomain, zoomPercent);
        SaveUiSettings();
        RefreshSiteComfortUi(site.RootDomain);
        _ = ApplySiteComfortAsync(CurrentTab()?.View, site.Address);
        StatusText.Text = zoomPercent == SiteComfortPolicy.DefaultZoomPercent
            ? $"{site.RootDomain} : zoom réinitialisé à 100 %."
            : $"{site.RootDomain} : zoom {zoomPercent} % (mémorisé pour ce site).";
    }
}
