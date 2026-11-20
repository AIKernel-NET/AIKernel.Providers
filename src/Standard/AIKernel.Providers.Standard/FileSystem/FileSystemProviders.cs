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
            global::AIKernel.Providers.ProviderBuildVersion.Value,
            ["fs.exists", "fs.read_text", "fs.write_text", "fs.list"],
            ["path", "text", "directory", "file"])
    {
    }

    /// <summary>[EN] Checks whether a path exists. [JA] path が存在するか確認します。</summary>
    /// <summary>Validates credentials before a VFS session is created.</summary>
    public Func<IVfsCredentials, bool>? CredentialValidator { get; init; }

    /// <summary>Reads the original bytes from the provider.</summary>
    public virtual byte[] ReadBytes(string path) => System.Text.Encoding.UTF8.GetBytes(ReadText(path));
    /// <summary>Writes bytes without silent text replacement.</summary>
    public virtual void WriteBytes(string path, byte[] content)
        => WriteText(path, new System.Text.UTF8Encoding(false, true).GetString(content));

    /// <summary>Reports whether a virtual path exists.</summary>
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
    {
        ArgumentNullException.ThrowIfNull(credentials);
        if (CredentialValidator?.Invoke(credentials) != true)
            throw new UnauthorizedAccessException("VFS credentials were not accepted.");
        return Task.FromResult<IVfsSession>(new StandardFileSystemVfsSession(this));
    }

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
    /// <summary>
    /// EN: Executes SessionId.
    /// [EN] Documents this public package API member. [JA] SessionId を実行します。
    /// </summary>

    public string SessionId { get; } = Guid.NewGuid().ToString("N");
    /// <summary>
    /// EN: Executes DisposeAsync.
    /// [EN] Documents this public package API member. [JA] DisposeAsync を実行します。
    /// </summary>

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    /// <summary>
    /// EN: Executes ReadFileAsync.
    /// [EN] Documents this public package API member. [JA] ReadFileAsync を実行します。
    /// </summary>

    public Task<IVfsFile> ReadFileAsync(string path)
        => Task.FromResult<IVfsFile>(new StandardVfsFile(path, _provider.ReadBytes(path)));
    /// <summary>
    /// EN: Executes ExistsAsync.
    /// [EN] Documents this public package API member. [JA] ExistsAsync を実行します。
    /// </summary>

    public Task<bool> ExistsAsync(string path)
        => Task.FromResult(_provider.Exists(path));
    /// <summary>
    /// EN: Executes GetDirectoryAsync.
    /// [EN] Documents this public package API member. [JA] GetDirectoryAsync を実行します。
    /// </summary>

    public Task<IVfsDirectory> GetDirectoryAsync(string path)
        => Task.FromResult<IVfsDirectory>(new StandardVfsDirectory(path, _provider.List(path), _provider));
    /// <summary>
    /// EN: Executes WriteFileAsync.
    /// [EN] Documents this public package API member. [JA] WriteFileAsync を実行します。
    /// </summary>

    public Task WriteFileAsync(string path, byte[] content)
    {
        _provider.WriteBytes(path, content);
        return Task.CompletedTask;
    }
    /// <summary>
    /// EN: Executes DeleteAsync.
    /// [EN] Documents this public package API member. [JA] DeleteAsync を実行します。
    /// </summary>

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
    /// <summary>
    /// EN: Executes QueryAsync.
    /// [EN] Documents this public package API member. [JA] QueryAsync を実行します。
    /// </summary>

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

internal sealed class StandardVfsFile(string path, byte[] content) : IVfsFile
{
    private readonly byte[] _content = content;
    /// <summary>
    /// EN: Executes Name.
    /// [EN] Documents this public package API member. [JA] Name を実行します。
    /// </summary>

    public string Name => System.IO.Path.GetFileName(path);
    /// <summary>
    /// EN: Gets Path.
    /// [EN] Documents this public package API member. [JA] Path を取得します。
    /// </summary>

    public string Path => path;
    /// <summary>
    /// EN: Executes Size.
    /// [EN] Documents this public package API member. [JA] Size を実行します。
    /// </summary>

    public long Size => _content.Length;
    /// <summary>
    /// EN: Gets CreatedAt.
    /// [EN] Documents this public package API member. [JA] CreatedAt を取得します。
    /// </summary>

    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    /// <summary>
    /// EN: Gets ModifiedAt.
    /// [EN] Documents this public package API member. [JA] ModifiedAt を取得します。
    /// </summary>

    public DateTime ModifiedAt { get; } = DateTime.UtcNow;
    /// <summary>
    /// EN: Executes GetMetadata.
    /// [EN] Documents this public package API member. [JA] GetMetadata を実行します。
    /// </summary>

    public IReadOnlyDictionary<string, string>? GetMetadata() => null;
    /// <summary>
    /// EN: Executes ReadAsync.
    /// [EN] Documents this public package API member. [JA] ReadAsync を実行します。
    /// </summary>

    public Task<byte[]> ReadAsync()
        => Task.FromResult(_content.ToArray());
    /// <summary>
    /// EN: Executes ReadAsTextAsync.
    /// [EN] Documents this public package API member. [JA] ReadAsTextAsync を実行します。
    /// </summary>

    public Task<string> ReadAsTextAsync()
        => Task.FromResult(System.Text.Encoding.UTF8.GetString(_content));
}

internal sealed class StandardVfsDirectory(
    string path,
    IReadOnlyList<string> entries,
    FileSystemProviderBase provider) : IVfsDirectory
{
    /// <summary>
    /// EN: Executes Name.
    /// [EN] Documents this public package API member. [JA] Name を実行します。
    /// </summary>
    public string Name => System.IO.Path.GetFileName(path.TrimEnd('/', '\\'));
    /// <summary>
    /// EN: Gets Path.
    /// [EN] Documents this public package API member. [JA] Path を取得します。
    /// </summary>

    public string Path => path;
    /// <summary>
    /// EN: Executes GetMetadata.
    /// [EN] Documents this public package API member. [JA] GetMetadata を実行します。
    /// </summary>

    public IReadOnlyDictionary<string, string>? GetMetadata() => null;
    /// <summary>
    /// EN: Executes GetFilesAsync.
    /// [EN] Documents this public package API member. [JA] GetFilesAsync を実行します。
    /// </summary>

    public Task<IReadOnlyList<IVfsFile>> GetFilesAsync(bool recursive = false)
        => Task.FromResult<IReadOnlyList<IVfsFile>>(
            entries
                .Where(provider.Exists)
                .Select(item => (IVfsFile)new StandardVfsFile(item, provider.ReadBytes(item)))
                .ToArray());
    /// <summary>
    /// EN: Executes GetDirectoriesAsync.
    /// [EN] Documents this public package API member. [JA] GetDirectoriesAsync を実行します。
    /// </summary>

    public Task<IReadOnlyList<IVfsDirectory>> GetDirectoriesAsync()
        => Task.FromResult<IReadOnlyList<IVfsDirectory>>([]);
    /// <summary>
    /// EN: Executes GetEntriesAsync.
    /// [EN] Documents this public package API member. [JA] GetEntriesAsync を実行します。
    /// </summary>

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
    /// <summary>
    /// EN: Executes GetSubdirectoryAsync.
    /// [EN] Documents this public package API member. [JA] GetSubdirectoryAsync を実行します。
    /// </summary>

    public Task<IVfsDirectory?> GetSubdirectoryAsync(string name)
        => Task.FromResult<IVfsDirectory?>(new StandardVfsDirectory(
            JoinPath(path, name),
            [],
            provider));

    private static long EntrySize(FileSystemProviderBase provider, string item)
        => provider.Exists(item) ? provider.ReadBytes(item).LongLength : 0;

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
    /// <summary>
    /// EN: Gets IsSuccessful.
    /// [EN] Documents this public package API member. [JA] IsSuccessful を取得します。
    /// </summary>
    public bool IsSuccessful { get; } = isSuccessful;
    /// <summary>
    /// EN: Gets RowCount.
    /// [EN] Documents this public package API member. [JA] RowCount を取得します。
    /// </summary>

    public int RowCount => Rows.Count;
    /// <summary>
    /// EN: Gets ColumnNames.
    /// [EN] Documents this public package API member. [JA] ColumnNames を取得します。
    /// </summary>

    public IReadOnlyList<string> ColumnNames { get; } = ["path", "exists"];
    /// <summary>
    /// EN: Gets Rows.
    /// [EN] Documents this public package API member. [JA] Rows を取得します。
    /// </summary>

    public IReadOnlyList<VfsQueryRow> Rows { get; } = rows;
    /// <summary>
    /// EN: Gets ErrorMessage.
    /// [EN] Documents this public package API member. [JA] ErrorMessage を取得します。
    /// </summary>

    public string? ErrorMessage { get; } = errorMessage;
}

/// <summary>
/// [EN] In-memory file system provider for deterministic tests and transient OS storage.
/// [JA] 決定論的 test と transient OS storage 向けの memory file system Provider です。
/// </summary>
public sealed class MemoryFileSystemProvider : FileSystemProviderBase
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    /// <summary>[EN] Initializes the memory file system provider. [JA] memory file system Provider を初期化します。</summary>
    public MemoryFileSystemProvider()
        : base("providers.fs.memory", "Memory File System Provider")
    {
    }

    /// <summary>[EN] Documents this public package API member. [JA] Exists を実行します。</summary>
    /// <inheritdoc />
    /// <inheritdoc />
    public override bool Exists(string path) => _files.ContainsKey(Normalize(path));

    /// <summary>[EN] Documents this public package API member. [JA] ReadText を実行します。</summary>
    /// <inheritdoc />
    public override string ReadText(string path)
        => RequireSuccess(TryReadText(path));

    /// <summary>[EN] Documents this public package API member. [JA] TryReadText を実行します。</summary>
    /// <inheritdoc />
    public override Result<string> TryReadText(string path)
        =>
            from normalized in Result<string>.Success(Normalize(path))
            from content in ReadMemoryFile(normalized)
            select content;

    /// <summary>[EN] Documents this public package API member. [JA] WriteText を実行します。</summary>
    /// <inheritdoc />
    public override void WriteText(string path, string content)
        => WriteBytes(path, System.Text.Encoding.UTF8.GetBytes(content ?? string.Empty));

    /// <inheritdoc />
    public override void WriteBytes(string path, byte[] content) => _files[Normalize(path)] = content.ToArray();

    /// <inheritdoc />
    public override byte[] ReadBytes(string path) => _files.TryGetValue(Normalize(path), out var data)
        ? data.ToArray() : throw new FileNotFoundException("Memory file was not found.", path);

    /// <summary>[EN] Deletes an in-memory file. [JA] in-memory file を削除します。</summary>
    public void Delete(string path)
        => _files.TryRemove(Normalize(path), out _);

    /// <summary>[EN] Documents this public package API member. [JA] List を実行します。</summary>
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
            ? Option<string>.Some(System.Text.Encoding.UTF8.GetString(value))
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
    private readonly string? _root;

    /// <summary>Restricts physical file access to the configured directory.</summary>
    public PhysicalFileSystemProvider(string root) : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
    }

    private string Resolve(string path)
    {
        var root = _root ?? throw new InvalidOperationException("Configure a physical VFS root before use.");
        var full = Path.GetFullPath(Path.Combine(root, path.TrimStart('/', '\\')));
        var child = Path.GetRelativePath(root, full);
        if (Path.IsPathRooted(child) || child == ".." || child.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Path escapes the VFS root.");
        var current = root;
        foreach (var part in new[] { "." }.Concat(child.Split(Path.DirectorySeparatorChar)))
        {
            current = Path.GetFullPath(Path.Combine(current, part));
            if (new FileInfo(current).LinkTarget is not null)
                throw new UnauthorizedAccessException("Links are not permitted in the physical VFS.");
        }
        return full;
    }

    /// <summary>Creates a provider for capability discovery. Configure a root for file operations.</summary>
    public PhysicalFileSystemProvider()
        : base("providers.fs.physical", "Physical File System Provider")
    {
    }

    /// <summary>[EN] Documents this public package API member. [JA] Exists を実行します。</summary>
    /// <inheritdoc />
    /// <inheritdoc />
    public override bool Exists(string path) => File.Exists(Resolve(path)) || Directory.Exists(Resolve(path));

    /// <summary>[EN] Documents this public package API member. [JA] ReadText を実行します。</summary>
    /// <inheritdoc />
    public override string ReadText(string path) => File.ReadAllText(Resolve(path));

    /// <inheritdoc />
    public override byte[] ReadBytes(string path) => File.ReadAllBytes(Resolve(path));

    /// <summary>[EN] Documents this public package API member. [JA] WriteText を実行します。</summary>
    /// <inheritdoc />
    public override void WriteText(string path, string content)
    {
        WriteBytes(path, System.Text.Encoding.UTF8.GetBytes(content ?? string.Empty));
    }

    /// <inheritdoc />
    public override void WriteBytes(string path, byte[] content)
    {
        path = Resolve(path);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, content);
    }

    /// <summary>[EN] Documents this public package API member. [JA] List を実行します。</summary>
    /// <inheritdoc />
    public override IReadOnlyList<string> List(string path)
        => Directory.Exists(Resolve(path))
            ? Directory.EnumerateFiles(Resolve(path), "*", new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint })
                .Select(file => Path.GetRelativePath(_root!, file)).Order(StringComparer.Ordinal).ToArray()
            : [];

    /// <summary>[EN] Deletes a physical file or directory. [JA] physical file または directory を削除します。</summary>
    public void Delete(string path)
    {
        path = Resolve(path);
        if (Path.GetRelativePath(_root!, path) == ".") throw new UnauthorizedAccessException("Cannot delete the VFS root.");
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

    /// <summary>[EN] Documents this public package API member. [JA] Exists を実行します。</summary>
    /// <inheritdoc />
    /// <inheritdoc />
    public override byte[] ReadBytes(string path)
    {
        using var archive = ZipFile.OpenRead(_zipPath);
        var entry = archive.GetEntry(Normalize(path)) ?? throw new FileNotFoundException("ZIP entry was not found.", path);
        using var input = entry.Open();
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    /// <inheritdoc />
    public override bool Exists(string path)
    {
        using var archive = ZipFile.OpenRead(_zipPath);
        return archive.GetEntry(Normalize(path)) is not null;
    }

    /// <summary>[EN] Documents this public package API member. [JA] ReadText を実行します。</summary>
    /// <inheritdoc />
    public override string ReadText(string path)
    {
        using var archive = ZipFile.OpenRead(_zipPath);
        var entry = archive.GetEntry(Normalize(path))
            ?? throw new FileNotFoundException("ZIP entry was not found.", path);
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    /// <summary>[EN] Documents this public package API member. [JA] WriteText を実行します。</summary>
    /// <inheritdoc />
    public override void WriteText(string path, string content)
        => throw new NotSupportedException("ZIP file system provider is read-only.");

    /// <summary>[EN] Documents this public package API member. [JA] List を実行します。</summary>
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
