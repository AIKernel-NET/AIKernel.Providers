# 0.1.4 の共有契約と互換性

HTTP と MCP のアプリからは `AIKernel.Abstractions.Hosting` のインターフェースを参照します。HTTP のパスとメソッド、MCP のツール名は別々に登録でき、どちらにも同じアプリケーションサービスを渡せます。Kestrel と MCP SDK は `AIKernel.Providers.Distributed` が使用します。起動例、接続ファイル、停止と復帰の確認手順は[サンプル](../../samples/DistributedApplication/README.md)にあります。

## 既存アプリで差し替えられる箇所

| 対象 | 参照する契約 | 互換性 |
| --- | --- | --- |
| 画像・音声・空間認知 | Perception の8インターフェース | 従来のメソッド、名前空間、要求・結果の型を維持 |
| Cosmos | `ISceneService`、`ISceneSession`、認可・推論・配備の3契約 | 従来の `SceneService.OpenSession` も引き続き使用可能 |
| Council | `ICouncilSemanticEvaluationProvider` と評価の要求・結果 | 既存の評価結果と governance の規則を維持 |
| マニフェスト | `IProviderManifestLoader`、`IProviderManifestValidator`、`IConfiguredManifestValidator` | カタログとレジストリは独自実装を受け取れ、既存コンストラクターも利用可能 |
| 資格情報と利用枠 | `ISecureCredentialResolver<TOptions>`、`ITokenReservationBudget` | 既存の資格情報解決器や永続ジャーナルの呼び出しを維持 |

旧アセンブリで公開していた型には型転送を設定しました。既存の名前空間を使うソースは同じ名前を参照できます。`AIKernel.Providers.Perception.IFramePerceptionProvider` は `AIKernel.Contracts` が所有します。カーネルの `AIKernel.Abstractions.Perception.IFramePerceptionProvider` は別の要求型を扱います。接続監視の `ConnectionState` は `AIKernel.Enums` が所有します。配布時には対応する共有パッケージも一緒に更新してください。

## 点検範囲

ライブラリ、ツール、アプリのソースで公開インターフェースとサービスの利用箇所を照合しました。共有契約へ移す基準は、別の実装が同じ入出力でアプリに提供でき、共有パッケージから具象 SDK やランタイムを参照せずに済むことです。

Microsoft AI の応答マッパーは SDK の `ChatResponse` を扱います。Wasm の JS 相互運用と GPU のデバイスブリッジは実行環境の契約です。これらは各アダプターで公開を続けます。Standard の既存スケジューラー・プロファイラーと Control の CTG コーディネーターは `AIKernel.Common.Results.Result<T>` を返すインターフェースを備えています。Monolith の単体配布用インターフェース、Doom・Demo のアプリ専用操作もそれぞれの既存境界を使用します。

## 受入試験

.NET 10 SDK で `dotnet test tests/Hosting/AIKernel.Providers.Distributed.Tests -c Release` を実行します。独立した2台のローカル待受で認証、失効、接続先切り替え、停止・復帰、TLS 終端とパス接頭辞、Range と切断の伝播を確認します。業務アプリのソースや外部モデルは必要ありません。
