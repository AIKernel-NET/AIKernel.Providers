namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneJournalCheckpointTests
{
    [Fact]
    public void CleanCloseRemovesCheckpointButOrdinarySavesDoNot()
    {
        InDirectory(directory =>
        {
            using var journal = new SceneJournal(directory, 1, 1024);
            Assert.False(journal.RecoveryRequired);
            Assert.Equal("scene-recovery.v1\n", File.ReadAllText(Marker(directory)));
            journal.Save([]); Assert.True(File.Exists(Marker(directory)));
            journal.Close(quarantined: false); Assert.False(File.Exists(Marker(directory)));
            using var reopened = new SceneJournal(directory, 1, 1024);
            Assert.False(reopened.RecoveryRequired);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirtyWriterAndFailedSerializationRequireExplicitRecovery(bool failedSave)
    {
        InDirectory(directory =>
        {
            using (var journal = new SceneJournal(directory, 1, 1024))
            {
                journal.Save([]);
                if (failedSave)
                {
                    Assert.Throws<IOException>(() => journal.Save(BrokenEntries()));
                    journal.Save([]); // Later successful IO cannot clear the failure.
                    Assert.True(journal.RecoveryRequired);
                    journal.Close(quarantined: false);
                }
                // Dispose alone models lease release without a clean-close acknowledgement.
            }
            for (int restart = 0; restart < 2; restart++)
            {
                using var recovered = new SceneJournal(directory, 1, 1024);
                Assert.True(recovered.RecoveryRequired); recovered.Save([]);
                Assert.True(recovered.RecoveryRequired); recovered.Close(quarantined: false);
                Assert.True(File.Exists(Marker(directory)));
            }
            using (var recovered = new SceneJournal(directory, 1, 1024))
            {
                recovered.Save([], quarantined: true, confirmRecovery: true);
                Assert.False(recovered.RecoveryRequired);
                Assert.True(File.Exists(Marker(directory))); // Protect subsequent writes too.
                recovered.Close(quarantined: false);
            }
            using var clean = new SceneJournal(directory, 1, 1024);
            Assert.False(clean.RecoveryRequired);
        });
    }

    [Fact]
    public void FailedCleanCloseCannotAcknowledgeRecoveryOrLeakWriterLease()
    {
        InDirectory(directory =>
        {
            using var journal = new SceneJournal(directory, 1, 1024); journal.Save([]);
            // Inject a checkpoint deletion failure in this test-owned directory.
            File.Delete(Marker(directory)); Directory.CreateDirectory(Marker(directory));
            var error = Assert.Throws<SceneException>(() => journal.Close(quarantined: false));
            Assert.Equal("journal_unavailable", error.Code); Assert.Null(error.InnerException); Assert.Empty(error.Data);
            using var reopened = new SceneJournal(directory, 1, 1024);
            Assert.True(reopened.RecoveryRequired);
        });
    }

    [Fact]
    public void LegacyJournalWithoutCheckpointRemainsReadable()
    {
        InDirectory(directory =>
        {
            File.WriteAllText(Path.Combine(directory, "jobs.json"), SceneJson.Write(new SceneJournalDocument("scene-journal.v1", [])));
            using var journal = new SceneJournal(directory, 1, 1024);
            Assert.False(journal.RecoveryRequired); Assert.Empty(journal.Load());
            journal.Save([]); journal.Close(quarantined: false);
            Assert.Equal(SceneJson.Write(new SceneJournalDocument("scene-journal.v1", [])), File.ReadAllText(Path.Combine(directory, "jobs.json")));
        });
    }

    private static IEnumerable<SceneJournalEntry> BrokenEntries()
    { yield return FailEntry(); }
    private static SceneJournalEntry FailEntry() => throw new IOException("private-storage-detail");
    private static string Marker(string directory) => Path.Combine(directory, "recovery-required");
    private static void InDirectory(Action<string> test)
    {
        var root = Path.GetFullPath(Path.GetTempPath());
        var directory = Path.GetFullPath(Path.Combine(root, "aikernel-checkpoint-test-" + Guid.NewGuid().ToString("N")));
        Assert.Equal(root.TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(directory));
        Directory.CreateDirectory(directory);
        try { test(directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
