using Microsoft.UI.Xaml;

namespace Lumora.WinUI;

// Mises a jour (2026-10-24, 0.94.32.0-dev) : volontairement SANS verification
// automatique. Les releases publiques s'appellent toutes "1.0.0" pour l'instant
// (decision utilisateur : micro-ajustements, pas de nouveau numero a annoncer),
// donc il n'y aurait rien a detecter, et un appel reseau vers un service tiers
// a chaque demarrage contredirait la promesse "zero suivi". Le bouton ouvre
// simplement la page des releases du depot dans un onglet, a la demande de
// l'utilisateur - aucune requete ne part tant qu'il ne clique pas. Une
// verification en tache de fond (au plus 1x/jour, interrupteur dans les
// Reglages) pourra se brancher derriere ce meme bouton le jour ou les
// releases seront numerotees (1.0.1...).
public sealed partial class MainWindow
{
    internal const string UpdatesPageUrl = "https://github.com/HandiJyhel/LumoraBrowser/releases";

    private void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        OpenUrlInNewTab(UpdatesPageUrl);
        ShowPanel(BrowserPanel, "Mises à jour de Lumora");
    }
}
