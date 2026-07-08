using PulseBrowser.WinUI.Credentials;

namespace PulseBrowser.WinUI.PasswordManager;

internal enum PasswordManagerPromptKind
{
    None,
    FillAvailable,
    UsernameFillAvailable,
    WaitingForPasswordField,
}

internal sealed record PasswordManagerPageDecision(
    PasswordManagerPromptKind Kind,
    VaultCredential? Credential,
    string Message,
    string Address,
    CredentialPageState? PageState)
{
    public bool CanOfferFill =>
        (Kind == PasswordManagerPromptKind.FillAvailable ||
         Kind == PasswordManagerPromptKind.UsernameFillAvailable) &&
        Credential is not null;
    public bool HasKnownCredential => Credential is not null;
}

internal sealed record PasswordManagerSaveOffer(
    PasswordManagerEntryDraft Draft,
    string DisplayOrigin,
    string Username,
    bool IsUpdate);
