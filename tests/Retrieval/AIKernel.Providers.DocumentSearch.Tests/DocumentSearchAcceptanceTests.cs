using System.Security.Cryptography;
using System.Text;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Governance;

namespace AIKernel.Providers.DocumentSearch.Tests;

public sealed class DocumentSearchAcceptanceTests
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task JapaneseMarkdownReturnsExactTextAndCtgEvidence()
    {
        using var corpus = new TestCorpus();
        const string excerpt = "証跡の保存期間は30日です。";
        const string path = "policies/運用 #1.md";
        byte[] document = Encoding.UTF8.GetBytes("\uFEFF# 運用手順\r\n\r\n" + excerpt + "\r\n");
        corpus.Write(path, document);

        IRagSearchProvider provider = corpus.Provider();
        var hit = Assert.Single(await provider.SearchAsync("保存期間 30日", cancellationToken: TestCancellation));
        Assert.Equal(excerpt, hit.RawData);
        Assert.Equal("policies/%E9%81%8B%E7%94%A8%20%231.md#L3", hit.Source);
        Assert.Equal(DateTimeKind.Utc, hit.RetrievedAt.Kind);
        Assert.Equal(1, hit.RelevanceScore);
        var source = Assert.IsType<Dictionary<string, object>>(hit.Metadata);
        Assert.Equal(path, source["relative_path"]);
        Assert.Equal(3, source["line_start"]);
        Assert.Equal(3, source["line_end"]);
        Assert.Equal(1, source["column_start"]);
        Assert.Equal(excerpt.Length, source["column_end"]);
        Assert.Equal(Hash(document), source["document_sha256"]);
        Assert.Equal(Hash(Encoding.UTF8.GetBytes(excerpt)), source["excerpt_sha256"]);

        var request = new CouncilEvaluationRequest
        {
            OperationId = "apply-retention-policy",
            InputFacts = new Dictionary<string, string> { ["retention-policy"] = hit.RawData }
        };
        Assert.Equal(excerpt, request.InputFacts["retention-policy"]);
    }

    [Fact]
    public async Task EnglishRankingHonorsWidthCaseTiesAndTopK()
    {
        using var corpus = new TestCorpus();
        corpus.Write("z.MARKDOWN", "Retention for BACKUP media is 30 days.");
        corpus.Write("b.txt", "Retention is 14 days.");
        corpus.Write("a/guide.md", "# Recovery\nＢＡＣＫＵＰ retention is 30 days.\nBackup retention also covers logs.");
        var provider = corpus.Provider();

        var hits = await provider.SearchAsync("backup RETENTION backup", topK: 3, cancellationToken: TestCancellation);

        Assert.Equal(new[] { "a/guide.md#L2", "a/guide.md#L3", "z.MARKDOWN#L1" }, hits.Select(hit => hit.Source));
        Assert.All(hits, hit => Assert.Equal(1, hit.RelevanceScore));
        var all = await provider.SearchAsync("ＢＡＣＫＵＰ retention", topK: 4, cancellationToken: TestCancellation);
        Assert.Equal(hits.Select(hit => hit.Source), all.Take(3).Select(hit => hit.Source));
        Assert.Equal(0.5, all[3].RelevanceScore);
    }

    [Fact]
    public async Task WordBoundariesUnsupportedFilesAndEmptyResults()
    {
        using var corpus = new TestCorpus();
        corpus.Write("notes.txt", "The program is stored here.");
        corpus.Write("attachment.pdf", [0xff, 0xfe]);
        corpus.Write(".hidden/internal.md", "RAM sensitive content");
        corpus.Write("empty.txt", "");
        var provider = corpus.Provider();

        Assert.Empty(await provider.SearchAsync("RAM", cancellationToken: TestCancellation));
        Assert.Single(await provider.SearchAsync("program", cancellationToken: TestCancellation));
    }

    [Fact]
    public async Task LongLineExcerptHasExactColumnsAndIntactUnicode()
    {
        using var corpus = new TestCorpus();
        string line = string.Concat(Enumerable.Repeat("🍀", 30)) + "共有メモリ" + new string(' ', 10) + new string('a', 30);
        corpus.Write("unicode.md", line);
        var provider = corpus.Provider(settings => settings with { MaxExcerptCharacters = 12 });

        var hit = Assert.Single(await provider.SearchAsync("共有メモリ", cancellationToken: TestCancellation));
        var source = hit.Metadata!;
        int start = Assert.IsType<int>(source["column_start"]) - 1;
        int end = Assert.IsType<int>(source["column_end"]);
        Assert.InRange(hit.RawData.Length, 5, 12);
        Assert.Contains("共有メモリ", hit.RawData);
        Assert.Equal(line[start..end], hit.RawData);
        Assert.Equal(hit.RawData, new UTF8Encoding(false, true).GetString(new UTF8Encoding(false, true).GetBytes(hit.RawData)));
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("... !", 1)]
    [InlineData("too long", 1)]
    [InlineData("find", 0)]
    [InlineData("find", 3)]
    public async Task RejectsInvalidQueriesAndTopK(string query, int topK)
    {
        using var corpus = new TestCorpus();
        var provider = corpus.Provider(settings => settings with { MaxQueryCharacters = 5, MaxTopK = 2 });

        await Assert.ThrowsAnyAsync<ArgumentException>(() => provider.SearchAsync(query, topK, TestCancellation));
    }

    [Theory]
    [InlineData("documents")]
    [InlineData("entries")]
    [InlineData("documentBytes")]
    [InlineData("corpusBytes")]
    public async Task CorpusLimitsFailWithoutPartialResults(string limit)
    {
        using var corpus = new TestCorpus();
        corpus.Write("a.md", "find now");
        corpus.Write("b.txt", "find here");
        var provider = corpus.Provider(settings => limit switch
        {
            "documents" => settings with { MaxDocuments = 1 },
            "entries" => settings with { MaxTraversalEntries = 1 },
            "documentBytes" => settings with { MaxDocumentBytes = 8 },
            _ => settings with { MaxCorpusBytes = 16 }
        });

        await Assert.ThrowsAsync<InvalidDataException>(() => provider.SearchAsync("find", 1, TestCancellation));
    }

    [Fact]
    public async Task RejectsInvalidUtf8AndMissingRoot()
    {
        using var corpus = new TestCorpus();
        corpus.Write("valid.txt", "find now");
        corpus.Write("bad.md", [0xff, 0xfe]);
        var provider = corpus.Provider();
        await Assert.ThrowsAsync<InvalidDataException>(() => provider.SearchAsync("find", 1, TestCancellation));

        var missing = corpus.Provider(settings => settings with { RootDirectory = Path.Combine(corpus.Root, "absent") });
        await Assert.ThrowsAnyAsync<IOException>(() => missing.SearchAsync("find", 1, TestCancellation));
    }

    [Fact]
    public async Task SymlinksCannotExposeFilesOutsideTheCorpus()
    {
        using var corpus = new TestCorpus();
        using var outside = new TestCorpus();
        outside.Write("secret.md", "haystack confidential");
        string link = Path.Combine(corpus.Root, "outside");
        try
        {
            Directory.CreateSymbolicLink(link, outside.Root);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            Assert.Skip("The test account cannot create directory symbolic links.");
        }

        try
        {
            Assert.Empty(await corpus.Provider().SearchAsync("haystack", cancellationToken: TestCancellation));
            var linkedRoot = corpus.Provider(settings => settings with { RootDirectory = link });
            await Assert.ThrowsAnyAsync<IOException>(() => linkedRoot.SearchAsync("haystack", 1, TestCancellation));
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public async Task CancellationIsPropagatedAndASecondSearchCanRun()
    {
        using var corpus = new TestCorpus();
        corpus.Write("running.txt", new string('a', 4 * 1024 * 1024));
        corpus.Write("done.md", "Ready again.");
        var provider = corpus.Provider(settings => settings with { MaxDocumentBytes = 4 * 1024 * 1024 });
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestCancellation);

        var running = provider.SearchAsync("a", cancellationToken: cancelled.Token);
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.SearchAsync("Ready", cancellationToken: cancelled.Token));
        var hit = Assert.Single(await provider.SearchAsync("Ready", cancellationToken: TestCancellation));
        Assert.Equal("done.md#L1", hit.Source);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed class TestCorpus : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("aikernel-document-search-");
        public string Root => _directory.FullName;

        public void Write(string path, string text) => Write(path, Encoding.UTF8.GetBytes(text));

        public void Write(string path, byte[] bytes)
        {
            string fullPath = Path.Combine(Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, bytes);
        }

        public LocalDocumentSearchProvider Provider(Func<DocumentSearchSettings, DocumentSearchSettings>? configure = null)
        {
            var settings = new DocumentSearchSettings { RootDirectory = Root };
            return new LocalDocumentSearchProvider(configure?.Invoke(settings) ?? settings);
        }

        public void Dispose() => _directory.Delete(recursive: true);
    }
}
