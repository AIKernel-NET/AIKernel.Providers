# Cosmos 0.1.4-dev9: ホスト運用APIのエラー制限

2026-11-12 / AIKernel.Providers / review/COSMOS-P0。dev8コミット1d9dd34への追補。

## 確認と修正

停止確認、権限失効通知、終了処理で、backend identity・時計・timer・HTTP handler由来の例外がそのまま返りました。
配置証跡の読込みではファイルパスを含むI/O例外、直接NIM probeでは内部例外を含む取消が返りました。
合成の機密マーカーを使った26ケースは、修正前にすべて失敗、修正後にすべて成功しました。
実際の資格情報、映像、ネットワーク接続、GPU操作は使用していません。

SceneHostErrorsに9種類の完全一致許可リストを追加しました。未知の運用失敗はhost_operation_failedです。
台帳初期化はjournal_unavailable、配置証跡の読込みはdeployment_evidence_invalidへ集約します。
新しい例外へ置き換え、元の本文・内部例外・Data・Task・内部取消トークンを保持しません。
SceneExceptionのWorkMayStillBeRunningは安全判断のため保持し、取消は取消型で返します。
トークンを受け取るAPIは呼出元トークン、受け取らない同期API/DisposeAsyncはNoneとします。
モデル設定・配置ファイルパスのArgumentExceptionは固定文にし、既存の引数拒否を維持します。

停止確認はidentity取得後に隔離を解除し、取得失敗で既存の物理処理フラグを保存から消しません。
サービス初期化のidentity取得を台帳lease取得前へ置き、読込み失敗時にはleaseを解放します。
既知エラー、取消/終端状態、台帳失敗時の隔離、再試行後の既存機能は維持します。
ホストの操作が例外になった場合、その操作の停止/失効/復旧完了を仮定しない契約です。

## 対象API

| API | 確認内容 |
|---|---|
| SceneService constructor | identity例外、台帳I/O・不正台帳、失敗後の再作成 |
| ConfirmBackendQuiescent | 独自コード・例外詳細除去、隔離保持、既知エラー/物理フラグ維持 |
| RevokeRequestAuthorization | 時計例外、無効ID・未登録ID・終了後、再試行の候補失効 |
| DisposeAsync | timer例外を固定化。物理完了待機・既存の失敗処理を維持 |
| SceneDeploymentFileSource | 設定パス、欠落/不正/巨大JSON、正常読込み、呼出元取消 |
| CosmosNimBackend | 設定コレクション、handler破棄、直接probe取消 |
| OpenSession / SceneSession / Service.ProbeAsync | dev7/dev8の保護を既存回帰試験で維持 |

推論プロバイダー呼出しはSceneServiceの既存境界を通します。DTOのシリアライズ/hash補助関数をホスト運用APIへ変更しません。
公開済みv0.1.3の型、Scene v1 JSON Schema、台帳形式、hash計算式は変更しません。
host_operation_failedはホスト専用。受付・ジョブ用の許可リストを広げません。

## 自動検証

新規SceneHostErrorTestsは48件。既存298件の期待値を変更していません。

| 検証 | 件数 |
|---|---:|
| 停止確認の未知/改変/空/null/巨大/既知コード・通常/I/O/取消例外 | 12 |
| 権限失効通知の時計例外と安全な再試行 | 5 |
| 終了時timer例外 | 3 |
| 配置証跡の私的パス除去 | 1 |
| NIM handler破棄例外 | 3 |
| 直接NIM probeの取消例外 | 2 |
| 初期化identity失敗とlease非保持 | 5 |
| 不正台帳の拒否・lease解放 | 2 |
| 台帳初期化のパス/多重writer拒否 | 2 |
| 停止確認の保存失敗と再起動後の隔離 | 1 |
| 既知ホストエラーコード | 3 |
| 不正JSON・重複キー・巨大配置証跡 | 3 |
| 正常配置証跡とリンクされた取消 | 1 |
| 配置パス設定の固定エラー | 2 |
| NIMの設定コレクション例外 | 3 |

修正直後は324件、試験拡張後は346件成功しました。新規試験の取消引数に関するanalyzer警告を解消しています。
台帳保存失敗の新規試験はOSの共有ロック動作に依存しないディレクトリ障害で検証します。
Cosmos全346件が成功しました。全体・下流は **831件成功、S0既知失敗2件、新規失敗なし**。全7リポジトリのrestore/buildが成功しました。
証跡はD:\AIKernel\.codex_tmp\cosmos-host-errors-20261112と配布ZIPのevidenceへ保存します。

## 引き渡し・未実施

パッケージは0.1.4-dev9。既存5パッケージは0.1.3-dev1に固定します。以前のdev1〜dev8配布物は維持します。
実機Cosmos B01〜B06・TheoriaCast共同受入は未実施です。外部映像送信、配信・モデル・GPU・取引操作は行いません。
push・PR・dev取り込み・公開は行いません。ホストはSDK例外のスタックトレースを公開応答へコピーしないでください。

## 確定した全体検証

| Repository | build | 成功 | 既知失敗 |
|---|---|---:|---:|
| Providers | 成功 | 466 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 42 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 64 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |

下流devのHEADを確認し、同じdevの隔離コピーでbuild/testを再実行しました。下流の作業ブランチ・未コミット変更は変更していません。Doomの既知失敗はHUD定数32/36の不一致による同じ2件です。

7 JSON Schemaと2例に生成差分はありません。nupkg内DLLは全体試験で使ったDLLと一致します。独立サンプルのlocked-mode restore/runが成功し、lockのSHA-512とnupkgが一致しました。Cosmosのみdev9、既存5パッケージはすべて0.1.3-dev1です。

nupkg SHA-256: `af85f0b36efb415732384042e75549449baab5ac8925caa14a5daa2c477e6edc`。
配布ZIP: `D:\AIKernel\artifacts\AIKernel.Cosmos-P0-dev9-20261112.zip`。同梱SHA256SUMS.txtで全ファイルを照合できます。
