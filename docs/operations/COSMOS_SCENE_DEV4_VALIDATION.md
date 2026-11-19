# Cosmos 0.1.4-dev4: 完了済み候補の権限失効を永続化

2026-11-11 / AIKernel.Providers / review/COSMOS-P0。dev3コミット977f4ccへの追補。

## 修正

dev3は権限変更時に完了済み候補を照会応答から隠すだけで、台帳の候補を保持していました。
撮影期限内に権限を戻すと、古い候補を再びfreshnessVerified=trueで取得できました。
scope/policyの変更、認可失敗、同一入力の再送では応答拒否が先に発生するため、失効自体が記録されませんでした。

dev4はscene.status、完了済みジョブへのscene.cancel、同一入力のscene.analyze再送で、候補の現在の権限を照合します。
actor/assetRevision、action、target、scope、policyの不一致、または現在の認可情報取得失敗を検知したら、
そのジョブの候補一式を消去し、freshnessVerified=false / stale_contextを台帳へ保存します。
status=completed、observations、provider、timings、撮影期限、冪等性キー・保持期限は維持します。
権限復旧、別セッション、同一ID再送、古いgrantを取得済みの同時照会、再起動でも候補を復活させません。
新しい観測と新しいrequestIdは通常どおり評価できます。候補に無関係な権限変更では候補を失効させません。

セッションの呼出元はOpenSessionで認証・認可したCallerIdに固定します。
現在grantが別CallerIdを返した場合はpermission_deniedとし、元の呼出元の該当候補を失効させます。
別利用者の同名requestIdへ切り替えて結果を読んだり、失効させたりしません。
呼出元の正当な切替には、新しい認証主体で新セッションを開く必要があります。
不正な能力名・引数・異なるInputHashの再送では失効を更新しません。

台帳更新と結果投影は同じロックで行い、失効保存に失敗した照会はjournal_unavailableになります。
失効した候補をメモリから返すこともなく、新規推論を停止します。
台帳v1は既存フィールドだけを更新し、形式を変えません。新しい公開DTO・操作・理由コード・Schemaは追加しません。
InputHash、OutputHash、proposalIdの計算式は維持します。失効後は応答内容が変わるのでOutputHashも変わります。
新パッケージだけ0.1.4-dev4へ上げ、既存5パッケージは0.1.3-dev1に固定します。

## 回帰試験

SceneCompletedAuthorizationTestsを28件追加。既存試験の期待値は変更していません。

| 検証 | 件数 |
|---|---:|
| actor版/削除、action、target、scope、policy、明示拒否、null、未認証、CallerId変更、認可例外/内部取消/不正grant後の復旧 | 13 |
| 同一入力再送時の権限変更と復旧 | 6 |
| 別利用者に同じrequestIdがある場合の主体分離 | 1 |
| 無関係な権限変更で結果JSON・OutputHash・候補IDを維持 | 1 |
| 不正能力・余分な引数・異なる入力による失効操作の防止 | 3 |
| 古いgrantを取得中の照会との競合 | 1 |
| 複数actorのうち一候補が無権限になった場合 | 1 |
| 完了済みジョブの取消照会での失効 | 1 |
| 台帳書込み不能時の候補遮断・新規推論停止 | 1 |

最初の26件で修正前22件失敗・4件成功を確認しました。修正後は追加2件を含むCosmos全119件が成功しています。
復旧前後だけでなく、台帳内の候補消去、観測履歴・期限・推論回数の維持、再起動後も失効が残ることを検証します。
台帳書込み障害はテスト所有のjobs.jsonパスに空ディレクトリを置き、Windows/Unixで置換失敗を再現します。
証跡は `D:\AIKernel\.codex_tmp\cosmos-completed-authorization-20261111` に保存します。
全体・下流も **604件成功、S0既知失敗2件、新規失敗なし**。全7リポジトリのrestore/buildが成功しました。

## 引渡しと残る制限

新しい引渡し先は `D:\AIKernel\artifacts\cosmos-p0-dev4-20261111`。
dev1〜dev3は保持し、ローカルnupkg、依存フィード、独立サンプルとlock、Schema、契約文書、検証証跡を配布します。
導入時はrestore --force-evaluateでCosmosの版・hashだけが変わることを確認し、--locked-modeに戻してください。

ホストの認可処理は毎回、現在の権限・資格期限・失効を確認する必要があります。
照会や通知がなかった期間の権限変化を推測することはできません。今回の変更は完了済み候補の照会時の処理です。
推論中の権限変更を自動監視する機能、取得済み候補を呼出元から遠隔消去する機能はありません。
TheoriaCastは採用直前にも現在状態・権限・期限を確認し、変更をscene.invalidate/cancelで通知します。
台帳が書けなかったときは永続化成功を保証しません。再起動後も過去の撮影鮮度を再保証しないため、古い候補は返しません。

GPUによるB01〜B06、TheoriaCastとの共同受入は未実施です。外部映像送信、モデル/GPU操作、配信操作は行いません。
push・PR・devへの取り込み・パッケージ公開も行いません。

## 確定した全体検証

| Repository | build | 成功 | 既知失敗 |
|---|---|---:|---:|
| Providers | 成功 | 239 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 42 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 64 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |

下流のdevが初版の検証時から変わっていないことを確認し、同じdevの隔離コピーでbuild/testを再実行しました。作業中のブランチ・未コミット変更は変更していません。Doomの既知失敗はHUD定数32/36の不一致による同じ2件です。

7 JSON Schemaと2例の生成差分なし。nupkg内DLLは全体試験で使ったDLLと一致します。独立サンプルのlocked-mode restore/runが成功し、lockのSHA-512とnupkgが一致しました。新SDKはdev4、既存Abstractions/Dtos/Enums/Common/Coreはすべて0.1.3-dev1です。

新nupkg SHA-256: `7b59cf0cd21504623b6471227d36084e297448e6af934c7355479805a720c6b6`。
配布ZIP: `D:\AIKernel\artifacts\AIKernel.Cosmos-P0-dev4-20261111.zip`。同梱SHA256SUMS.txtで各ファイルを照合できます。
