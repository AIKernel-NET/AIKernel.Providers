# LocalLlm 0.3.0 Step 2 品質指標・例外承認記録

2026-11-24 に著者が、Bonsai 1.7B Q1_0 を共通テンソル・常駐管理へ接続した Step 2 の統合を承認した。この報告を含む Providers コミットと、対になる [Core コミット](https://github.com/AIKernel-NET/AIKernel.Core/commit/fe829bda7a6acfaf3874b32bb97e6f9b524c0693) が対象となる。

| 対象 | Step 1（MI / CC / 結合型数） | Step 2 |
|---|---:|---:|
| LocalLlmProvider アセンブリ | 87 / 112 / 56 | **85 / 125 / 68** |
| LocalLlmInvoker 型 | 61 / 26 / 33 | 62 / 26 / 32 |
| Qwen3CpuModel 型 | 56 / 266 / 84 | 57 / 244 / 79 |
| GgufWeights 型 | 70 / 44 / 28 | 74 / 43 / 26 |

例外はアセンブリ総計の1行に適用する。ホスト登録設定、登録済みリーダー、応答結果の構築を追加したため、アセンブリ全体の規模・結合が増加した。Step 1 と照合した593行のうち、他の592行は既存シンボルの基準または新規メソッドの基準を満たした。承認者は原指示書の著者、承認日は2026-11-24。[著者の承認記録](https://drive.google.com/file/d/1kx3fwt3XYGDJ0jQDv9ZtCqv-oqTYoFiF/view)

## 次回の品質判定

Step 2 の実測値を次回の基準とする。アセンブリ総計は MI 85 以上、CC 125 以下、結合型数 68 以下。既存シンボルはそれぞれ MI の低下・CC と結合型数の増加を検出する。新規メソッドは MI 60 以上、CC 10 以下、結合型数 10 以下。シンボルを移動した場合は旧責務との対応を記録して比較する。[593行の基準表](quality-baseline.tsv)

Microsoft.CodeAnalysis.Metrics 5.6.0 の同じ測定条件で取得した Core と Providers の TSV を使い、Providers のルートで比較する。列順は種別、完全修飾シンボル名、MI、CC、結合型数。測定器が出力する6列目のソース行数も受理する。

```console
python tools/check_localllm_metrics.py core.tsv providers.tsv
```

## 実機で照合した範囲

Windows x64、.NET SDK 10.0.404、Bonsai 1.7B Q1_0、CPU AVX2 で確認した。公開版 0.1.6 の既存10件と追加11件が通り、日英の固定16トークンの ID 列 SHA-256 と通常生成の本文 SHA-256 が公開版に一致した。64 MiB の重みキャッシュでもメモリマップ経路に一致した。Core 全1,709件が合格した。[検証報告](https://drive.google.com/file/d/1oz7C5-pgXfkdbrMg78ohGaYjhQRECkJD/view) · [試験・メトリクス明細](https://drive.google.com/file/d/15Sgr3v7k3gBL5HuWdzczWwduGDBqgibz/view)

正式 Kernel パッケージに必要な Windows／Linux のネイティブ資材は 0.3.0 公開時にステージングする。
