# B4: Provider境界のキャンセル判定

検証日: 2026-11-12〜13 (JST)。dev `db7e989` から作成した `review/B4`。

OpenAICompatibleProviderの取消判定を、Core側で追加した
`AIKernel.Common.Results.CancellationExceptionClassifier` に集約します。
OperationCanceledException / TaskCanceledException / 独自派生型 / リンク済みトークンを扱い、
元の例外とトークンをそのまま伝播します。通信エラーとTimeoutExceptionの既存のラップを維持します。

ProviderManifestLoaderのcatch/rethrowだけの取消節は削除します。
捕捉対象はIO等の例外だけなので、取消は自然に伝播します。SubstrateへCommon依存を追加しません。

回帰テストを12件追加。既存テストの期待値は変更していません。
ProvidersはRelease build成功・132件成功。全.NETはbuild成功、1,185件成功、Doom既知失敗2件です。
全体の手順・結果はAIKernel.Coreの `docs/operations/REVIEW_B4-v0.1.3.md` にまとめています。
証跡は `D:\AIKernel\.codex_tmp\review-b4-20261112` です。

Core / Providers / Control / Wasmのreview/B4は一緒にレビューし、0.1.4候補ではCommonと消費側の
版・依存下限を揃えてください。新しい消費側DLLを公開済み0.1.3 Commonだけで動かすことはできません。
検証用のローカル0.1.3再packは配布物ではありません。

レビュー待ちのreview/COSMOS-P0は保持し、このdevからの変更に混ぜていません。
Cosmosを取り込んだ後の統合検証は別途必要です。
dev/mainへの取り込み、push、PR、タグ、公開は行いません。
