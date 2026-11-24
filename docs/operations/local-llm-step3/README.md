# LocalLlm 0.3.0 Step 3 の受け入れ結果

対象は .NET 10 の開発ブランチ。Core は `cadea595e0e21a7899f85f36fe856b807f0a415a`、Providers はこの報告を格納するコミット。公開済みの LocalLlmProvider 0.1.6 と Kernel 0.1.4 を独立した試験ディレクトリに置き、Windows x64 の CPU で比較した。

| 確認項目 | 結果 |
| --- | --- |
| Provider | 既存10件、Step 2の11件、継承した登録での拒否・復旧1件の計22件が合格 |
| Core | 1,717件合格。プロファイル解決、未知キー・循環、MoEの同点順序、重みの単一予約、並行リクエスト後のKVとページ解放を含む |
| Bonsai 1.7B | 日英とも固定16トークンID列と通常応答のUTF-8本文のSHA-256が公開版に一致 |
| コンパイル済み利用者 | 公開版に対してビルドした試験アセンブリで、公開DLLと候補DLLの双方が11件合格 |
| Kernelの直接利用 | 公開版0.1.4のデバイス別5型が持つ公開コンストラクター・メソッドの署名54件を、継承したメソッドも含めて候補DLLで確認。Step 2で追加した旧Openは別途、既存のページング試験で実行 |
| Gemma 4 E2B | 新しい登録APIで参照の12トークンに一致。公式参照との228件の数値比較が合格。最大絶対誤差0.0008544921875、ロジット最大絶対誤差0.00009965896606445312。以前のAVX2版の229個のFP32ダンプとバイト一致 |
| GPT-OSS-20B | 新しい登録APIと上限7,000,000,000バイトのホストページングで、算術と首都名の2例ともllama.cppの入力列・生成列・終了条件・本文に一致 |
| Roslyn品質指標 | 既存593シンボルと新規メソッドの閾値を通過。ProviderアセンブリはMI 85、CC 125、結合 68。今回の例外は0件 |

全試験でスキップは0件。Providerの既存10件のうち直接比較には従来の `new Qwen3CpuModel(...)` が残る。Step 2のページング比較も旧 `Open(...)` を通す。利用者の `chat.local` を通る側は新しいセッションを開く。固定16トークンの比較と、通常の終了条件での本文の比較は別々に記録した。[ハッシュと実測結果](acceptance.json)

## 移行した責務

| 旧型・入口 | 0.3の役割 |
| --- | --- |
| Qwen3CpuModel | DenseModelSession が生成・プライベートKVの寿命、DenseAttentionKernel が演算、登録プロファイルが形状・重み・tokenizerの照合を担当 |
| Qwen3GpuModel | Kernel が予算とデバイスを確認し、登録したDenseグラフにCUDA行列実装を配置。旧型は明示的な装置選択と従来の結果型を維持 |
| Qwen3VulkanModel | 同じDenseグラフでKernelがVulkan候補を確認。デバイス固有のメモリ確保と実行はVulkanカーネルに置く |
| Gemma4E2BCpuModel | GemmaTextSession、E2Bプロファイル、層別入力・KV所有者の定義、専用行列カーネルに分離 |
| GptOss20BCpuModel | MixtureModelSession、20Bプロファイル、RoutingPlan、HarmonyChatTemplate、共有のページ管理に分離 |
| DenseModelRegistration.Open | 旧シグネチャを維持。新しいOpenSessionでKernelに配置を依頼 |

0.3系では5型と旧Openに警告を表示し、0.4系で削除予定。直接利用の場合の移行例はCoreの `docs/local-model-registration-0.3.md` に収録した。今回の実機照合はCPU経路で行った。CUDA・Vulkanの実機実行、Linux向けネイティブ資材とNuGet配布は別の公開ゲートで確認する。

## 品質判定の再現

Providersリポジトリのルートで実行する。対応表は旧シンボルと移行先を1対1で結び、Step 2で承認された閾値を適用する。

```console
python tools/check_localllm_metrics.py --moves docs/operations/local-llm-step3/responsibility-moves.tsv docs/operations/local-llm-step3/core.tsv docs/operations/local-llm-step3/providers.tsv
```

[責務対応表](responsibility-moves.tsv) · [Core測定](core.tsv) · [Providers測定](providers.tsv) · [Step 2の固定基準](../local-llm-step2/quality-baseline.tsv)
