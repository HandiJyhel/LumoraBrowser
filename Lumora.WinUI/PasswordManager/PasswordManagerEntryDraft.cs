namespace Lumora.WinUI.PasswordManager;

internal sealed record PasswordManagerEntryDraft(
    string Origin,
    string Username,
    string Password,
    string LoginUrl = "",
    string Label = "");

internal sealed record PasswordManagerSearchOptions(string Query = "");
