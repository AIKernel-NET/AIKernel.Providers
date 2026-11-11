# Cosmos 0.1.4-dev8: ジョブ・readinessの理由コード制限

2026-11-12 / AIKernel.Providers / review/COSMOS-P0。dev7コミット840bed3への追補。

## 修正

受付後のジョブErrorCodeには独自SceneException.Codeが保存され、readinessもバックエンドの独自理由コードを返していました。
probe取消例外と旧台帳を含む24ケースを追加し、修正前はすべて失敗することを確認しました。
試験の機密文字列は合成マーカーであり、実際の資格情報・映像・外部推論は使用していません。

SceneRuntimeErrorsにジョブ77種類・readiness25種類の完全一致許可リストを集約しました。
未知のジョブ理由はinvalid_backend_responseへ置換し、保存前と応答前、旧台帳の読込み時に適用します。
既知コードの記録は変更しません。不正コードがあった記録は候補を除去しますが、解析履歴・終端状態・期限・冪等性・物理処理フラグを維持します。
理由が不要な状態でのnullは維持し、失敗状態のnullは安全な理由へ置換します。

readinessの未知理由はprobe_failedです。既知の非ready状態は維持し、未知状態や不正なready主張はunavailableにします。
不正応答はdeploymentVerified=false、retryAfterMs=nullとなり、推論を開始しません。
readyはstate/理由ともreadyで配置検証済みの場合に限定します。既知の正しい応答・再試行目安とprovider一致検査は維持します。
probe取消は本文・内部例外・Dataを除去し、取消型と呼出元トークンを保持します。
直接ProbeAsyncと能力カタログの両方で確認しています。

物理処理の隔離は公開理由コードと別に扱います。未知コードでもWorkMayStillBeRunningを捨てず、再起動後も隔離します。
通常例外・内部取消、OOM、過負荷、先に確定した取消・期限切れ・世代失効も従来の扱いを維持します。
オフラインサンプルの理由だけをreadyへ合わせ、backend=offline-demo/isMock=trueによる区別は維持します。

公開済みv0.1.3の型、Scene v1 JSON Schema、台帳v1形式、ハッシュ計算式は変えません。
パッケージのみ0.1.4-dev8とし、既存5パッケージは0.1.3-dev1を維持します。

## 自動検証

SceneRuntimeErrorTestsを66件追加しています。既存テストの期待値は変更していません。

| 検証 | 件数 |
|---|---:|
| 独自・改変・巨大・nullジョブコードの返却/保存/再起動 | 10 |
| 独自readiness理由を実行可能としない | 10 |
| probe取消例外の機密情報除去 | 2 |
| 旧台帳の独自・null失敗理由の置換 | 2 |
| 既知ジョブ理由、再試行目安、物理処理隔離 | 10 |
| 未知コードの物理処理フラグと再起動後の隔離 | 2 |
| 通常例外・I/O例外・内部取消の隔離と情報除去 | 3 |
| 既知readiness7状態と再試行目安の維持 | 7 |
| 非ready6状態での未知理由を閉じたままにする | 6 |
| 未知readiness状態の除去 | 3 |
| 不整合なready主張の拒否 | 3 |
| null応答・provider不一致・呼出元リンク取消 | 3 |
| 遅い独自エラーで先行終端状態を上書きしない | 3 |
| 旧completed記録の候補失効と観測履歴の保持 | 1 |
| 既知台帳バイト列とhashの維持 | 1 |

初回の修正後試験は289件成功・新規試験9件失敗でした。隔離後にテストがメディアを登録して要求を準備しようとし、
登録自体が既存のbackend_work_unconfirmedで拒否されたためです。新規試験にその拒否確認を追加し、
登録済み要求から別IDを作って解析受付の拒否も確認するよう修正しました。本体の隔離処理と既存試験は変更していません。
最初の失敗ログ・TRXは修正前証跡とは別に保存しています。
Cosmos全298件が成功しました。全体・下流は **783件成功、S0既知失敗2件、新規失敗なし**。全7リポジトリのrestore/buildが成功しました。
証跡はD:\AIKernel\.codex_tmp\cosmos-runtime-errors-20261112と配布ZIPのevidenceへ保存します。

## 接続と制限

独自バックエンドはCOSMOS_SCENE_ERRORS.mdの許可リストへ合わせてください。自由文や接続先、資格情報を理由へ入れないでください。
ready時の独自ラベルはreadyへ変更し、モデル/モックの区別は既存provider情報で行います。
正規化で古い不正理由の応答内容・OutputHashは変わります。正しい記録のhashと計算式は維持します。
認可ホストの失効通知、利用側の最新状態・権限・期限の検証は引き続き必要です。
設定済みprovider identityや観測本文などへ機密情報を置くことを許可する変更ではなく、ホストはSDK例外のスタックトレースを公開しないでください。
実機CosmosのB01〜B06・TheoriaCast共同受入は未実施です。外部映像送信・モデル/GPU/配信操作は行いません。
以前のdev1〜dev7配布物を保持し、dev8を別ZIPにします。push・PR・dev取り込み・公開は行いません。

## 確定した全体検証

| Repository | build | 成功 | 既知失敗 |
|---|---|---:|---:|
| Providers | 成功 | 418 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 42 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 64 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |

下流devのHEADを確認し、同じdevの隔離コピーでbuild/testを再実行しました。下流の作業ブランチ・未コミット変更は変更していません。Doomの既知失敗はHUD定数32/36の不一致による同じ2件です。

7 JSON Schemaと2例に生成差分はありません。nupkg内DLLは全体試験で使ったDLLと一致します。独立サンプルのlocked-mode restore/runが成功し、lockのSHA-512とnupkgが一致しました。Cosmosのみdev8、既存5パッケージはすべて0.1.3-dev1です。

nupkg SHA-256: `37cd5693b8d3a9d3a6f5141a9d8555b9218ff9d9f5fdeaedf1e397b869d68ba8`。
配布ZIP: `D:\AIKernel\artifacts\AIKernel.Cosmos-P0-dev8-20261112.zip`。同梱SHA256SUMS.txtで全ファイルを照合できます。
