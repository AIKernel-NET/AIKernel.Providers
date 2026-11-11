# Cosmos P0 ローカル検証・引渡し記録

日付: 2026-11-11。対象: AIKernel.Providers / review/COSMOS-P0。
基点: dev `db7e989`。新パッケージ `AIKernel.Providers.Cosmos 0.1.4-dev1`。
Windows / .NET SDK 10.0.401 / net10.0。Linux と実 GPU の試験は未実施。

## 結果

**新規 Cosmos 自動試験 69 件成功。既存と下流を合わせ 554 件成功、S0 既知失敗 2 件。新しい失敗なし。**
Providers と下流6リポジトリの restore/build はすべて成功した。
既存試験の期待値は変更していない。Core/Control の未マージ CT1 や Providers の WS2 を本ブランチへ混ぜていない。

| リポジトリ | 対象 | build | テスト成功 | 失敗 |
|---|---|---|---:|---:|
| Providers | devからの今回変更 | 成功 | 189（既存120＋Cosmos69） | 0 |
| Cuda13.0 | dev 9e3ef826 | 成功 | 21 | 0 |
| Control | dev 2229477a | 成功 | 42 | 0 |
| Tools | dev 05ec0216 | 成功 | 34 | 0 |
| Wasm | dev 2c6b11f4 | 成功 | 64 | 0 |
| Demo | dev b3fcb620 | 成功 | 71 | 0 |
| Doom | dev 3df7008d | 成功 | 133 | 2（既知） |

下流は `git archive dev` の隔離コピーを使い、各リポジトリの人間の作業ブランチ・未コミット変更を維持した。
未変更の NET/Core ソースを改修対象として再ビルドしたものではない。依存 NuGet は元のキャッシュから取得した。
新機能は optional package のため、下流は従来の依存版のまま回帰検証した。
Providers 全体試験後の最終変更は Cosmos 内に限定し、その最終ソースで69件を再実行した。

Doom の既知失敗は以下。両方とも `HUD_UNIFORM_FLOAT_COUNT = 32` の期待と現在の36の不一致で、S0 と同じ。

* DoomWebGpuProvider_SeparatesRawFramebufferAndHudCompositeTargets
* DoomPrompt_UsesCanonicalAisthesisNoesisKrisisKinesisPanelMapping

## 新規試験の証跡

* SceneAcceptanceTests: 公式型/独立Capability、入力と出力の拒否、冪等性/最新優先/取消、期限、認可、台帳、複数actor。
* SceneBoundaryTests: 時計巻戻し、未完了推論の再起動・保持期限、取消済み物理処理の永続化、時刻付き画像、policy変更、PNG破損、重複JSON、upload並列上限、処理前キャンセル。
* NimProtocolTests: 実transportクラスのHTTPを差し替えたmultimodal送信、length/content_filter/不完全応答、過負荷/接続断、GPU配置不一致、禁止IP、契約に反する配置識別子。

NimProtocolTests は実 GPU の証明ではない。モデルの応答品質、CUDA実行、p50/p95/p99、VRAM/CPU、Bonsaiとの同時稼働は測定していない。
実行ログ: `D:\AIKernel\.codex_tmp\cosmos-p0-20261111`。最終 Cosmos TRX は `cosmos-final.trx`。
既存/下流の各TRXは `results/`。同名Cosmos試験の初回と最終回を二重加算していない。

## 契約・パッケージの検証

1. 7 JSON Schema と2例を.NET公開型から生成。`ContractExport --check` で差分なし、全JSON構文を確認。
2. 0.1.4-dev1 nupkg の lib/net10.0、Schema、接続仕様、エラーコード、LICENSE を確認。
3. 新しいキャッシュへ nupkg を導入。既存5パッケージ Abstractions/Dtos/Enums/Common/Core はすべて0.1.3-dev1。
4. `--locked-mode` restore成功。リポジトリ内のパッケージ消費サンプルと、引渡しディレクトリの独立サンプルを実行。
5. offline-demo は `backend=offline-demo, isMock=true`。completed、canceled、observation_expired を確認し、動作は実行していない。

正式な変更内容・制限事項・A01〜A12対応表は `COSMOS_SCENE_P0.md`、コード一覧は `COSMOS_SCENE_ERRORS.md`。
TheoriaCast の実 AssistantObservationCapability はこの作業環境で接続試験していない。
従来のAPIを変更しないことと公式型・text回帰は確認済みだが、A01のTheoriaCast側統合部分は残る。
クライアントの二重採用防止、手動操作優先、実行停止はTheoriaCastの検証対象。

## 引渡し物

ディレクトリ: `D:\AIKernel\artifacts\cosmos-p0-20261111`

* `feed/AIKernel.Providers.Cosmos.0.1.4-dev1.nupkg` と独立サンプルに必要な依存のローカルフィード。
* `sample/`: .NET 10 の独立サンプル、固定lock、設定例。
* `docs/`: 契約・運用・対応表・検証結果・エラーコード。
* `schemas/`: 型から生成したSchema/説明用例。
* `evidence/`: オフライン試験ログ/TRX、下流devのhead、パッケージ導入ログ。
* `SHA256SUMS.txt`: 引渡しファイルのSHA-256。

新パッケージのSHA-256:
`0a4a52e8035992c4d8b549de62ae26a9e94a1ef9aa8af25a403ac7119a5ad468`

このパッケージはdev基点のローカル作業ツリーから作成したpreviewで、NuGetへ公開していない。
モデル重み、NIMコンテナ、TheoriaCast本体、番組素材は同梱していない。
フィード内の依存パッケージはそれぞれのライセンス/noticeを保持する。

```powershell
# 引渡しディレクトリから。ローカルフィードだけを参照する
dotnet restore sample/Cosmos.SceneClient.csproj --configfile NuGet.config --packages packages --locked-mode -p:NuGetAudit=false
dotnet run --project sample -c Release --no-restore -- --offline-demo
```

ソースの再検証:

```powershell
dotnet build AIKernel.Providers.slnx -c Release
dotnet test AIKernel.Providers.slnx -c Release --no-build
dotnet run --project tools/AIKernel.Cosmos.ContractExport -c Release -- src/Cosmos/AIKernel.Providers.Cosmos/schemas --check
```

## 残る受入作業

B01〜B06 はすべて未実施。今回、外部への映像送信、実モデルの起動/停止/置換、GPU配置、配信再起動、YouTube送出、取引操作を行っていない。
実測用の管理下接続先、モデル/runtime/image digest/GPU、使用許可のある匿名化入力が未確定である。
これらが決まってからB01〜B03の実推論・ルール比較・負荷測定を行い、B04 shadow接続へ進む。
B05の30分共同録画とB06のCosmosだけの無効化はTheoriaCast/Unity/OBS側と共同実施する。
実機で期限に収まらなければ動作採用を保留し、録画レビュー・入力削減・専用推論環境を比較する。

P0は統合用実装とオフライン検証を引き渡す段階であり、配信導入の受入完了ではない。
