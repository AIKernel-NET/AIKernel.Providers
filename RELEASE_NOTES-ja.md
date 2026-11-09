# AIKernel.Providers リリースノート

[English](RELEASE_NOTES.md)

## 0.1.3（公開準備中）

- 公式 Provider パッケージを AIKernel.NET・Core・Control の 0.1.3 に揃えます。接続先、ドライバー、任意のネイティブランタイムは Core の外に置きます。
- `aikernel-providers` の同梱検証には Compute を含め、対応する DLL の読み込みと Provider の登録を確認します。CUDA のネイティブ実行は利用側が明示的に有効化します。

## 0.1.2

**2026年6月16日 - Provider substrate package line。**

AIKernel.Providers 0.1.2 は、AIKernel.NET / AIKernel.Core 0.1.2 に揃えた provider substrate と runtime-configurable provider family を公開します。

- provider substrate、standard、audio、compute、perception、council、chat、local LLM、Microsoft AI、CUDA descriptor、dynamic pipeline package を公開します。
- Providers は SDK wrapper ではなく capability adapter として維持します。
- deterministic router と manifest behavior を維持します。
- 同期 Python wrapper 資料を 0.1.2 release flow で公開します。
