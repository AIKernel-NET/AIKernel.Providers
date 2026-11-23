using System.Runtime.InteropServices;
using AIKernel.Contracts.Images;
using AIKernel.Kernel.Inference;
using OpenCvSharp;

namespace AIKernel.Providers.Image;

/// <summary>Tags images with the local Cosmos 3 Edge CPU reasoner.</summary>
public sealed class CosmosImageTagger : IImageTaggingProvider, IDisposable
{
    private const int MaximumSide = 256;
    private readonly int maximumEncodedBytes;
    private readonly int maximumPixels;
    private readonly Cosmos3Model model;

    /// <summary>Loads a registered Cosmos 3 Edge reasoner using its expected import manifest hash.</summary>
    /// <param name="modelDirectory">Directory prepared by the AIKernel Cosmos importer.</param>
    /// <param name="expectedManifestSha256">Trusted SHA-256 of the import manifest.</param>
    /// <param name="memory">Optional CPU RAM budget and private SSD backing directory.</param>
    /// <param name="maximumEncodedBytes">Maximum encoded bytes per request.</param>
    /// <param name="maximumPixels">Maximum decoded pixels per request.</param>
    /// <param name="cpuThreads">Maximum simultaneous model workers.</param>
    public CosmosImageTagger(string modelDirectory, string expectedManifestSha256, CosmosMemoryOptions? memory = null,
        int maximumEncodedBytes = ImageInput.DefaultMaximumBytes, int maximumPixels = ImageInput.DefaultMaximumPixels,
        int cpuThreads = 4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedManifestSha256);
        ImageInput.ValidateLimits(maximumEncodedBytes, maximumPixels);
        if (!Cosmos3Model.IsCheckpointAvailable(modelDirectory, Cosmos3Variant.Edge, expectedManifestSha256))
            throw new InvalidOperationException("A verified Cosmos 3 Edge reasoner is required for image tagging.");

        this.maximumEncodedBytes = maximumEncodedBytes;
        this.maximumPixels = maximumPixels;
        model = new Cosmos3Model(modelDirectory, Cosmos3Variant.Edge, new CosmosExecutionOptions
        {
            CpuThreads = cpuThreads,
            ContextTokens = 512,
            MaximumVisionPatches = MaximumSide * MaximumSide / (16 * 16),
            EnableThinking = false,
            ExpectedImportManifestSha256 = expectedManifestSha256,
            Memory = memory
        });
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> TagAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ImageInput.ValidateSize(image, maximumEncodedBytes);
        var (rgb, size) = await Task.Run(() => ResizeToRgb(image, cancellationToken), cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await model.GenerateAsync("You tag the objects and colors that are clearly visible in a photo. Leave out anything uncertain.",
                [new CosmosContent.Image(new CosmosRgbFrame(size.Width, size.Height, rgb)),
                 new CosmosContent.Text("Return up to five short English tags, separated by commas. Do not infer a location or add related objects.")],
                maxNewTokens: 48, cancellationToken: cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            string[] tags = result.Text.Split([',', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(static tag => tag.Trim(' ', '-', '*', '.'))
                .Where(static tag => tag.Length is > 0 and <= 96)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray();
            if (tags.Length == 0)
                throw new InvalidDataException("Cosmos 3 Edge returned no usable image tags.");

            return tags;
        }
        finally
        {
            Array.Clear(rgb);
        }
    }

    private (byte[] Pixels, Size Size) ResizeToRgb(ReadOnlyMemory<byte> image, CancellationToken cancellationToken)
    {
        using var decoded = ImageInput.Decode(image, maximumPixels);
        double scale = (double)MaximumSide / Math.Max(decoded.Width, decoded.Height);
        var size = new Size(Aligned(decoded.Width, scale), Aligned(decoded.Height, scale));
        using var thumbnail = new Mat();
        Cv2.Resize(decoded, thumbnail, size, interpolation: InterpolationFlags.Area);
        cancellationToken.ThrowIfCancellationRequested();

        int channels = thumbnail.Channels();
        var source = new byte[size.Width * size.Height * channels];
        var rgb = new byte[size.Width * size.Height * 3];
        Marshal.Copy(thumbnail.Data, source, 0, source.Length);
        for (int i = 0, offset = 0; i < source.Length; i += channels, offset += 3)
        {
            byte alpha = channels == 4 ? source[i + 3] : (byte)255;
            rgb[offset] = BlendOnWhite(source[i + (channels == 1 ? 0 : 2)], alpha);
            rgb[offset + 1] = BlendOnWhite(source[i + (channels == 1 ? 0 : 1)], alpha);
            rgb[offset + 2] = BlendOnWhite(source[i], alpha);
        }
        Array.Clear(source);
        return (rgb, size);
    }

    private static int Aligned(int dimension, double scale) => Math.Clamp((int)Math.Round(dimension * scale / 32) * 32, 32, MaximumSide);

    private static byte BlendOnWhite(byte color, byte alpha) => (byte)((color * alpha + 255 * (255 - alpha) + 127) / 255);

    /// <summary>Waits for active generation and releases model resources.</summary>
    public void Dispose() => model.Dispose();
}
