using PulseBrowser.WinUI;
using Xunit;

namespace PulseBrowser.Tests;

public sealed class DownloadHistoryTests
{
    [Fact]
    public void SourceDomainFor_Extracts_Host()
    {
        Assert.Equal("example.com", DownloadHistoryEntry.SourceDomainFor("https://example.com/file.zip"));
    }

    [Fact]
    public void Upsert_Replaces_Existing_Entry()
    {
        var path = Path.Combine(Path.GetTempPath(), "pulse-downloads-" + Guid.NewGuid() + ".pulse");
        try
        {
            var store = new DownloadHistoryStore(path);
            var started = Entry("same-id", DownloadHistoryEntry.InProgress);
            var completed = started.WithProgress(10, 10).WithState(DownloadHistoryEntry.Completed);

            store.Upsert(started);
            store.Upsert(completed);

            var entry = Assert.Single(store.AllEntries());
            Assert.Equal(DownloadHistoryEntry.Completed, entry.State);
            Assert.Equal(10, entry.ReceivedBytes);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Store_Reloads_Persisted_Entries()
    {
        var path = Path.Combine(Path.GetTempPath(), "pulse-downloads-" + Guid.NewGuid() + ".pulse");
        try
        {
            var store = new DownloadHistoryStore(path);
            store.Upsert(Entry("persisted-id", DownloadHistoryEntry.Completed));

            var reloaded = new DownloadHistoryStore(path);

            var entry = Assert.Single(reloaded.AllEntries());
            Assert.Equal("persisted-id", entry.Id);
            Assert.Equal("file.zip", entry.FileName);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Guest_Mode_Does_Not_Persist_New_Entries()
    {
        var path = Path.Combine(Path.GetTempPath(), "pulse-downloads-" + Guid.NewGuid() + ".pulse");
        try
        {
            var store = new DownloadHistoryStore(path);
            store.SetGuestMode(true);
            store.Upsert(Entry("guest-id", DownloadHistoryEntry.Completed));

            var reloaded = new DownloadHistoryStore(path);

            Assert.Empty(reloaded.AllEntries());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static DownloadHistoryEntry Entry(string id, string state) =>
        new(
            id,
            "file.zip",
            "https://example.com/file.zip",
            "example.com",
            Path.Combine(Path.GetTempPath(), "file.zip"),
            0,
            10,
            state,
            DateTimeOffset.Now,
            null);
}
