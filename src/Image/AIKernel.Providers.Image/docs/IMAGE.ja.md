# AIKernel 画像Provider

.NET 10とAIKernel 0.1.4向けのローカル画像処理です。Windows x64用のネイティブOpenCVは依存パッケージから復元されます。アプリからは `AIKernel.Contracts.Images` のインターフェースを使い、実行先はホストで構成します。

```sh
dotnet add package AIKernel.Providers.Image --version 0.2.0
```

```csharp
using AIKernel.Contracts.Images;
using AIKernel.Providers.Image;

using var provider = new ImageEditProvider(backgroundModelPath: "models/birefnet-lite-512.onnx");
IImageEditProvider editor = provider;
var photo = await File.ReadAllBytesAsync("photo.jpg", cancellationToken);
var illustration = await editor.StylizeAsync(photo, "illustration",
    new ImageStylizationOptions(strength: 0.6), cancellationToken);
var cutout = await editor.RemoveBackgroundAsync(photo, cancellationToken);
await File.WriteAllBytesAsync("illustration.png", illustration, cancellationToken);
await File.WriteAllBytesAsync("cutout.png", cutout, cancellationToken);
```

- イラスト化のスタイルは `illustration`、`pencil`、`detail` です。イラスト化だけの場合は重みの設定を省略できます。
- 3スタイルとも強さを0から1で指定できます。中間値では元画像と加工結果を合成し、元の透過度を保ちます。0では元の画素をPNGで出力します。強さを省略すると1として処理し、0.1.4と同じ結果になります。
- 背景除去では[リビジョン固定のBiRefNet_lite FP32 ONNX](https://huggingface.co/studioludens/birefnet-lite-512/blob/962e67969ec6acec9c19fab76acee4f5e22e990c/onnx/model.onnx)を登録します。バイト数とSHA-256を照合した後、ONNXセッションを作成し、後の処理で再利用します。
- 入力は8ビット以下のPNGと8ビットのJPEGです。既定の上限は16 MiBと4,194,304画素で、各辺は8,192画素までです。出力PNGは元の画素寸法を保ちます。背景除去では推定したマスクに元画像の透過度を掛け合わせます。
- 呼び出しが完了するまで入力バッファを変更しないでください。未対応画像、未対応スタイル、未登録モデルは例外になります。ONNX推論はキャンセルできます。OpenCVで実行中のネイティブ処理は、その処理が終わった時点でキャンセルを確認します。編集Providerの同時実行数はインスタンスあたり1です。

[AIKernelの取り込み手順](https://github.com/AIKernel-NET/AIKernel.Core/blob/dev/docs/cosmos3-inference.md)で準備したCosmos 3 Edge Reasonerと、信頼済みの `import-manifest.json` のSHA-256を指定すると、画像から英語タグを取得できます。

```csharp
using var taggingProvider = new CosmosImageTagger(edgeDirectory, manifestSha256);
IImageTaggingProvider tagger = taggingProvider;
var tags = await tagger.TagAsync(photo, cancellationToken);
```

RAMに上限を設けるホストでは、`CosmosMemoryOptions` で予算と専用SSDディレクトリを設定できます。初期版の処理はCPUで実行します。モデル重みは別途準備してください。パッケージにOpenCV、OpenCvSharp4、ONNX Runtime、BiRefNet_liteの許諾文を同梱しています。
