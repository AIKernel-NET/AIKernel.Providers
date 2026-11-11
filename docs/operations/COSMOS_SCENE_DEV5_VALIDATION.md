# Cosmos 0.1.4-dev5: 推論完了時の再認可

2026-11-11 / AIKernel.Providers / review/COSMOS-P0。dev4コミット5f7abe8への追補。

## 修正

dev4は推論開始前と完了済み結果の照会時に認可していましたが、推論中に元の受付資格の権限が変わっても、
モデル応答をそのままcompletedとして保存していました。別の有効な資格による照会では、元の資格の失効を検知できません。
新しい試験では、元の資格だけを推論中に失効させ、従来のgrantを維持した別セッションで結果を照会して再現しています。

dev5は、正常なモデル応答の内容検証後、結果確定前に元の受付主体の認可を取り直します。
CallerId、permissionScope、policyRevision、要求中のactor/assetRevision・action・targetを照合します。

* 権限の取消・不一致はrejected。permission_denied、unauthenticated、actor_revision_mismatch、action_not_authorized、target_not_authorizedを区別します。
* 認可処理の例外・内部取消・不正grant・未知の理由コードはfailed / authorization_unavailable。例外本文は返しません。
* 拒否・失敗時はモデルの観測と候補を結果へ保存しません。終端記録は保持し、権限復旧・同一ID再送・再起動でも再実行・復活させません。
* 認可はサービスのロック外で実行します。その間に確定したcanceled/timed_out/supersededを上書きせず、元の期限・単調時計予算を延長しません。
* 正常終了を確認したバックエンドへは取消を送りません。推論が残る間の取消伝播と、確認できない物理処理の隔離は維持します。
* 結果の検証・認可が終わるまで予約枠を保持します。認可情報の取得障害だけで、完了済みのGPU処理を未確認扱いにはしません。

推論開始前はdev3、すでにcompletedとなった結果の失効はdev4の仕様を維持します。
完了時の新しい認可が成功して初めてcompletedを確定し、成功結果のValidationMsにはこの認可時間を含めます。
公開DTO、操作、状態・理由コード、Scene v1 Schema、台帳v1、ハッシュ・IDの計算式は変更しません。
パッケージは0.1.4-dev5、既存Abstractions/Dtos/Enums/Common/Coreは0.1.3-dev1のままです。

## 試験

SceneCompletionAuthorizationTestsを19件追加しました。

| 検証 | 件数 |
|---|---:|
| 推論中の明示拒否/null/未認証/CallerId/scope/policy/actor版・削除/action/target変更、認可例外・内部取消・不正grant・未知理由コード | 14 |
| 元の受付主体で再認可し、有効な候補と期限を維持 | 1 |
| 完了時の認可中に取消・期限切れ・世代失効、予約枠と後続要求の処理 | 3 |
| 既に取り消した推論の遅い応答は再認可・候補確定しない | 1 |

最初の15件は修正前にすべて失敗しました。権限変更14件ではcompletedが返り、正常例では完了時の認可回数が0でした。
修正後は、新しい19件と既存119件を合わせたCosmos全138件が成功しました。
回帰試験では拒否・失敗の永続化、再起動後の再照会・同一ID再送、新しい正常要求の処理、秘密情報非保存も確認します。

既存の `A06_UnchangedOrExpandedGrantDispatchesUsingOriginalPrincipal` は「開始前のみ1回」という回数確認を、
今回の明示要件に合わせて「開始前と完了時の計2回」へ更新しました。候補・期限・推論回数などの期待値は維持し、
回数の厳密な確認を省略・緩和していません。他の既存試験の期待値は変更していません。

最初のProviders全体試験では、既存の完了候補復旧試験のactionケース1件が、再起動後の再送時に失敗しました。
完了表示後の台帳後処理と、試験による直接読取りが競合し得る箇所を確認しました。
この環境の独立プローブでは、開いているファイルの置換は拒否され、読取りを閉じると置換に成功しました。
検証側はActiveInference=0まで待ってから台帳を読み、PhysicalOutstanding=falseも確認するよう同期を追加しました。
元の候補・履歴・期限・冪等性の期待値は維持し、再送失敗時には理由コードも明示するよう検証を強めました。
本体の台帳書込み失敗時の拒否処理は変更せず、意図的な書込み障害の試験も維持しています。
最初の失敗ログ・TRXとプローブ結果は、最終再実行の証跡とは別に保存します。

証跡は `D:\AIKernel\.codex_tmp\cosmos-completion-authorization-20261111` に保存します。
全体・下流も **623件成功、S0既知失敗2件、新規失敗なし**。全7リポジトリのrestore/buildが成功しました。

## 引渡し・制限

引渡し先は `D:\AIKernel\artifacts\cosmos-p0-dev5-20261111`。
以前のdev1〜dev4は保持し、ローカルnupkg、依存フィード、独立サンプルとlock、Schema、文書、証跡を配布します。
導入時はrestore --force-evaluateでCosmosの版・hashだけが変わることを確認し、--locked-modeへ戻してください。

ホストの認可処理は呼出しごとに現在の権限・資格期限・失効を確認する必要があります。
同期の認可処理に強制取消は追加しないため、ホスト側で短く有界にしてください。止まらない認可処理は予約枠を保持します。
これは開始前・完了時の検査です。通知なしに権限が失われて元へ戻る変化の履歴を検知する保証や、推論中の継続監視はありません。
TheoriaCastは引き続き採用直前の状態・権限・期限を検証し、変更時にscene.invalidate/cancelを通知してください。

実機CosmosによるB01〜B06とTheoriaCast共同受入は未実施です。外部映像送信、モデル/GPU操作、配信操作は行いません。
push・PR・devへの取り込み・パッケージ公開も行いません。

## 確定した全体検証

| Repository | build | 成功 | 既知失敗 |
|---|---|---:|---:|
| Providers | 成功 | 258 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 42 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 64 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |

下流のdevが初版の検証時から変わっていないことを確認し、同じdevの隔離コピーでbuild/testを再実行しました。作業中のブランチ・未コミット変更は変更していません。Doomの既知失敗はHUD定数32/36の不一致による同じ2件です。

7 JSON Schemaと2例の生成差分なし。nupkg内DLLは全体試験で使ったDLLと一致します。独立サンプルのlocked-mode restore/runが成功し、lockのSHA-512とnupkgが一致しました。新SDKはdev5、既存Abstractions/Dtos/Enums/Common/Coreはすべて0.1.3-dev1です。

新nupkg SHA-256: `dc1cfdcabef89d1d566cbf8f04f997e1762c53748739cd2015d3cabee90fa469`。
配布ZIP: `D:\AIKernel\artifacts\AIKernel.Cosmos-P0-dev5-20261111.zip`。同梱SHA256SUMS.txtで各ファイルを照合できます。
