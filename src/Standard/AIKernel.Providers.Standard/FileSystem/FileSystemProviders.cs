namespace AIKernel.Providers.Standard.FileSystem;

using System.Collections.Concurrent;
using System.IO.Compression;
using AIKernel.Common.Results;
using AIKernel.Enums;
using AIKernel.Dtos.Vfs;
using AIKernel.Providers.Standard;
using AIKernel.Vfs;

/// <summary>
/// [EN] Base class for read-oriented standard file system providers.
/// [JA] read-oriented な標準 file system Provider の base class です。
/// </summary>
public abstract class FileSystemProviderBase : StandardProviderBase, IFileSystemProvider
{
    /// <summary>[EN] Initializes a file system provider. [JA] file system Provider を初期化します。</summary>
    protected FileSystemProviderBase(
        string providerId,
        string name)
        : base(
            providerId,
            name,
            "0.1.1",
            ["fs.exists", "fs.read_text", "fs.write_text", "fs.list"],
            ["path", "text", "directory", "file"])
    {
    }

    /// <summary>[EN] Checks whether a path exists. [JA] path が存在するか確認します。</summary>
    public abstract bool Exists(string path);

    /// <summary>[EN] Reads text from a path. [JA] path から text を読み取ります。</summary>
    public abstract string ReadText(string path);

    /// <summary>[EN] Writes text to a path. [JA] path へ text を書き込みます。</summary>
    public abstract void WriteText(string path, string content);

    /// <summary>[EN] Lists entries under a path. [JA] path 配下の entry を列挙します。</summary>
    public abstract IReadOnlyList<string> List(string path);

    /// <summary>[EN] Safely checks whether a path exists. [JA] path の存在を安全に確認します。</summary>
    public virtual Result<bool> TryExists(string path)
        => Try.Run(() => Exists(path));

    /// <summary>[EN] Safely reads text from a path. [JA] path から text を安全に読み取ります。</summary>
    public virtual Result<string> TryReadText(string path)
        => Try.Run(() => ReadText(path));

    /// <summary>[EN] Safely writes text to a path. [JA] path へ text を安全に書き込みます。</summary>
    public virtual Result<bool> TryWriteText(string path, string? content)
        => Try.Run(() =>
        {
            WriteText(path, content ?? string.Empty);
            return true;
        });

    /// <summary>[EN] Safely lists entries under a path. [JA] path 配下の entry を安全に列挙します。</summary>
    public virtual Result<IReadOnlyList<string>> TryList(string path)
        => Try.Run(() => List(path));

    /// <summary>[EN] Opens a VFS session for this file system provider. [JA] この file system Provider の VFS session を開きます。</summary>
    public Task<IVfsSession> OpenSessionAsync(IVfsCredentials credentials)
        => Task.FromResult<IVfsSession>(new StandardFileSystemVfsSession(this));

    /// <summary>[EN] Returns VFS health for the file-system alias contract. [JA] file-system alias contract 向けの VFS health を返します。</summary>
    async Task<VfsProviderHealth> IVfsProvider.GetHealthAsync()
        => new()
        {
            IsHealthy = await IsAvailableAsync().ConfigureAwait(false),
            Message = "Standard file-system provider is available.",
            CheckedAtUtc = DateTime.UtcNow
        };
}

internal sealed class StandardFileSystemVfsSession(FileSystemProviderBase provider) : IVfsSession
{
    private readonly FileSystemProviderBase _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    public string SessionId { get; } = Guid.NewGuid().ToString("N");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<IVfsFile> ReadFileAsync(string path)
        => Task.FromResult<IVfsFile>(new StandardVfsFile(path, _provider.ReadText(path)));

    public Task<bool> ExistsAsync(string path)
        => Task.FromResult(_provider.Exists(path));

    public Task<IVfsDirectory> GetDirectoryAsync(string path)
        => Task.FromResult<IVfsDirectory>(new StandardVfsDirectory(path, _provider.List(path), _provider));

    public Task WriteFileAsync(string path, byte[] content)
    {
        _provider.WriteText(path, System.Text.Encoding.UTF8.GetString(content ?? []));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path)
    {
        if (_provider is MemoryFileSystemProvider memory)
        {
            memory.Delete(path);
            return Task.CompletedTask;
        }

        if (_provider is PhysicalFileSystemProvider physical)
        {
            physical.Delete(path);
            return Task.CompletedTask;
        }

        throw new NotSupportedException("This file system provider is read-only.");
    }

    public Task<IVfsQueryResult> QueryAsync(IVfsQuery query)
    {
        var path = QueryPath(query);
        var rows = _provider.List(path)
            .Skip(query.Offset ?? 0)
            .Take(query.Limit ?? int.MaxValue)
            .Select(item => new VfsQueryRow
            {
                Data = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["path"] = item,
                    ["exists"] = _provider.Exists(item).ToString()
                }
            })
            .ToArray();

        return Task.FromResult<IVfsQueryResult>(new StandardVfsQueryResult(true, rows, null));
    }

    private static string QueryPath(IVfsQuery query)
        => QueryPathOption(query).Match(() => "/", path => path);

    private static Option<string> QueryPathOption(IVfsQuery query)
        => query.Filters is not null && query.Filters.TryGetValue("path", out var filterPath)
            ? Option<string>.Some(filterPath)
            : Option<string>.None();
}

internal sealed class StandardVfsFile(string path, string content) : IVfsFile
{
    private readonly string _content = content ?? string.Empty;

    public string Name => System.IO.Path.GetFileName(path);

    public string Path => path;

    public long Size => System.Text.Encoding.UTF8.GetByteCount(_content);

    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public DateTime ModifiedAt { get; } = DateTime.UtcNow;

    public IReadOnlyDictionary<string, string>? GetMetadata() => null;

    public Task<byte[]> ReadAsync()
        => Task.FromResult(System.Text.Encoding.UTF8.GetBytes(_content));

    public Task<string> ReadAsTextAsync()
        => Task.FromResult(_content);
}

internal sealed class StandardVfsDirectory(
    string path,
    IReadOnlyList<string> entries,
    FileSystemProviderBase provider) : IVfsDirectory
{
    public string Name => System.IO.Path.GetFileName(path.TrimEnd('/', '\\'));

    public string Path => path;

    public IReadOnlyDictionary<string, string>? GetMetadata() => null;

    public Task<IReadOnlyList<IVfsFile>> GetFilesAsync(bool recursive = false)
        => Task.FromResult<IReadOnlyList<IVfsFile>>(
            entries
                .Where(provider.Exists)
                .Select(item => (IVfsFile)new StandardVfsFile(item, provider.ReadText(item)))
                .ToArray());

    public Task<IReadOnlyList<IVfsDirectory>> GetDirectoriesAsync()
        => Task.FromResult<IReadOnlyList<IVfsDirectory>>([]);

    public Task<IReadOnlyList<VfsEntry>> GetEntriesAsync()
        => Task.FromResult<IReadOnlyList<VfsEntry>>(
            entries
                .Select(item => new VfsEntry
                {
                    Name = System.IO.Path.GetFileName(item),
                    Path = item,
                    Type = VfsEntryType.File,
                    Size = EntrySize(provider, item),
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                })
                .ToArray());

    public Task<IVfsDirectory?> GetSubdirectoryAsync(string name)
        => Task.FromResult<IVfsDirectory?>(new StandardVfsDirectory(
            JoinPath(path, name),
            [],
            provider));

    private static long EntrySize(FileSystemProviderBase provider, string item)
        => EntryContent(provider, item)
            .Match(
                () => 0,
                content => System.Text.Encoding.UTF8.GetByteCount(content));

    private static Option<string> EntryContent(FileSystemProviderBase provider, string item)
        => provider.Exists(item)
            ? Option<string>.Some(provider.ReadText(item))
            : Option<string>.None();

    private static string JoinPath(string path, string name)
        => MonadicDecision.SelectText(
            !string.IsNullOrWhiteSpace(path),
            name,
            $"{path.TrimEnd('/', '\\')}/{name}");
}

internal sealed class StandardVfsQueryResult(
    bool isSuccessful,
    IReadOnlyList<VfsQueryRow> rows,
    string? errorMessage) : IVfsQueryResult
{
    public bool IsSuccessful { get; } = isSuccessful;

    public int RowCount => Rows.Count;

    public IReadOnlyList<string> ColumnNames { get; } = ["path", "exists"];

    public IReadOnlyList<VfsQueryRow> Rows { get; } = rows;

    public string? ErrorMessage { get; } = errorMessage;
}

/// <summary>
/// [EN] In-memory file system provider for deterministic tests and transient OS storage.
/// [JA] 決定論的 test と transient OS storage 向けの memory file system Provider です。
/// </summary>
public sealed class MemoryFileSystemProvider : FileSystemProviderBase
{
    private readonly ConcurrentDictionary<string, string> _files = new(StringComparer.Ordinal);

    /// <summary>[EN] Initializes the memory file system provider. [JA] memory file system Provider を初期化します。</summary>
    public MemoryFileSystemProvider()
        : base("providers.fs.memory", "Memory File System Provider")
    {
    }

    /// <inheritdoc />
    public override bool Exists(string path) => _files.ContainsKey(Normalize(path));

    /// <inheritdoc />
    public override string ReadText(string path)
        => RequireSuccess(TryReadText(path));

    /// <inheritdoc />
    public override Result<string> TryReadText(string path)
        =>
            from normalized in Result<string>.Success(Normalize(path))
            from content in ReadMemoryFile(normalized)
            select content;

    /// <inheritdoc />
    public override void WriteText(string path, string content)
        => _files[Normalize(path)] = content ?? string.Empty;

    /// <summary>[EN] Deletes an in-memory file. [JA] in-memory file を削除します。</summary>
    public void Delete(string path)
        => _files.TryRemove(Normalize(path), out _);

    /// <inheritdoc />
    public override IReadOnlyList<string> List(string path)
    {
        var prefix = Normalize(path).TrimEnd('/') + "/";
        return _files.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string Normalize(string path)
        => NormalizePath(path).Match(value => value, value => value);

    private Result<string> ReadMemoryFile(string normalized)
        => MemoryFileOption(normalized)
            .Match(
                () => Result<string>.Fail($"Memory file was not found: {normalized}. ErrorCode=FS_MEMORY_FILE_NOT_FOUND"),
                Result<string>.Success);

    private Option<string> MemoryFileOption(string normalized)
        => _files.TryGetValue(normalized, out var value)
            ? Option<string>.Some(value)
            : Option<string>.None();

    private static Either<string, string> NormalizePath(string path)
        => string.IsNullOrWhiteSpace(path)
            ? Either<string, string>.FromLeft("/")
            : Either<string, string>.FromRight(path.Replace('\\', '/'));

    private static T RequireSuccess<T>(Result<T> result)
        => result.Match(
            error => throw new InvalidOperationException(error.Message),
            value => value);
}

/// <summary>
/// [EN] Physical file system provider for host-local files.
/// [JA] host-local file 向けの physical file system Provider です。
/// </summary>
public sealed class PhysicalFileSystemProvider : FileSystemProviderBase
{
    /// <summary>[EN] Initializes the physical file system provider. [JA] physical file system Provider を初期化します。</summary>
    public PhysicalFileSystemProvider()
        : base("providers.fs.physical", "Physical File System Provider")
    {
    }

    /// <inheritdoc />
    public override bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    /// <inheritdoc />
    public override string ReadText(string path) => File.ReadAllText(path);

    /// <inheritdoc />
    public override void WriteText(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content ?? string.Empty);
    }

    /// <inheritdoc />
    public override IReadOnlyList<string> List(string path)
        => Directory.Exists(path)
            ? Directory.EnumerateFileSystemEntries(path).Order(StringComparer.Ordinal).ToArray()
            : [];

    /// <summary>[EN] Deletes a physical file or directory. [JA] physical file または directory を削除します。</summary>
    public void Delete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
            return;
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}

/// <summary>
/// [EN] ZIP-backed read-only file system provider useful for WAD-like bundled assets.
/// [JA] WAD 風 bundle asset に便利な ZIP backed read-only file system Provider です。
/// </summary>
public sealed class ZipFileSystemProvider : FileSystemProviderBase
{
    private readonly string _zipPath;

    /// <summary>[EN] Initializes the ZIP file system provider. [JA] ZIP file system Provider を初期化します。</summary>
    public ZipFileSystemProvider(string zipPath)
        : base("providers.fs.zip", "ZIP File System Provider")
    {
        _zipPath = string.IsNullOrWhiteSpace(zipPath)
            ? throw new ArgumentException("ZIP path is required.", nameof(zipPath))
            : zipPath;
    }

    /// <inheritdoc />
    public override bool Exists(string path)
    {
        using var archive = ZipFile.OpenRead(_zipPath);
        return archive.GetEntry(Normalize(path)) is not null;
    }

    /// <inheritdoc />
    public override string ReadText(string path)
    {
        using var archive = ZipFile.OpenRead(_zipPath);
        var entry = archive.GetEntry(Normalize(path))
            ?? throw new FileNotFoundException("ZIP entry was not found.", path);
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    /// <inheritdoc />
    public override void WriteText(string path, string content)
        => throw new NotSupportedException("ZIP file system provider is read-only.");

    /// <inheritdoc />
    public override IReadOnlyList<string> List(string path)
    {
        using var archive = ZipFile.OpenRead(_zipPath);
        var prefix = Normalize(path).TrimEnd('/') + "/";
        return archive.Entries
            .Select(entry => entry.FullName)
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string Normalize(string path)
        => (path ?? string.Empty).Replace('\\', '/').TrimStart('/');
}
