# Cosmos 0.1.4-dev10: 停止確認の保存失敗後も物理処理情報を保持

2026-11-12 / AIKernel.Providers / review/COSMOS-P0。dev9コミット77a3390への追補。

## 再現と修正

ConfirmBackendQuiescentは保存前に全Job.Entry.PhysicalOutstandingをfalseへ変更していました。
保存失敗で隔離だけは戻る一方、メモリのフラグは戻らず、後のRevokeRequestAuthorizationや遅い取消応答が
falseの記録を台帳へ保存しました。その状態で再起動すると物理処理の未確認情報が失われます。
保持期限を過ぎた記録は再起動時に除去される可能性もあります。

この問題は16ケースで再現し、修正前はすべて失敗しました。複数記録のケースでは、2件の未確認フラグが0件になりました。
新規試験だけを追加し、既存346件の期待値は変更していません。

修正は解除後の記録を別配列として準備し、その台帳を保存してからメモリへ反映する方式です。
保存失敗時に元のJob.Entryを変更しないため、通常のPersistや背景の保存も元の物理処理フラグを保持します。
同じサービスロック内で保存と反映を行い、保存後はホストのコールバックを呼ばず参照・状態を代入します。
backend identityとreadinessも保存前に準備します。稼働中の拒否と、保存失敗時のjournal_unavailableは維持します。
成功時だけPhysicalOutstandingを解除し、readinessをprobe_requiredへ戻します。既存要求の推論を自動再実行しません。

公開DTO、JSON Schema、台帳v1形式、hash計算式、既存5パッケージの版は変更しません。
停止確認による台帳の物理フラグ変更は意図した状態更新です。解析結果JSONとOutputHashは同一であることを検証します。

## 新規自動検証

SceneRecoveryPersistenceTestsに19件追加しています。

| 検証 | 件数 |
|---|---:|
| failed/canceled/timed_out × 同一サービス/再読込み × 保持期限内/期限後。保存失敗後の後続操作・再保存・二度の再起動 | 12 |
| 遅い取消応答confirmed/unsupported/独自値が書き込まれてもフラグを保持 | 3 |
| 未確認2件と正常完了1件の混在。完了候補の失効保存で他記録のフラグを消さない | 1 |
| 保存失敗後の明示再試行成功。結果/OutputHash保持、二度の再起動、新規要求だけ実行 | 2 |
| 稼働中の停止確認を拒否し、台帳と物理枠を変更しない | 1 |

障害注入は、試験専用ディレクトリの台帳を一時的なディレクトリ障害で書込み不能にする方法です。
OS固有のファイル共有ロックには依存しません。取消応答はTaskCompletionSourceで順序を固定し、sleepのタイミングに依存しません。
例外はCode/Message/InnerException/Dataまで確認し、運用エラーの情報制限を維持します。
隔離中のメディア登録・新規解析が拒否され、バックエンド呼出し回数が増えないことも検証します。

Cosmos全365件が成功しました。全体・下流は **850件成功、S0既知失敗2件、新規失敗なし**。全7リポジトリのrestore/buildが成功しました。
証跡はD:\AIKernel\.codex_tmp\cosmos-recovery-persistence-20261112と配布ZIPのevidenceへ保存します。

## 制限・引き渡し

これは保存APIが失敗した場合と、後続書込み・サービス再作成の自動検証です。実機の電源断試験ではありません。
台帳破損・手動削除からの回復や、ホストによる実際のGPU停止確認を代替しません。
実機Cosmos B01〜B06・TheoriaCast共同受入は未実施です。外部映像送信・配信・モデル/GPU・取引操作は行いません。
Cosmosのみ0.1.4-dev10、既存5パッケージは0.1.3-dev1のままです。以前のdev1〜dev9配布物を維持します。
push・PR・dev取り込み・公開は行いません。

## 確定した全体検証

| Repository | build | 成功 | 既知失敗 |
|---|---|---:|---:|
| Providers | 成功 | 485 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 42 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 64 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |

下流devのHEADを確認し、同じdevの隔離コピーでbuild/testを再実行しました。下流の作業ブランチ・未コミット変更は変更していません。Doomの既知失敗はHUD定数32/36の不一致による同じ2件です。

7 JSON Schemaと2例に生成差分はありません。nupkg内DLLは全体試験で使ったDLLと一致します。独立サンプルのlocked-mode restore/runが成功し、lockのSHA-512とnupkgが一致しました。Cosmosのみdev10、既存5パッケージはすべて0.1.3-dev1です。

nupkg SHA-256: `e002119407d2e07982cf267868eb9408cfca0059d36b7f9a71afbcc4eb58a60e`。
配布ZIP: `D:\AIKernel\artifacts\AIKernel.Cosmos-P0-dev10-20261112.zip`。同梱SHA256SUMS.txtで全ファイルを照合できます。
