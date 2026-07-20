using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

// Etat minimal d'un onglet Incognito. Contrairement a BrowserTabState (fenetre
// normale) : pas de favicon, pas d'epinglage, pas de groupes, pas de creation
// paresseuse - Incognito reste volontairement minimal (voir commentaire de
// classe de LumoraIncognitoWindow). Item.Tag pointe vers cette instance :
// association directe, pas de lookup par id.
internal sealed class IncognitoTab
{
    public required TabViewItem Item { get; init; }
    public required WebView2 View { get; init; }
    public string Address { get; set; } = string.Empty;
    public string Title { get; set; } = "Nouvel onglet";
}
