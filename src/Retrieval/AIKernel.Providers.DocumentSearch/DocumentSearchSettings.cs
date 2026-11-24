namespace AIKernel.Providers.DocumentSearch;

/// <summary>Host settings for one bounded, read-only document corpus.</summary>
public sealed record DocumentSearchSettings
{
    /// <summary>The host-configured directory containing the searchable documents.</summary>
    public required string RootDirectory { get; init; }

    /// <summary>Maximum number of directory entries that may be examined by one search.</summary>
    public int MaxTraversalEntries { get; init; } = 2048;

    /// <summary>Maximum number of Markdown and text files that may be read by one search.</summary>
    public int MaxDocuments { get; init; } = 256;

    /// <summary>Maximum number of bytes in one document.</summary>
    public int MaxDocumentBytes { get; init; } = 1024 * 1024;

    /// <summary>Maximum total number of document bytes read by one search.</summary>
    public long MaxCorpusBytes { get; init; } = 16L * 1024 * 1024;

    /// <summary>Maximum search query length in UTF-16 code units.</summary>
    public int MaxQueryCharacters { get; init; } = 256;

    /// <summary>Maximum excerpt length in UTF-16 code units.</summary>
    public int MaxExcerptCharacters { get; init; } = 640;

    /// <summary>Maximum number of results a caller may request.</summary>
    public int MaxTopK { get; init; } = 50;
}
