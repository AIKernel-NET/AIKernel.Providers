# 出典付きローカル文書検索

`AIKernel.Providers.DocumentSearch` はAIKernel 0.1.4の `IRagSearchProvider` を実装します。ホスト指定フォルダー内のUTF-8の `.md`、`.markdown`、`.txt` をCPUでキーワード検索します。埋め込みによる意味検索は今回の対象外です。

```csharp
using AIKernel.Abstractions.Providers;
using AIKernel.Providers.DocumentSearch;

IRagSearchProvider search = new LocalDocumentSearchProvider(new DocumentSearchSettings
{
    RootDirectory = "./approved-documents"
});

var hits = await search.SearchAsync("バックアップ 保持期間", topK: 3, cancellationToken: cancellationToken);
foreach (var hit in hits)
    Console.WriteLine($"{hit.Source}  {hit.RawData}");
```

キーワードは空白で区切ります。大文字と小文字、全角と半角を同一視します。英数字は単語境界で、日本語の語句は行の途中でも一致します。抜粋に含まれる検索語の割合が高い順に返し、同点は相対パス、続いて行番号で並べます。1件につき原文の1行から抜粋し、長い行は検索語の付近を切り出します。

`Source` は `policies/backup.md#L12` のようなルート相対の参照先です。`RawData` は元ファイルの文字列をそのまま返します。`Metadata` に相対パス、開始・終了行と列、原文と抜粋のSHA-256を含めます。行と列は1始まりで、終了位置を含みます。列はUTF-16コード単位です。`document_sha256` はファイルの元バイト列、`excerpt_sha256` は `RawData` のUTF-8バイト列をハッシュ化します。どちらも小文字16進数です。

既定値は、走査2,048エントリ、読み取り256文書、1文書1 MiB、合計16 MiBです。クエリ長、抜粋長、結果件数と併せて、`DocumentSearchSettings` から変更できます。検索ごとに現在の文書を読み、該当がなければ空の結果を返します。上限超過、不正なUTF-8、読み取り失敗時は検索全体が失敗し、キャンセルは `OperationCanceledException` で通知します。配下の隠し項目、シンボリックリンク、再解析ポイントは読み飛ばします。運用者が管理する専用フォルダーを指定してください。

## CTGへ根拠を渡す

ホストが `Source` と `document_sha256` を承認済みの文書版と照合し、確認した `RawData` をホスト定義のキーで `CouncilEvaluationRequest.InputFacts` に入れます。提案本文は別の引数で渡し、参照先とハッシュは監査用に保管します。必要な根拠が欠けている場合は、そのキーを `BonsaiCtgEvidenceRequirements` の `Unavailable` とします。`RelevanceScore` は検索語の一致率です。
