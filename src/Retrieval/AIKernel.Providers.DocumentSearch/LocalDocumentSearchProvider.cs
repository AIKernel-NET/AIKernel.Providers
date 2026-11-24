using System.Security.Cryptography;
using System.Text;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Context;

namespace AIKernel.Providers.DocumentSearch;

/// <summary>Searches local UTF-8 Markdown and text files and returns verifiable excerpts.</summary>
public sealed class LocalDocumentSearchProvider : IRagSearchProvider
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly EnumerationOptions Enumeration = new() { AttributesToSkip = 0, IgnoreInaccessible = false };
    private const FileAttributes ExcludedAttributes = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint;

    private readonly DocumentSearchSettings _settings;
    private readonly string _rootDirectory;

    /// <summary>Creates a search provider scoped to a directory selected by the host.</summary>
    public LocalDocumentSearchProvider(DocumentSearchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.RootDirectory);
        if (settings.MaxTraversalEntries < 1 || settings.MaxDocuments < 1 || settings.MaxDocumentBytes < 1
            || settings.MaxCorpusBytes < 1 || settings.MaxQueryCharacters < 1 || settings.MaxExcerptCharacters < 2
            || settings.MaxTopK < 1)
            throw new ArgumentException("Document search limits must be positive, and excerpts must allow at least two characters.", nameof(settings));

        _rootDirectory = Path.GetFullPath(settings.RootDirectory);
        _settings = settings;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MaterialContextDto>> SearchAsync(
        string query, int topK = 10, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (query.Length > _settings.MaxQueryCharacters)
            throw new ArgumentException("The query exceeds the configured character limit.", nameof(query));
        if (topK < 1 || topK > _settings.MaxTopK)
            throw new ArgumentOutOfRangeException(nameof(topK), "The requested result count exceeds the configured range.");

        var keywords = new KeywordMatcher(query, _settings.MaxExcerptCharacters);
        CheckRoot();
        var hits = new SortedSet<SearchHit>(SearchHit.Ranking);
        int documentCount = 0;
        long remainingBytes = _settings.MaxCorpusBytes;
        foreach (string path in EnumerateDocuments(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++documentCount > _settings.MaxDocuments)
                throw new InvalidDataException("The corpus exceeds the configured document count.");

            var file = new FileInfo(path);
            if ((file.Attributes & (ExcludedAttributes | FileAttributes.Directory | FileAttributes.Device)) != 0)
                throw new IOException("A corpus document is no longer a readable file.");
            long length = file.Length;
            if (length == 0)
                continue;
            if (length > _settings.MaxDocumentBytes || length > remainingBytes)
                throw new InvalidDataException("A document or the corpus exceeds the configured byte limit.");

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length != length)
                throw new IOException("The document length changed during retrieval.");

            byte[] bytes = new byte[(int)length];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (stream.Length != length)
                throw new IOException("The document length changed during retrieval.");
            remainingBytes -= length;

            string relativePath = Path.GetRelativePath(_rootDirectory, path).Replace(Path.DirectorySeparatorChar, '/');
            SearchDocument(relativePath, bytes, keywords, hits, topK, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var retrievedAt = DateTime.UtcNow;
        return hits.Select(hit => hit.ToMaterial(retrievedAt)).ToArray();
    }

    private void CheckRoot()
    {
        for (DirectoryInfo? directory = new(_rootDirectory); directory is not null; directory = directory.Parent)
        {
            FileAttributes attributes = File.GetAttributes(directory.FullName);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("The configured corpus directory must use a path without symbolic links or reparse points.");
            if ((attributes & FileAttributes.Directory) == 0)
                throw new DirectoryNotFoundException("The configured corpus root is not a directory.");
        }
    }

    private IEnumerable<string> EnumerateDocuments(CancellationToken cancellationToken)
    {
        var directories = new Stack<DirectoryInfo>();
        directories.Push(new DirectoryInfo(_rootDirectory));
        int entryCount = 0;
        while (directories.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((File.GetAttributes(directory.FullName) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("A corpus directory changed to a symbolic link or reparse point during retrieval.");

            foreach (var entry in directory.EnumerateFileSystemInfos("*", Enumeration))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++entryCount > _settings.MaxTraversalEntries)
                    throw new InvalidDataException("The corpus exceeds the configured directory-entry limit.");
                if ((entry.Attributes & ExcludedAttributes) != 0 || entry.Name[0] == '.')
                    continue;
                if (entry is DirectoryInfo child)
                    directories.Push(child);
                else if (IsDocument(entry.Extension))
                    yield return entry.FullName;
            }
        }
    }

    private static bool IsDocument(string extension)
        => extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);

    private static void SearchDocument(string path, byte[] bytes, KeywordMatcher keywords,
        SortedSet<SearchHit> hits, int topK, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string text;
        try
        {
            var contents = bytes.AsSpan();
            if (contents.StartsWith("\uFEFF"u8))
                contents = contents[3..];
            text = StrictUtf8.GetString(contents);
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException("The corpus contains a document that is not valid UTF-8.");
        }

        if (text.Contains('\0'))
            throw new InvalidDataException("The corpus contains a text document with a NUL character.");

        string? documentHash = null;
        using var reader = new StringReader(text);
        int lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;
            if (keywords.Match(line, cancellationToken) is not { } match)
                continue;

            documentHash ??= Convert.ToHexStringLower(SHA256.HashData(bytes));
            hits.Add(new SearchHit(path, lineNumber, match.Start, line.Substring(match.Start, match.Length), match.Score, documentHash));
            if (hits.Count > topK)
                hits.Remove(hits.Max!);
        }
    }
}
