using System.Security.Cryptography;
using AIKernel.Contracts.Images;
using AIKernel.Providers.Image;
using OpenCvSharp;

if (args.Length is not (3 or 5))
{
    Console.Error.WriteLine("Usage: Image.LocalClient <photo> <birefnet.onnx> <output-directory> [<cosmos-edge-directory> <manifest-sha256>]");
    return 2;
}

byte[] photo = await File.ReadAllBytesAsync(args[0]);
Directory.CreateDirectory(args[2]);
using var provider = new ImageEditProvider(args[1]);
IImageEditProvider editor = provider;
using var original = Cv2.ImDecode(photo, ImreadModes.Unchanged);
if (original.Empty())
    throw new InvalidDataException("The example needs one decodable PNG or JPEG photo.");

Console.WriteLine($"OpenCV {Cv2.GetVersionString()}, input {original.Width}x{original.Height}");
foreach (string style in new[] { "illustration", "pencil", "detail" })
    await SaveAndCheckAsync(style, await editor.StylizeAsync(photo, style), matte: false);

Cv2.ImEncode(".jpg", original, out byte[] jpeg);
await SaveAndCheckAsync("illustration-jpeg", await editor.StylizeAsync(jpeg, "illustration"), matte: false);
await SaveAndCheckAsync("cutout", await editor.RemoveBackgroundAsync(photo), matte: true);

using var canceled = new CancellationTokenSource();
canceled.Cancel();
await ExpectFailureAsync<OperationCanceledException>(() => editor.StylizeAsync(photo, "illustration", canceled.Token));
using var cancelInference = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
await ExpectFailureAsync<OperationCanceledException>(() => editor.RemoveBackgroundAsync(photo, cancelInference.Token));
await SaveAndCheckAsync("cutout-after-cancel", await editor.RemoveBackgroundAsync(photo), matte: true);

await ExpectFailureAsync<InvalidDataException>(() => editor.StylizeAsync(new byte[] { 1, 2, 3 }, "illustration"));
await ExpectFailureAsync<NotSupportedException>(() => editor.StylizeAsync(photo, "unknown"));
using var unregistered = new ImageEditProvider(maximumEncodedBytes: 8, maximumPixels: 1);
await ExpectFailureAsync<InvalidOperationException>(() => unregistered.RemoveBackgroundAsync(photo));
await ExpectFailureAsync<InvalidDataException>(() => unregistered.StylizeAsync(photo, "illustration"));
using var boundedPixels = new ImageEditProvider(maximumPixels: 1);
await ExpectFailureAsync<InvalidDataException>(() => boundedPixels.StylizeAsync(photo, "illustration"));
using var absentCheckpoint = new ImageEditProvider(Path.Combine(args[2], "unregistered-model.onnx"));
await ExpectFailureAsync<InvalidOperationException>(() => absentCheckpoint.RemoveBackgroundAsync(photo));

if (args.Length == 5)
{
    using var tagger = new CosmosImageTagger(args[3], args[4]);
    IImageTaggingProvider tags = tagger;
    var result = await tags.TagAsync(photo);
    if (result.Count is < 1 or > 5)
        throw new InvalidDataException("Tag count is outside the supported range.");
    Console.WriteLine($"Cosmos 3 Edge tags: {string.Join(", ", result)}");
}

return 0;

async Task SaveAndCheckAsync(string name, ReadOnlyMemory<byte> png, bool matte)
{
    using var result = Cv2.ImDecode(png.Span, ImreadModes.Unchanged);
    if (result.Size() != original.Size() || (matte && result.Channels() != 4))
        throw new InvalidDataException("The edited image dimensions or channels are unexpected.");

    if (matte)
    {
        using var alpha = new Mat();
        Cv2.ExtractChannel(result, alpha, 3);
        Cv2.MinMaxLoc(alpha, out double minimum, out double maximum);
        if (minimum >= 32 || maximum <= 224)
            throw new InvalidDataException("The test photo should yield transparent and opaque regions.");
    }

    string path = Path.Combine(args[2], name + ".png");
    await File.WriteAllBytesAsync(path, png);
    Console.WriteLine($"{name}: {png.Length} bytes, SHA-256 {Convert.ToHexStringLower(SHA256.HashData(png.Span))}");
}

static async Task ExpectFailureAsync<TException>(Func<ValueTask<ReadOnlyMemory<byte>>> run) where TException : Exception
{
    try
    {
        await run();
    }
    catch (TException)
    {
        Console.WriteLine($"Rejected as expected: {typeof(TException).Name}");
        return;
    }
    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}
