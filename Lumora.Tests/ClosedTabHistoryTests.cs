using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public sealed class ClosedTabHistoryTests
{
    private static ClosedTabRecord Record(string address, string title = "Titre") =>
        new(title, address, IconPath: string.Empty, GroupId: null, Pinned: false);

    [Fact]
    public void Pop_ReturnsMostRecentlyClosedFirst()
    {
        var history = new ClosedTabHistory();
        history.Push(Record("https://a.example"));
        history.Push(Record("https://b.example"));

        Assert.Equal("https://b.example", history.Pop()!.Address);
        Assert.Equal("https://a.example", history.Pop()!.Address);
        Assert.Null(history.Pop());
    }

    [Fact]
    public void Push_IgnoresHomeTabs()
    {
        var history = new ClosedTabHistory();
        history.Push(Record("lumora://accueil"));
        history.Push(Record("LUMORA://ACCUEIL"));
        history.Push(Record(""));
        history.Push(Record("   "));

        Assert.Empty(history.Items);
    }

    [Fact]
    public void Push_KeepsAtMostCapacityEntries()
    {
        var history = new ClosedTabHistory();
        for (var i = 0; i < ClosedTabHistory.Capacity + 5; i++)
        {
            history.Push(Record($"https://site{i}.example"));
        }

        Assert.Equal(ClosedTabHistory.Capacity, history.Items.Count);
        // Les plus anciens sont évincés : le plus récent reste en tête.
        Assert.Equal($"https://site{ClosedTabHistory.Capacity + 4}.example", history.Items[0].Address);
    }

    [Fact]
    public void Remove_TakesOutASpecificEntry()
    {
        var history = new ClosedTabHistory();
        var middle = Record("https://milieu.example");
        history.Push(Record("https://a.example"));
        history.Push(middle);
        history.Push(Record("https://b.example"));

        Assert.True(history.Remove(middle));
        Assert.False(history.Remove(middle));
        Assert.Equal(2, history.Items.Count);
        Assert.DoesNotContain(middle, history.Items);
    }

    [Fact]
    public void Clear_EmptiesTheHistory()
    {
        var history = new ClosedTabHistory();
        history.Push(Record("https://a.example"));

        history.Clear();

        Assert.Empty(history.Items);
        Assert.Null(history.Pop());
    }

    [Fact]
    public void IsWorthRestoring_OnlyForRealAddresses()
    {
        Assert.True(ClosedTabHistory.IsWorthRestoring("https://exemple.fr"));
        Assert.False(ClosedTabHistory.IsWorthRestoring("lumora://accueil"));
        Assert.False(ClosedTabHistory.IsWorthRestoring(" "));
    }
}
