namespace AIKernel.Providers.Cosmos;

internal sealed record SceneJournalEntry(string Key, string Scope, string InputHash, string Generation,
    DateTimeOffset RetainUntilUtc, SceneAnalysisResult Result, bool PhysicalOutstanding = false);

internal sealed record SceneJournalDocument(string SchemaVersion, IReadOnlyList<SceneJournalEntry> Entries);

internal sealed class SceneJournal : IDisposable
{
    private readonly string _directory;
    private readonly string _path;
    private readonly FileStream _lease;
    private readonly int _maxBytes;

    internal SceneJournal(string directory, int maxJobs, int maxResponseBytes)
    {
        if (!Path.IsPathFullyQualified(directory) || directory.StartsWith("\\\\", StringComparison.Ordinal))
            throw new ArgumentException("JournalDirectory must be an explicit local absolute path.");
        _directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(_directory);
        for (var current = new DirectoryInfo(_directory); current is not null; current = current.Parent)
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("Journal paths cannot traverse links.");
        _path = Path.Combine(_directory, "jobs.json");
        var leasePath = Path.Combine(_directory, "writer.lock");
        foreach (var file in new[] { _path, leasePath })
            if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("Journal files cannot be links.");
        _lease = new FileStream(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        _maxBytes = checked(maxJobs * (maxResponseBytes + 8192));
    }

    internal IReadOnlyList<SceneJournalEntry> Load()
    {
        if (!File.Exists(_path)) return [];
        if (new FileInfo(_path).Length > _maxBytes) throw new SceneException("journal_invalid");
        try
        {
            var document = SceneJson.Read<SceneJournalDocument>(File.ReadAllText(_path), _maxBytes);
            if (document.SchemaVersion != "scene-journal.v1") throw new SceneException("journal_invalid");
            return document.Entries;
        }
        catch (Exception ex) when (ex is SceneException or IOException) { throw new SceneException("journal_invalid"); }
    }

    internal void Save(IEnumerable<SceneJournalEntry> entries)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(SceneJson.Write(new SceneJournalDocument("scene-journal.v1", entries.ToArray())));
        if (bytes.Length > _maxBytes) throw new SceneException("journal_capacity");
        string temporary = Path.Combine(_directory, ".jobs-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, _path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void Dispose() => _lease.Dispose();
}
