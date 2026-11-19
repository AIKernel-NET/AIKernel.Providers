# Cosmos 0.1.4-dev7: 受付エラーの許可リスト

2026-11-12 / AIKernel.Providers / review/COSMOS-P0。dev6コミット7ede11aへの追補。

## 修正

dev6で報告した、認可ホストの独自SceneException.Codeが受付ErrorCode/ErrorMessageへそのまま返る問題を修正しました。
認可アダプター・セッション開始・メディア登録・取消例外を使った25ケースが修正前にすべて失敗しました。
試験に使う機密文字列は合成したマーカーであり、実際の資格情報・画像・外部推論は使用していません。

SceneIntakeErrorsに認可用6種類と最終受付用40種類の許可リストを集約しました。
大文字小文字も含めた完全一致のみ採用し、prefix・空白・改行・空文字・null・巨大な独自コードを通しません。
未知の認可コード、通常例外、不正grantはauthorization_unavailable、それ以外の未知受付エラーはinvalid_requestです。
ErrorMessageも安全なコードだけとし、失敗応答のMetadataは空、OutputHashはnullを維持します。
例外として返すセッション開始・メディア登録も、新しい例外へ置換して元の本文・内部例外・Data・物理処理フラグを渡しません。
メディアStreamが投げる例外にも同じ境界を適用します。

取消はOperationCanceledException/TaskCanceledExceptionとして維持し、本文だけ固定化します。
呼出元のCancellationTokenは保持し、認可ホスト内部のトークン・Task・例外情報をコピーしません。
新規要求は推論を開始せず、既知の待機中・実行中・完了済み要求には従来どおり失効を保存します。
権限復旧・同一ID再送・再起動でも候補は復活しません。

既存テストの期待値は変更していません。公開済みv0.1.3の型、Scene v1 JSON Schema、台帳形式、ハッシュ・IDの計算式も維持します。
パッケージのみ0.1.4-dev7とし、Abstractions/Dtos/Enums/Common/Coreはすべて0.1.3-dev1のままです。

## 自動検証

SceneIntakeErrorTestsを59件追加しました。

| 検証 | 件数 |
|---|---:|
| 未知・改変・巨大な認可コード、通常例外の置換 | 10 |
| 全5操作の認可受付エラー | 5 |
| セッション開始・メディア受付の例外情報除去 | 4 |
| 認可の取消例外を3受付経路で安全に伝播 | 6 |
| 既知の認可6コードを保ち、例外Dataを除去 | 6 |
| 最終応答境界の未知コード拒否と既知コード維持 | 16 |
| メディアStreamの独自・通常・取消例外と受付枠の解放 | 5 |
| 呼出元のリンク済み取消トークンの維持 | 2 |
| 待機中・実行中・完了済み要求の失効・再起動・再送 | 3 |
| 不正grantを安全な認可エラーへ置換 | 2 |

新規59件と既存173件のCosmos全232件が成功しました。初回に新規試験のトークン指定警告を検出し、
試験の呼出しへTestContext.Current.CancellationTokenを渡すように修正しています。
Cosmos全232件が成功しました。全体・下流は **717件成功、S0既知失敗2件、新規失敗なし**。全7リポジトリのrestore/buildが成功しました。
証跡はD:\AIKernel\.codex_tmp\cosmos-intake-errors-20261112と配布ZIPのevidenceへ保存します。

## 互換性と制限

公開受付コードはCOSMOS_SCENE_ERRORS.mdを参照してください。独自理由コードを使っていた認可ホストは、6種類に対応させてください。
不正grantをinvalid_identifier等で判別していた接続はauthorization_unavailableへ対応してください。
通常の入力エラーと既知の拒否コード、API/ジョブの成功判定、取消例外の扱いは維持しています。
HTTPホストはSDK例外のスタックトレースを公開応答へ転写せず、公開コードと取消状態だけを使用してください。

今回の対象は受付エラーです。カスタムバックエンドの受付後result.ErrorCode/readiness.ReasonCodeは引き続き別契約であり、
任意のバックエンドが返す全コードを正規化する実装は含めません。独自バックエンドにも非機密コードの契約を守らせる必要があります。
実機CosmosのB01〜B06・TheoriaCast共同検証は未実施です。外部映像送信・モデル/GPU/配信操作は行いません。
以前のdev1〜dev6配布物を保持し、dev7を別ZIPで渡します。push・PR・dev取り込み・公開は行いません。

## 確定した全体検証

| Repository | build | 成功 | 既知失敗 |
|---|---|---:|---:|
| Providers | 成功 | 352 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 42 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 64 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |

下流devのHEADを確認し、同じdevの隔離コピーでbuild/testを再実行しました。下流の作業ブランチ・未コミット変更は変更していません。Doomの既知失敗はHUD定数32/36の不一致による同じ2件です。

7 JSON Schemaと2例に生成差分はありません。nupkg内DLLは全体試験で使ったDLLと一致します。独立サンプルのlocked-mode restore/runが成功し、lockのSHA-512とnupkgが一致しました。Cosmosのみdev7、既存5パッケージはすべて0.1.3-dev1です。

nupkg SHA-256: `cba4b645884ee806cc235c5022902e9053b1b8c5c38dfd9f3d9471f8dc221aa3`。
配布ZIP: `D:\AIKernel\artifacts\AIKernel.Cosmos-P0-dev7-20261112.zip`。同梱SHA256SUMS.txtで全ファイルを照合できます。
