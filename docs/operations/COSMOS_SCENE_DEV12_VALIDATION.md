# Cosmos scene dev12 — 停止済みインスタンスと台帳所有権

検証日: 2026-11-12 (JST)。対象: AIKernel.Providers、review/COSMOS-P0。
修正前: c91893485c5a3359069c54a30593206cb8138278 (dev11)。実Cosmos・GPU・配信・取引操作は行いません。

## 再現と改修

dev11で未改修として報告した停止済みConfirmBackendQuiescentを調査しました。
旧インスタンスがwriter.lockを解放した後にも、手元の古いジョブ一覧でjobs.jsonを再保存できました。
新しいインスタンスのrunning/隔離中の記録が消え、PhysicalOutstandingの根拠が失われることを再現しました。
内部台帳のClose反復も新所有者のrecovery-requiredを削除でき、Load/Saveに閉鎖ガードがありませんでした。
停止済みprobeがdeploymentVerified=trueを残す、進行中probeが停止後にready/unavailableを返す、
重複DisposeAsyncが物理処理の終了前に完了する経路も確認しました。

修正前の26件は23件失敗/3件成功。既存389件の試験コード・期待値は変更していません。
停止完了失敗の共有、ホスト情報取得中の停止再入、停止準備の取消タスク状態も追加し、新規試験は計32件です。

- サービスの停止確認・利用者向け入口を停止済みチェックで保護し、旧インスタンスから再保存しない。
- 認可やホスト情報取得の後にも停止を確認し、遅い応答で停止済み状態を復活させない。
- 停止済みprobeはdisabled、deploymentVerified=false、retryAfterMs=null。取消例外は意味と秘匿を維持。
- 停止開始確定後は同じTaskで物理処理の終了とクローズを共有し、成功/失敗を反復呼出しで変えない。
- 内部台帳のLoad/Save/Close/Disposeを同じロックで保護。閉鎖済みLoad/Saveは拒否し、反復Close/Disposeは無操作。
- 列挙中に所有権を手放す再入があっても、書込み前の再検査で新所有者への上書きを防ぐ。

停止準備のホストタイマー例外は従来どおり即時に秘匿して返し、停止未確定としてwriter.lockを維持します。
物理処理が未完了のまま新インスタンスへ所有権を移す変更はしません。バックエンド/モデル自体を自動停止・破棄しません。
停止後の新規セッション/能力/メディア要求はdisabledとなります。RevokeRequestAuthorizationの識別子検証順序は維持します。
正常時のScene v1、jobs.json v1、recovery-required形式、公開DTO、hash式、エラー許可リストは変えません。

## 新規自動検証

SceneDisposedInstanceTests 25件、SceneJournalOwnershipTests 7件。

| 検証 | 件数 |
|---|---:|
| 旧サービス正常/隔離 × 新所有者running/隔離、旧停止確認による再保存を拒否 | 4 |
| 停止済みの確認/失効/セッション/解析/照会/取消/世代失効/能力情報/メディア入口、ホストコールバックなし | 9 |
| 以前readyだった停止済みprobeの配置承認を除去 | 1 |
| 進行中probeの正常/障害/取消/Task取消が停止後に戻る | 4 |
| 重複停止が同じ物理処理の終了とロック解放を待つ | 1 |
| クローズ失敗を反復停止で成功へ変えず、新所有者に後片付けを行わない | 1 |
| 停止確認/probeのidentityコールバックから停止が再入する | 2 |
| 停止準備の通常例外/取消/Task取消のタスク状態・秘匿とwriter.lock維持 | 3 |
| 内部台帳のClose/Dispose × 停止後Save/Load/Close | 6 |
| スナップショット列挙中の閉鎖と新所有者への移譲 | 1 |

実際の試験用ローカル台帳とwriter.lockを使い、jobs.jsonと補助記録の内容が変わらないこと、
新所有者の物理フラグと隔離を維持すること、第三の書込者がロックを取れないことを検証します。
TaskCompletionSourceでprobe競合の順序を固定します。実バックエンドやユーザーの台帳は使いません。
停止準備の取消タスク状態を保つ最終調整後、Providersはrestore/build/testを全体で再実行しました。
初回のProviders証跡はinitial-providersへ保持し、最終集計は再実行の結果を使います。

Cosmos全421件が成功しました。全体・下流は **906件成功、S0既知失敗2件、新規失敗なし**。全7リポジトリのrestore/buildが成功しました。
証跡: D:\AIKernel\.codex_tmp\cosmos-disposed-instance-20261112、および配布ZIPのevidence。

## 制限・次の確認事項

実機Cosmos B01〜B06、TheoriaCast共同受入、実機の電源断は未実施です。
dev11の補助記録を含むディレクトリ単位の保全と、補助記録がある状態での旧版への切戻し禁止を維持します。
Cosmosのみ0.1.4-dev12、既存5パッケージは0.1.3-dev1のまま。dev1〜dev11の配布物を維持します。
push・PR・dev取り込み・公開は行いません。

別件（コード確認、未再現）: ProbeAsyncの障害処理内でもホストのGetUtcNowを呼ぶため、時計実装がそこで例外を出す場合の
例外詳細の秘匿を次項目で確認する必要があります。今回は停止済みの運用APIと所有権の修正に限定し、未改修とします。

## 確定した全体検証

| Repository | build | 成功 | 既知失敗 |
|---|---|---:|---:|
| Providers | 成功 | 541 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 42 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 64 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |

下流devのHEADを確認し、同じdevの隔離コピーでbuild/testを再実行しました。下流の作業ブランチ・未コミット変更は変更していません。Doomの既知失敗はHUD定数32/36の不一致による同じ2件です。

7 JSON Schemaと2例に生成差分はありません。nupkg内DLLは全体試験で使ったDLLと一致します。独立サンプルのlocked-mode restore/runが成功し、lockのSHA-512とnupkgが一致しました。Cosmosのみdev12、既存5パッケージはすべて0.1.3-dev1です。

nupkg SHA-256: `b2531a2bf25eecbe0cea433e285a2f5bd979b67185c321af67988aa9bafca5d7`。
配布ZIP: `D:\AIKernel\artifacts\AIKernel.Cosmos-P0-dev12-20261112.zip`。同梱SHA256SUMS.txtで全ファイルを照合できます。
