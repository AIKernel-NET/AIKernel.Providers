# Cosmos scene dev13 — readiness障害処理の時計例外

検証日: 2026-11-12 (JST)。対象: AIKernel.Providers、review/COSMOS-P0。
修正前: acbdc2d09dc183129217d7680f1f46b86a1dc404 (dev12)。実Cosmos・GPU・配信・取引操作は行いません。

## 再現と改修

dev12で未再現として報告した、ProbeAsyncの障害処理内のGetUtcNow例外を再現しました。
通常例外や独自SceneExceptionがそのまま外へ出るほか、取消例外も固定本文への変換がなく、
Message/InnerException/Data/内部取消トークンが残る経路がありました。
時計の取得中に停止が再入してもunavailableを返す、進行中probeの失敗より先に隔離されても時計を呼ぶ経路も再現しました。
修正前の23件は18件失敗/5件成功。既存421件の試験コード・期待値は変更していません。

修正はProbeAsyncの障害処理に限定します。

- 障害処理内のGetUtcNowを例外境界で保護。通常例外・独自コードはunavailable/probe_failedに留める。
- 時計の取消は型とCanceledタスク状態を維持し、固定本文・呼出元トークンだけを返す。内部例外・Data・Taskは返さない。
- backend等の失敗確定時は、時計を読む前に過去の承認と時刻を無効化し、時刻取得不能でもreadyへ戻さない。
- 正常に失敗時刻を取得できた場合の既存JSONとhashは維持する。時刻取得不能後の照会ではprobe_requiredとなる場合がある。
- 主経路のprobe取消で既存readinessを維持する挙動は変更しない。障害処理内の取消では無効化した承認を戻さない。
- 時計取得の前後に停止・隔離を確認し、disabled/backend_work_unconfirmedを優先する。
- readonly probeは台帳、物理処理フラグ、復旧記録、新しいインスタンスのwriter.lockへ作用しない。

例外発生時の再試行は入れ子にせず、障害処理内の時計取得は一度です。主経路の時計も通常例外を出した場合は、合計二度になります。
公開DTO、Scene v1、jobs.json v1、recovery-required形式、hash式、理由コード許可リストは変えません。

## 新規自動検証

SceneReadinessClockErrorTestsに23件追加。

| 検証 | 件数 |
|---|---:|
| backend/identity失敗 × 時計の通常例外/独自コード/既知コード/取消/Task取消。秘匿・キャッシュ失効・正常probe復帰 | 10 |
| 主経路の時計例外4種類。取消の型・タスク状態・以前のreadinessを維持 | 4 |
| 最初の時計が失敗し、障害処理の時計は成功する既存経路 | 1 |
| 二回目の時計が取消/Task取消。呼出元トークンのみを保持 | 2 |
| 障害処理の時計から停止再入、正常復帰/例外。新所有者の台帳・ロックを維持 | 2 |
| probe待機中に隔離、時計が正常/故障。時計を呼ばず隔離・物理フラグを維持 | 2 |
| 停止済みprobeで壊れた時計を呼ばない | 1 |
| 通常backend障害のJSONとOutputHashを維持 | 1 |

試験用のTimeProvider・バックエンドで例外と再入を制御し、取消トークンの混入も検査します。
競合はTaskCompletionSourceで順序を固定し、実際の試験用台帳・補助記録の内容が変わらないことを確認します。
実バックエンド・ユーザーの台帳は使いません。

Cosmos全444件が成功しました。全体・下流は **929件成功、S0既知失敗2件、新規失敗なし**。全7リポジトリのrestore/buildが成功しました。
証跡: D:\AIKernel\.codex_tmp\cosmos-readiness-clock-20261112、および配布ZIPのevidence。

## 制限・引き渡し

実機Cosmos B01〜B06、TheoriaCast共同受入、実機電源断は未実施です。
dev11の補助記録を含むディレクトリ単位の保全と、補助記録がある状態での旧版への切戻し禁止を維持します。
Cosmosのみ0.1.4-dev13、既存5パッケージは0.1.3-dev1のまま。dev1〜dev12の配布物を維持します。
push・PR・dev取り込み・公開は行いません。
今回の範囲で新たな未改修問題は確認していません。次の工程は実機受入とTheoriaCast共同検証です。

## 確定した全体検証

| Repository | build | 成功 | 既知失敗 |
|---|---|---:|---:|
| Providers | 成功 | 564 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 42 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 64 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |

下流devのHEADを確認し、同じdevの隔離コピーでbuild/testを再実行しました。下流の作業ブランチ・未コミット変更は変更していません。Doomの既知失敗はHUD定数32/36の不一致による同じ2件です。

7 JSON Schemaと2例に生成差分はありません。nupkg内DLLは全体試験で使ったDLLと一致します。独立サンプルのlocked-mode restore/runが成功し、lockのSHA-512とnupkgが一致しました。Cosmosのみdev13、既存5パッケージはすべて0.1.3-dev1です。

nupkg SHA-256: `9d5b9ef51150506e0a78f32d974e9a18d5a0081a4df113c54bbedfdf5dc38175`。
配布ZIP: `D:\AIKernel\artifacts\AIKernel.Cosmos-P0-dev13-20261112.zip`。同梱SHA256SUMS.txtで全ファイルを照合できます。
