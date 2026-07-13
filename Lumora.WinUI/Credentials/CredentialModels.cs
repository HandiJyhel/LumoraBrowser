namespace Lumora.WinUI.Credentials;

internal sealed record CredentialCapture(
    string Origin,
    string Username,
    string Password,
    string LoginUrl,
    string Source,
    int Confidence);

internal sealed record CredentialPageState(
    string Origin,
    string LoginUrl,
    bool HasUsernameField,
    bool HasPasswordField,
    string Username,
    string Source,
    int Confidence,
    bool HasEmptyNewPasswordField = false);

internal sealed record CredentialFillResult(bool Success, string Message);
