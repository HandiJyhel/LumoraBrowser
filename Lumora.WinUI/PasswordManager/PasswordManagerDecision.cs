using Lumora.WinUI.Credentials;

namespace Lumora.WinUI.PasswordManager;

internal enum PasswordManagerPromptKind
{
    None,
    FillAvailable,
    UsernameFillAvailable,
    WaitingForPasswordField,
    SuggestNewPassword,
}

internal sealed record PasswordManagerPageDecision(
    PasswordManagerPromptKind Kind,
    IReadOnlyList<VaultCredential> Credentials,
    string Message,
    string Address,
    CredentialPageState? PageState)
{
    public bool CanOfferFill =>
        (Kind == PasswordManagerPromptKind.FillAvailable ||
         Kind == PasswordManagerPromptKind.UsernameFillAvailable) &&
        Credentials.Count > 0;
    public bool HasKnownCredential => Credentials.Count > 0;
}

internal sealed record PasswordManagerSaveOffer(
    PasswordManagerEntryDraft Draft,
    string DisplayOrigin,
    string Username,
    bool IsUpdate,
    // Non nul quand ce meme compte (identifiant + mot de passe) est deja enregistre
    // sous un AUTRE domaine : le site a probablement change de nom de domaine, on
    // propose de rattacher le nouveau domaine au compte existant. Contient le domaine
    // d'affichage ou le compte vit deja (ex. "weareholy.com").
    string? LinkedFromDomain = null);
