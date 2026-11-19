# Cosmos 0.1.4-dev3: 待機ジョブの推論前再認可

2026-11-11 / AIKernel.Providers / review/COSMOS-P0。dev2コミット2345fc4への追補。

## 修正

dev2は受付時のgrantをJobへ保存し、待機後もそのgrantで画像を解決していました。
受付後に認証資格、呼出元、公開範囲、ポリシー、actor版、動作または対象の権限が変わっても、推論を始められました。
追加した13ケースで、修正前に拒否・失敗すべきジョブがcompletedになることを再現しました。

dev3は元の受付主体をメモリ内に保持し、readiness確認後にホストのISceneAuthorizerを呼び直します。
CallerId、permissionScope、policyRevisionの一致と、要求中のactor/assetRevision・action・targetの現在の許可を検証し、
新しいgrantでメディアの所有者とsnapshotを照合します。検証できなければバックエンドへ入力しません。

* 権限の取消・不一致はrejected。permission_denied、unauthenticated、actor_revision_mismatch、action_not_authorized、target_not_authorizedを区別します。
* 認可処理の障害・内部の取消・不正grant・未知の例外コードはfailed / authorization_unavailable。例外本文は返しません。
* 認可処理はサービスのロック外で実行します。その間に取消・期限切れが確定したら元の終端状態を維持し、推論は開始しません。
* 認可処理が戻るまで予約スロットを保持します。バックエンドに推論を渡していない間は取消を送らず、渡す段階では推論登録より取消が先行しないように同期します。
* 拒否・失敗も台帳へ保存し、権限が戻っても同じrequestIdの再送で再推論しません。再起動後も保持期間内は終端を維持します。
* 元の撮影期限と受付時の単調時計予算は延長しません。principal/claimsは台帳・結果・推論入力へ含めず、ジョブ終了時に参照を外します。

公開DTO、操作名、Scene v1 Schema、InputHash/OutputHash/proposalIdの計算は変更していません。
既存のrejected状態を実際の推論前拒否に使用し、authorization_unavailableを理由コードに追加します。
APIのSucceededは照会等の処理成功、statusはジョブの状態、実際の動作実行はTheoriaCast側、という区別は維持します。
新パッケージを0.1.4-dev3とし、既存5パッケージは0.1.3-dev1に固定したままです。

## 試験

SceneDispatchAuthorizationTestsを17件追加。既存試験の期待値は変更していません。

| 検証 | 件数 | 結果 |
|---|---:|---|
| 明示拒否、null grant、未認証、呼出元/scope/policy変更、actor版/action/target変更、通常例外・内部取消・不正grant・未知理由コード | 13 | 修正前13失敗、修正後すべて成功 |
| 現在grantが有効な場合、元の受付主体で再検証し同じ入力を推論へ渡す | 1 | 成功 |
| readiness応答待ちの間に権限が取り消される | 1 | 推論せずrejected |
| 再認可中の取消・期限切れ、後続要求の有界待機、元の期限の維持 | 2 | 推論せず終端、後続の正常要求は処理 |

13ケースでは、失効後の同一ID再送、復旧後の別ID、再起動後の再照会・再送も確認します。
認可障害だけでGPUを残存処理扱いにせず、別の正常な要求は完了します。
秘密のclaimsと例外文字列が返却JSON・台帳へ入らないことも検証します。

Cosmos全91件が成功しました。証跡は `D:\AIKernel\.codex_tmp\cosmos-authorization-20261111` に保存します。
全体・下流も **576件成功、S0既知失敗2件、新規失敗なし**。全7リポジトリのrestore/buildが成功しました。

## 引渡し・制限

引渡し先は `D:\AIKernel\artifacts\cosmos-p0-dev3-20261111`。
ローカルnupkg、依存フィード、独立サンプルと固定lock、契約文書、Schema、試験証跡を含めます。
既存dev1/dev2の引渡しは上書きしません。導入時は新パッケージをrestore --force-evaluateで一度解決し、
既存5パッケージが0.1.3-dev1のままであることとlock差分を確認した後、--locked-modeへ戻します。

ホストのISceneAuthorizerは各呼出しで現在の権限・資格期限・失効を確認する必要があります。
claimsを無条件でgrantへ戻すホスト実装のままでは失効を検知できません。
同期の既存認可契約に強制取消は追加していないため、ホストの認可処理は短く有界にしてください。
止まらない認可処理はその予約スロットを保持します。サービスのロックは保持しません。

これは推論開始時点での再認可です。推論開始後の権限変更を自動監視・取消する機能は追加していません。
dev2で報告した「完了後、新しい観測を送らず照会時だけ権限やassetRevisionが変わる経路」の恒久失効は今回の対象外です。
呼出元は採用直前の現在状態・権限・期限を検証し、変更時にscene.invalidate/cancelを通知します。
GPUによるB01〜B06とTheoriaCast共同受入は未実施です。外部映像送信・モデル起動/停止・GPU配置・配信操作は行いません。
push・PR・パッケージ公開も行いません。

## 確定した全体検証

| Repository | build | 成功 | 既知失敗 |
|---|---|---:|---:|
| Providers | 成功 | 211 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 42 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 64 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |

下流のdevが初版の検証時から変わっていないことを確認し、同じdevの隔離コピーでbuild/testを再実行しました。作業中のブランチ・未コミット変更は変更していません。Doomの既知失敗はHUD定数32/36の不一致による同じ2件です。

7 JSON Schemaと2例の生成差分なし。nupkg内DLLは全体試験で使ったDLLと一致します。独立サンプルのlocked-mode restore/runが成功し、lockのSHA-512とnupkgが一致しました。新SDKはdev3、既存Abstractions/Dtos/Enums/Common/Coreはすべて0.1.3-dev1です。

新nupkg SHA-256: `464a0a58de134acf67b813f1c78d0144d19c254d4076b6084116b57caff61541`。
配布ZIP: `D:\AIKernel\artifacts\AIKernel.Cosmos-P0-dev3-20261111.zip`。同梱SHA256SUMS.txtで各ファイルを照合できます。
