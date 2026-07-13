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
    bool IsUpdate);
