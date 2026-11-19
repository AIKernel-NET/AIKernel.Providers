# Cosmos 0.1.4-dev2: 候補失効の維持

2026-11-11 / AIKernel.Providers / review/COSMOS-P0。初版コミット10546e2への追補。

## 修正

dev1では、設定内容のhashをそのまま現在世代としていました。カメラ、モデルrevision、actorのassetRevisionが
A→B→Aと変わると、最初のAで完了した候補が再びfreshnessVerified=trueとなり、同じrequestIdの再照会・再送で取得できました。
撮影期限内に状態が往復する場合の不具合です。

dev2では、新しい観測の受付時に、設定内容のfingerprintと、その設定が継続した期間を識別する内部の世代を分離して扱います。
状態が変わるたびに新しい世代を割り当てるため、元の設定へ戻しても過去の候補は復活しません。
過去ジョブの解析完了と撮影期限は保持し、候補は空、freshnessVerified=false、ErrorCode=stale_contextになります。
再送で新しい推論は起動しません。別の呼出元の世代には影響しません。

公開DTO、操作名、Scene v1 Schema、InputHash/OutputHash/proposalIdの計算は変更していません。
内部世代はそれらのhash入力に含めません。同じ状態が継続する場合の結果JSONと候補IDが不変であることも検証します。
配布済みpreviewの取り違えを防ぐため、新パッケージを **0.1.4-dev2** とし、既存5パッケージは0.1.3-dev1のままです。

## 試験

SceneContextLifetimeTestsを5件追加しました。既存試験の期待値は変更していません。

* カメラ A→B→A: 修正前に失敗。
* モデルrevision A→B→A: 修正前に失敗。
* assetRevision A→B→A: 修正前に失敗。
* 同じ状態が継続するときの結果JSON・hash・候補ID維持: 修正前から成功。
* 別の認証主体の候補に影響しないこと: 修正前から成功。

修正後、Cosmos全74件が成功しました。再照会・冪等再送でも失効を維持し、新しい現在世代の候補は返せます。
再現前後のTRXと全体・下流の検証ログは `D:\AIKernel\.codex_tmp\cosmos-context-20261111` に記録します。
全体・下流も **559件成功、S0既知失敗2件、新規失敗なし**。すべてのrestore/buildが成功しました。

## 引渡し・互換性

新しい引渡し先は `D:\AIKernel\artifacts\cosmos-p0-dev2-20261111` です。
初版のZIP/フィードは上書きしません。ローカルnupkg、依存フィード、独立サンプル、固定lock、契約文書、Schema、証跡を渡します。
サンプルは0.1.4-dev2へ更新し、TheoriaCastのAbstractions/Dtos/Enums/Common/Coreの5パッケージを0.1.3-dev1に固定します。
新しい版のrestoreを1度行いlock差分を確認した後、--locked-modeで導入してください。

GPU推論やTheoriaCast/Unity/OBSの共同試験B01〜B06は未実施です。PNGと時刻付きPNG列、物理取消確認非対応などの制限は初版と同じです。
外部への映像送信、モデル起動・停止、GPU配置、配信操作、公開、push・PRは行いません。

## 次の調査候補

今回の対象外として、待機ジョブが実推論を開始する直前に現在の認可を再取得していない箇所を確認しました。
現在は受付時のgrantを保持してメディア解決に使います。待機中の権限取消との競合と、
新しい観測を送らず認可やassetRevisionが照会時だけ変わる経路は、次の改修で再現確認が必要です。
この追補では新しい問題を併せて修正せず、候補の世代管理に変更を限定しました。

## 確定した全体検証

| Repository | build | 成功 | 既知失敗 |
|---|---|---:|---:|
| Providers | 成功 | 194 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 42 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 64 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |

下流のdevが初版の検証時から変わっていないことを確認し、同じdevの隔離コピーでbuild/testを再実行しました。人間の作業ブランチ・未コミット変更は変更していません。Doomの既知失敗はHUD定数32/36の不一致による同じ2件です。

7 JSON Schemaと2例の生成差分なし。nupkg内DLLと試験したDLLが一致し、独立サンプルのlocked-mode restore/runも成功しました。新SDKはdev2、既存Abstractions/Dtos/Enums/Common/Coreはすべて0.1.3-dev1です。

新nupkg SHA-256: `5d5b073773c4d12e577877c9a9bd87e7dd22fa4a6133ab50ffd51526aef13e19`。
配布ZIP: `D:\AIKernel\artifacts\AIKernel.Cosmos-P0-dev2-20261111.zip`。ファイル一覧とhashは同梱SHA256SUMS.txtを参照してください。
