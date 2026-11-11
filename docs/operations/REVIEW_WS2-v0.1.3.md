# WS2: CPU カーネルの識別

## 基準と再現

- 対象: `AIKernel.Providers`、ローカル `dev` の `db7e989` から作成した `review/WS2`。
- `CpuComputeProvider` はソースに `out[i] = a[i] + b[i]` または空白を省いた文字列が含まれるだけで、CPU の vector-add を選択していた。
- 追加した `CpuKernelIdentityTests` を改修前に実行すると、18 ケース中 9 ケースが失敗した。コメントだけのソース、計算式・binding・entry point を変えたソースなどが成功し、出力バッファを書き換えた。

## 変更

公開済みの `ComputeKernel` にはカーネル ID を指定するプロパティがないため、公開契約を変えず、Provider 内部で `cpu.vector_add.f32.v1` に登録したソースの SHA-256 と照合する。照合に成功した場合だけ内部 ID を返し、実行処理はその ID で選択する。

登録ソースは v0.1.3 の `ComputeKernel.CreateVectorAdd` が生成する WGSL 全体を固定したもの。正の workgroup size だけをパラメーターとし、数値は invariant culture で表記する。実行時の factory から登録ソースを取得しないため、将来の factory の変更を自動的に信頼しない。

既存の factory が OS の改行を使うため、LF と CRLF の二つの正確なソースを登録する。入力のコメントや空白を除去したり、改行を正規化したりしない。ソースへの追記、binding・entry point・計算式の変更、ID を装ったコメントも不一致となり、バッファの読み書きより前に `CPU_KERNEL_NOT_SUPPORTED` で失敗する。

ハッシュは既存の `ISemanticHasher` / `Sha256SemanticHasher` を通す。`CanonicalizedRomDto.CanonicalBody` にソースをそのまま渡し、既存実装の UTF-8 SHA-256 を利用する。この経路で ROM の整形は行わない。

公開インターフェース・DTO・enum、既存の StepId / ReplayLogHash / ROM の計算、vector-add の計算とバッファ検証、既存テストの期待値は変更しない。

## 追加テスト

`tests/Standard/StandardProviders.Tests/CpuKernelIdentityTests.cs` に 18 ケースを追加した。

- 標準ソース: workgroup size 1 / 32 / 64 / 256 と LF / CRLF の組合せ 8 ケース。計算結果と入力バッファの不変性を検証する。
- 別ソース: コメント、空白なしコメント、計算式の変更、binding の変更、entry point の変更、末尾への追記、加算文だけ、ID を装ったコメント、未対応の減算の 9 ケース。失敗の理由と全バッファが変更されないことを検証する。
- `ExecuteKernelAsync`: 改変ソースを例外として拒否し、出力を書き換えない 1 ケース。

改修後の単体実行では 18 ケースすべて成功した。

Providers のリポジトリ直下で、追加分だけを再実行できる。

```powershell
dotnet test tests/Standard/StandardProviders.Tests/StandardProviders.Tests.csproj -c Release --filter FullyQualifiedName~CpuKernelIdentityTests
```

## 下流検証

2026-11-11、Windows / .NET SDK 10.0.401 で Release の restore / build / test を実行した。修正版 `AIKernel.Providers.Standard` を公開せず検証専用 feed に pack し、独立した NuGet キャッシュで下流に適用した。他の依存パッケージは既存の公開版を使い、未取り込みの CT1 などを混ぜていない。

下流は各リポジトリの `dev` を `git archive` したコピーを使う。

| リポジトリ | 検証元の dev |
| --- | --- |
| Cuda13.0 | `9e3ef82` |
| Control | `2229477` |
| Tools | `05ec021` |
| Wasm | `2c6b11f` |
| Demo | `b3fcb62` |
| Doom | `3df7008` |

| リポジトリ | restore / build | テスト成功 | 既知の失敗 |
| --- | --- | ---: | ---: |
| Providers | 成功 | 138 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 42 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 64 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |
| 合計 | 全 7 リポジトリ成功 | 503 | 2 |

テストのスキップは 0 件。追加した 18 ケースは全件成功し、新しい失敗はない。Doom は S0 と同じ次の 2 ケースのみ失敗した。

- `DoomWebGpuProvider_SeparatesRawFramebufferAndHudCompositeTargets`
- `DoomPrompt_UsesCanonicalAisthesisNoesisKrisisKinesisPanelMapping`

いずれも `HUD_UNIFORM_FLOAT_COUNT` の期待値が 32、実装が 36 であることによる失敗。期待値も実装も変更していない。Doom のネイティブ WASM ビルドは既定どおり無効の、通常の `dotnet build/test` による確認。

検証用パッケージの DLL、今回ビルドした DLL、独立 NuGet キャッシュの DLL、下流の出力 10 箇所の DLL が同じ SHA-256 であることも確認した。検証用 DLL の SHA-256 は `540ea9537e7c2b7273bf8a00f953f80bbb488aec33b2ac9a0240a90b5057d771`。

ローカルのログ・TRX・検証用 feed・集計スクリプトは `D:\AIKernel\.codex_tmp\review-ws2-20261111` に保存した。

## 残る判断

S0 で記録済みの Wasm `src/Compute/WebGpuComputeProvider/IWebGpuBackend.cs` の別の部分文字列判定は、今回の Providers の WS2 には含めない。対応範囲の追加は人間の判断が必要。

ブランチのレビューと `dev` への取り込みは人間が行う。▲ の未承認項目を除くと、次の作業候補は B1。
