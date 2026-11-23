using AIKernel.Contracts.Images;
using OpenCvSharp;

namespace AIKernel.Providers.Image;

/// <summary>Local CPU image styling and foreground matting with bounded inputs and one active operation per instance.</summary>
public sealed class ImageEditProvider : IImageEditProvider, IDisposable
{
    /// <summary>SHA-256 of the supported Studio Ludens BiRefNet_lite 512 FP32 checkpoint.</summary>
    public const string BackgroundModelSha256 = BiRefNetMatting.CheckpointSha256;

    private readonly string? backgroundModelPath;
    private readonly int maximumEncodedBytes;
    private readonly int maximumPixels;
    private readonly int inferenceThreads;
    private readonly SemaphoreSlim execution = new(1);
    private BiRefNetMatting? matting;
    private bool disposed;

    /// <summary>Creates an editor and optionally registers a local background-removal checkpoint.</summary>
    /// <param name="backgroundModelPath">Absolute or relative path to the verified FP32 ONNX checkpoint.</param>
    /// <param name="maximumEncodedBytes">Maximum encoded bytes per request, at most 32 MiB.</param>
    /// <param name="maximumPixels">Maximum decoded pixels per request, at most 16 Mi pixels.</param>
    /// <param name="inferenceThreads">Number of ONNX Runtime CPU threads, between 1 and 16.</param>
    public ImageEditProvider(string? backgroundModelPath = null, int maximumEncodedBytes = ImageInput.DefaultMaximumBytes,
        int maximumPixels = ImageInput.DefaultMaximumPixels, int inferenceThreads = 2)
    {
        ImageInput.ValidateLimits(maximumEncodedBytes, maximumPixels);
        ArgumentOutOfRangeException.ThrowIfLessThan(inferenceThreads, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(inferenceThreads, 16);
        this.maximumEncodedBytes = maximumEncodedBytes;
        this.maximumPixels = maximumPixels;
        this.inferenceThreads = inferenceThreads;
        this.backgroundModelPath = backgroundModelPath is null ? null : Path.GetFullPath(backgroundModelPath);
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> StylizeAsync(ReadOnlyMemory<byte> image, string style,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (style is not ("illustration" or "pencil" or "detail"))
            throw new NotSupportedException("Use illustration, pencil or detail for CPU stylization.");

        return EditAsync(image, style, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> RemoveBackgroundAsync(ReadOnlyMemory<byte> image,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (backgroundModelPath is null)
            throw new InvalidOperationException("No BiRefNet_lite checkpoint is registered.");

        return EditAsync(image, style: null, cancellationToken);
    }

    private async ValueTask<ReadOnlyMemory<byte>> EditAsync(ReadOnlyMemory<byte> bytes, string? style, CancellationToken token)
    {
        ImageInput.ValidateSize(bytes, maximumEncodedBytes);
        await execution.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return await Task.Run(() => Edit(bytes, style, token), token).ConfigureAwait(false);
        }
        finally
        {
            execution.Release();
        }
    }

    private byte[] Edit(ReadOnlyMemory<byte> bytes, string? style, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var source = ImageInput.Decode(bytes, maximumPixels);
        using var bgr = ImageInput.ToBgr(source);
        using var sourceAlpha = new Mat();
        if (source.Channels() == 4)
            Cv2.ExtractChannel(source, sourceAlpha, 3);

        using var output = new Mat();
        token.ThrowIfCancellationRequested();
        if (style is null)
        {
            matting ??= new BiRefNetMatting(backgroundModelPath!, inferenceThreads, token);
            using var matte = matting.Predict(bgr, token);
            if (!sourceAlpha.Empty())
                Cv2.Multiply(matte, sourceAlpha, matte, scale: 1d / 255d);

            Cv2.CvtColor(bgr, output, ColorConversionCodes.BGR2BGRA);
            Cv2.InsertChannel(matte, output, 3);
        }
        else
        {
            using var edited = new Mat();
            switch (style)
            {
                case "pencil":
                    using (var grayscale = new Mat())
                        Cv2.PencilSketch(bgr, grayscale, edited);
                    break;
                case "detail":
                    Cv2.DetailEnhance(bgr, edited);
                    break;
                default:
                    Cv2.Stylization(bgr, edited);
                    break;
            }

            if (sourceAlpha.Empty())
                edited.CopyTo(output);
            else
            {
                Cv2.CvtColor(edited, output, ColorConversionCodes.BGR2BGRA);
                Cv2.InsertChannel(sourceAlpha, output, 3);
            }
        }

        token.ThrowIfCancellationRequested();
        if (!Cv2.ImEncode(".png", output, out byte[] png))
            throw new InvalidOperationException("OpenCV could not encode the edited image.");

        token.ThrowIfCancellationRequested();
        return png;
    }

    /// <summary>Releases the model after the current native operation has settled.</summary>
    public void Dispose()
    {
        execution.Wait();
        try
        {
            disposed = true;
            matting?.Dispose();
            matting = null;
        }
        finally
        {
            execution.Release();
        }
    }
}
