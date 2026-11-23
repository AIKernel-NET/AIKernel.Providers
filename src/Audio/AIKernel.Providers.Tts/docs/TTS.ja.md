# AIKernel.Providers.Tts の利用手引き

.NET 10とAIKernel 0.1.4で、利用者が用意した音声合成ランタイムを呼び出します。NuGet名は `AIKernel.Providers.Tts`、ProviderとcapabilityのIDは `tts`、操作は `tts.synthesize` です。[登録からWAVの保存までの実行例](TTS.md#register-and-call)

付属の `providers/tts.settings.example.json` を参考に、ホストの設定でエンジン名、URL、既定の言語と配布音声ID、タイムアウトを指定します。Kokoro-82Mのサンプルは `en-US` と `af_heart`、`af_bella` 等の配布音声に対応します。呼び出しごとに `text`、`language`、`voice`、`speed` を指定できます。言語と声の省略時は設定を使います。音声IDの既定値を設定しない場合は要求に指定してください。速度の既定値は `1.0` です。

成功したら `result.Metadata["audio_base64"]` をBase64から復号して `.wav` として保存できます。`format`、`sample_rate_hz`、`channels`、`sample_frames`、`provider`、`engine`、`voice` で結果を確認し、ファイルのSHA-256を `OutputHash` と比較します。音声は応答が所有します。永続化は呼び出し側で行い、通常のログには音声や入力本文を含めないでください。

未設定、接続不能、未対応の言語・音声、タイムアウトは結果の `ErrorCode` で区別できます。呼び出し側のキャンセルは `OperationCanceledException` です。HTTPの待機をキャンセルした後も、ランタイムの実行状況はランタイム側で管理します。[HTTPプロトコルとエラー一覧](TTS.md#runtime-protocol)

T1000に固定するには、導入済みランタイムの起動前に `CUDA_VISIBLE_DEVICES` へT1000のUUIDを設定します。ランタイム側のデバイス0とONNX RuntimeのCUDA実行ノードを確認してください。モデル、配布音声、CUDAとcuDNNの導入およびライセンス確認は利用者が行います。[Windowsでの接続例](TTS.md#connect-a-t1000-runtime-on-windows)
