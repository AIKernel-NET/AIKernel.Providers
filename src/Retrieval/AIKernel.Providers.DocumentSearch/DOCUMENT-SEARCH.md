# Local document search

`AIKernel.Providers.DocumentSearch` implements `IRagSearchProvider` from AIKernel 0.1.4. It searches UTF-8 `.md`, `.markdown` and `.txt` files under a host-selected directory on the CPU. It uses keyword matching. Embedding-based semantic search is outside this release.

```csharp
using AIKernel.Abstractions.Providers;
using AIKernel.Providers.DocumentSearch;

IRagSearchProvider search = new LocalDocumentSearchProvider(new DocumentSearchSettings
{
    RootDirectory = "./approved-documents"
});

var hits = await search.SearchAsync("backup retention", topK: 3, cancellationToken: cancellationToken);
foreach (var hit in hits)
    Console.WriteLine($"{hit.Source}  {hit.RawData}");
```

Matching is case- and width-insensitive. ASCII words match at word boundaries and Japanese keywords can match within a line. Separate keywords with spaces. Results are ranked by the fraction of distinct keywords present in the returned excerpt. Ties use the ordinal relative path and then line number. Each result cites one physical line. Long lines are excerpted around a matching keyword.

`Source` is an escaped, root-relative reference such as `policies/backup.md#L12`. `RawData` preserves the cited characters from the file. `Metadata` contains `relative_path`, `line_start`, `line_end`, `column_start`, `column_end`, `document_sha256`, `excerpt_sha256`, and `retrieval_kind`. Lines and columns are 1-based and inclusive. Columns count UTF-16 code units. `document_sha256` hashes the original file bytes and `excerpt_sha256` hashes the UTF-8 encoding of `RawData`. Both use lowercase hex. This allows the host to save and verify the exact retrieved observation for replay.

The defaults inspect at most 2,048 entries and read at most 256 documents, 1 MiB per document, and 16 MiB in total. Settings also limit query length, excerpt length, and the requested result count. A search scans the current corpus and returns an empty list when no line matches. Invalid queries, unreadable corpora, invalid UTF-8, and exceeded limits fail the entire search. Cancellation raises `OperationCanceledException`. Hidden entries and symbolic links or reparse points inside the corpus are skipped. Configure a dedicated directory containing files controlled by the host operator.

For CTG, the host can compare the returned source and document digest with an approved revision, then put `hit.RawData` under a host-owned key in `CouncilEvaluationRequest.InputFacts`. Keep the proposal in its own argument and retain the source fields and digest with the audit record. Mark missing or unapproved evidence as `Unavailable` in `BonsaiCtgEvidenceRequirements`. The retrieval score measures keyword coverage only.
