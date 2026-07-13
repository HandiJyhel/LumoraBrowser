namespace Lumora.Privacy;

internal interface IPrivacyModule
{
    string Id { get; }
    string DisplayName { get; }
    bool IsEnabled { get; set; }

    // Returns true to block the network request entirely
    bool ShouldBlock(string requestUri, string pageUri) => false;

    // Returns the cleaned URL if it was modified, null if no change needed
    string? CleanUrl(string uri) => null;
}
