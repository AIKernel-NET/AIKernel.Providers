# AIKernel image provider

Local CPU processing for .NET 10 and the AIKernel 0.1.4 package family. Windows x64 includes its native OpenCV runtime. The application uses `AIKernel.Contracts.Images` and configures provider implementations at startup.

```sh
dotnet add package AIKernel.Providers.Image --version 0.3.0
```

```csharp
using AIKernel.Contracts.Images;
using AIKernel.Providers.Image;

using var provider = new ImageEditProvider(backgroundModelPath: "models/birefnet-lite-512.onnx");
IImageEditProvider editor = provider;
var photo = await File.ReadAllBytesAsync("photo.jpg", cancellationToken);
var illustration = await editor.StylizeAsync(photo, "illustration",
    new ImageStylizationOptions(strength: 0.6), cancellationToken);
var cutout = await editor.RemoveBackgroundAsync(photo,
    new ImageMattingOptions(maskRefineRadius: 6, maskErodePx: 1, decontaminateEdge: true),
    cancellationToken);
await File.WriteAllBytesAsync("illustration.png", illustration, cancellationToken);
await File.WriteAllBytesAsync("cutout.png", cutout, cancellationToken);
```

- Styles are `illustration`, `pencil`, and `detail`. Pass no checkpoint when only using the OpenCV styles.
- Strength ranges from 0 to 1 for all three styles. The provider blends the source and the styled result at intermediate values and preserves the source alpha channel. Strength 0 writes the source pixels as PNG. Strength 1, the default used by the original overload, produces the same result as 0.1.4.
- For background removal, download the [pinned BiRefNet_lite FP32 ONNX file](https://huggingface.co/studioludens/birefnet-lite-512/blob/962e67969ec6acec9c19fab76acee4f5e22e990c/onnx/model.onnx). The provider verifies its size and SHA-256 against `ImageEditProvider.BackgroundModelSha256` before loading it. The session is reused between jobs.
- The optional `ImageMattingOptions` control guided refinement (0–32 px), inward edge adjustment (0–2 px), and background-color removal at partially transparent edges. Zero disables the corresponding pixel adjustment. With all options at their defaults, background removal preserves the 0.2.0 output.
- Inputs are PNG (at most 8 bits per sample) and 8-bit JPEG. The defaults are 16 MiB encoded and 4,194,304 pixels decoded. Each image side is at most 8,192 pixels. Outputs preserve pixel dimensions. Background removal multiplies the soft matte by existing transparency.
- Await the operation before reusing the input buffer. Unsupported input, unknown style, and missing checkpoint throw exceptions. Cancellation can terminate ONNX inference. OpenCV completes the current native operation before observing cancellation. An editor runs one operation at a time.

To tag visible content, load an [AIKernel-imported Cosmos 3 Edge reasoner](https://github.com/AIKernel-NET/AIKernel.Core/blob/dev/docs/cosmos3-inference.md) with a trusted SHA-256 for `import-manifest.json`:

```csharp
using var taggingProvider = new CosmosImageTagger(edgeDirectory, manifestSha256);
IImageTaggingProvider tagger = taggingProvider;
var tags = await tagger.TagAsync(photo, cancellationToken);
```

Tagging returns short English tags from the local reasoner. A host with a fixed RAM budget can supply `CosmosMemoryOptions` and a private SSD directory. The supported image operations use CPU execution. Model weights require separate provisioning. The package carries third-party notices for OpenCV, OpenCvSharp4, ONNX Runtime, and BiRefNet_lite.
