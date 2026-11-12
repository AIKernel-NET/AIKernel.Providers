namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneJournalOwnershipTests
{
    [Theory]
    [InlineData(false, "save")]
    [InlineData(false, "load")]
    [InlineData(false, "close")]
    [InlineData(true, "save")]
    [InlineData(true, "load")]
    [InlineData(true, "close")]
    public async Task ClosedHandleCannotAccessOrRemoveSuccessorsJournal(bool cleanClose, string operation)
    {
        await using var f = new SceneFixture(); await f.Service.DisposeAsync();
        using var old = new SceneJournal(f.Directory, 1, 1024); old.Save([]);
        if (cleanClose) old.Close(false); else old.Dispose();
        using var successor = new SceneJournal(f.Directory, 1, 1024); successor.Save([]);
        var before = Snapshot(f); bool enumerated = false;
        IEnumerable<SceneJournalEntry> Entries() { enumerated = true; yield break; }
        var error = Record.Exception(() =>
        {
            if (operation == "save") old.Save(Entries(), confirmRecovery: true);
            else if (operation == "load") old.Load();
            else old.Close(false);
        });
        Assert.Equal(before, Snapshot(f)); Assert.False(enumerated);
        if (operation != "close") AssertUnavailable(error); else Assert.Null(error);
        old.Close(false); old.Dispose(); Assert.Equal(before, Snapshot(f));
        Assert.ThrowsAny<IOException>(() => new SceneJournal(f.Directory, 1, 1024));
    }

    [Fact]
    public async Task ClosingDuringSnapshotEnumerationCannotWriteAfterOwnershipTransfer()
    {
        await using var f = new SceneFixture(); await f.Service.DisposeAsync();
        using var old = new SceneJournal(f.Directory, 1, 1024); old.Save([]);
        SceneJournal? successor = null; string? before = null;
        IEnumerable<SceneJournalEntry> Entries()
        {
            old.Dispose(); successor = new(f.Directory, 1, 1024); successor.Save([]);
            before = Snapshot(f); yield break;
        }
        try
        {
            var error = Record.Exception(() => old.Save(Entries(), confirmRecovery: true));
            AssertUnavailable(error); Assert.Equal(before, Snapshot(f));
            old.Close(false); Assert.Equal(before, Snapshot(f));
            Assert.ThrowsAny<IOException>(() => new SceneJournal(f.Directory, 1, 1024));
        }
        finally { successor?.Dispose(); }
    }

    private static void AssertUnavailable(Exception? error)
    {
        var scene = Assert.IsType<SceneException>(error); Assert.Equal("journal_unavailable", scene.Code);
        Assert.Equal(scene.Code, scene.Message); Assert.Null(scene.InnerException); Assert.Empty(scene.Data);
    }
    private static string Snapshot(SceneFixture f) => File.ReadAllText(Path.Combine(f.Directory, "jobs.json")) + "\nCHECKPOINT\n" +
        (File.Exists(Path.Combine(f.Directory, "recovery-required")) ? File.ReadAllText(Path.Combine(f.Directory, "recovery-required")) : "MISSING");
}
