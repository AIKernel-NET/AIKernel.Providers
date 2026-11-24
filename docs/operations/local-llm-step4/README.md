# LocalLlm 0.3.0 Step 4　Qwen3-0.6B のCPU受け入れ報告

2026-11-25。Windows x64 のAVX2環境でCPUワーカー4本を使用した。Coreの検証対象は `e089ee400e75c42ca5bad1b91b7399eb87170773`。Step 3は両リポジトリのGitHub `dev` に統合し、Core 1,717件、Provider 22件、Bonsai日英、および統合前後の品質指標763件の一致を再確認した。Step 4の成果はローカル開発ブランチ `codex/localllm-030-step4` に保存している。

## 読み込みから生成まで

Qwen3-0.6BをFP32 ONNX、単一BF16 SafeTensors、2分割BF16 SafeTensors、2分割FP32 SafeTensorsの4通りで開いた。最後の2通りは公式の単一ファイルを再配置し、FP32化ではBF16の値をそのまま拡張した。4通りとも同じ基底モデル、プロファイル形状、トークナイザー、演算経路を使う。Provider の `chat.local` と Kernel の既存の登録APIで呼び出せる。

ONNXは `onnx-community/Qwen3-0.6B-ONNX@79346d370dfae34801d26993fda9ae2372900fd0` のFP32グラフを対象とする。グラフ全体のSHA-256が辺、演算子、属性と動的軸を固定し、外部初期化子のファイルも独立に照合する。SafeTensorsは `Qwen/Qwen3-0.6B@c1899de289a04d12100db370d81485cdf75e47ca` を直接読む。各形式の設定・トークナイザー・重みを同じファイルハンドルで照合してから、共通の行優先FP32の重みへ取り込む。学習済みテンソル310個の全要素は、向きと表現をそろえると一致した。ONNXに固有の2配列はRoPEのsin/cos表である。[各テンソルの最大差と不一致要素数](weight-comparison.json)

## 公式グラフをONNX Runtimeで実行した結果との照合

ONNX Runtime 1.32.0、CPUExecutionProvider、greedy、最大16生成トークンで照合した。各生成位置の全151,936ロジットを比較した。トークン数はEOSを含む。4形式間で、全17位置のFP32ロジット配列がバイト単位で一致した。ONNX Runtimeに対しては次の誤差を観測した。

| 入力 | トークン数 | 最大絶対誤差 | 各位置のRMS誤差の最大 | 応答本文SHA-256（先頭12桁） |
|---|---:|---:|---:|---|
| 英語・フランスの首都 | 8 | 5.4836273e-05 | 1.5745948e-05 | `a1b7eb2ee7a6` |
| 日本語・日本の首都 | 9 | 5.0544739e-05 | 1.293747e-05 | `42e3513e5b7b` |

両言語で生成ID、EOSの位置、通常生成の本文とそのUTF-8ハッシュが参照と一致した。最大絶対誤差の基準は0.001、RMS誤差の基準は0.0001。英語の本文は「The capital of France is Paris.」。日本語で参照が返した「日本の首都は、大阪市です。」は事実誤認であり、検証が示すのは同じモデルの応答を再現したことまでである。

プロンプト中の各位置を追跡すると、埋め込みと参照の最大絶対誤差は両言語とも0で、測定した演算で最初の差は第0層の入力RMSNorm（最大 1.9073486e-06）に現れた。その直後のQ射影の最大差は 8.5830688e-06 だった。各位置の数値、生成ID、AIKernelと参照の全語彙ロジットのSHA-256を [acceptance.json](acceptance.json) に記録した。

## 登録失敗・取消しと既存利用者

- 未知のONNX演算子、トークナイザー設定の欠落、2番目のシャードの欠落・破損をそれぞれ拒否した。さらに1バイト異なる重みに合わせてホストのロックと登録ファイルを両方更新した場合も、公式リビジョンのテンソル照合で拒否した。[異常系の実測](negative-cases.json)
- ONNXとSafeTensorsの双方で、誤った登録を渡したProviderは失敗を返し、正した後に同じinvokerで応答した。4形式すべてで推論中の取消し後に同じ直接利用セッションで正しい生成を再開できた。
- Core 1,722件、Provider 24件が合格。旧型の署名照合も合格。公開版でコンパイルした利用コードを公開DLLと候補DLLで各11件実行し、双方で合格した。Bonsai 1.7Bの日英固定16トークンと通常本文のハッシュは従来どおりだった。
- Gemma 4 E2Bの12トークン、公式数値照合228件、従来AVX2ダンプ229件が一致または既存基準内。GPT-OSS-20Bはホストの7,000,000,000バイト枠で2例ともllama.cppの入力・生成に一致した。
- Roslyn 5.6.0の固定基準763件と新規メソッドの閾値はすべて合格。ProviderアセンブリはMI 85、CC 124、結合型数 68。従来の測定対象外だったBPE・トークナイザー2型に今回追加・変更した6メソッドも同じ新規メソッド基準で照合した。

## 再現

1. 両リポジトリで検証コミットを選び、上記リビジョンのモデルをスナップショットごとに用意する。必要なファイルとハッシュは `acceptance.json` の `registeredSnapshots` に収録した。
2. Coreの `tests/AIKernel.Inference.Optimization.Validation/registered_snapshot_reference.py <ONNXスナップショット> <参照出力先>` をPythonで実行する。参照側は `onnxruntime`、`onnx`、`numpy`、`tokenizers` を使用する。AIKernelによる推論はこのPython環境から独立している。
3. 同じディレクトリの `repack_registered_snapshot.py <SafeTensorsスナップショット> <新規出力先> <SafeTensors登録JSON>` が、2分割の重み・インデックス・ロック・登録ファイルを作る。`--fp32` で全テンソルをFP32に拡張できる。
4. 上記C#検証プロジェクトをReleaseでビルドして、`dotnet AIKernel.Core.Tests.dll --registered-snapshot <重みまたはインデックス> <登録JSON> <参照出力先> <測定出力先>` を各形式について実行する。登録JSONと配置例はProviderの `src/Llm/LocalLlmProvider/` とCoreの `docs/local-model-registration-0.3.md` にある。
5. Providersリポジトリのルートで固定基準と今回の測定を比較する。

```console
python tools/check_localllm_metrics.py --baseline-step 3 --moves docs/operations/local-llm-step4/responsibility-moves.tsv docs/operations/local-llm-step4/core.tsv docs/operations/local-llm-step4/providers.tsv docs/operations/local-llm-step4/tokenizer.tsv
```

GPU・Linux実機、NuGet公開、プリフェッチは後続工程で扱う。

## Step 5 の固定基準

統合したStep 4のCore・Providers・トークナイザーの測定を再実行し、全行のMI、CC、結合型数が一致した。従来の763シンボルを含む911シンボルを [quality-baseline.tsv](quality-baseline.tsv) に固定する。Providerの集合値はMI 85、CC 124、結合型数68。比較スクリプトの既定はStep 4。
