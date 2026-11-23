using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

namespace AIKernel.Providers.Image;

internal sealed class BiRefNetMatting : IDisposable
{
    internal const string CheckpointSha256 = "1cb0fb360dadd15af77c639085d77a9df67db0c64315560c3de005f676345ac2";
    private const int Side = 512;
    private const int Plane = Side * Side;
    private const int CheckpointBytes = 191_877_254;
    private readonly InferenceSession session;
    private readonly string outputName;

    internal BiRefNetMatting(string checkpoint, int cpuThreads, CancellationToken cancellationToken)
    {
        if (!File.Exists(checkpoint))
            throw new InvalidOperationException("The registered BiRefNet_lite checkpoint is missing.");

        // Load the same bytes that were verified. The temporary copy is released after ORT has loaded the graph.
        using FileStream model = File.OpenRead(checkpoint);
        if (model.Length != CheckpointBytes)
            throw new InvalidDataException("BiRefNet_lite checkpoint size does not match the supported release.");

        var data = new byte[CheckpointBytes];
        model.ReadExactly(data);
        cancellationToken.ThrowIfCancellationRequested();
        if (!SHA256.HashData(data).AsSpan().SequenceEqual(Convert.FromHexString(CheckpointSha256)))
            throw new InvalidDataException("BiRefNet_lite checkpoint SHA-256 does not match the supported release.");

        cancellationToken.ThrowIfCancellationRequested();
        using var options = new SessionOptions { IntraOpNumThreads = cpuThreads, InterOpNumThreads = 1 };
        session = new InferenceSession(data, options);
        try
        {
            if (session.OutputMetadata.Count != 1 || !session.InputMetadata.TryGetValue("input_image", out var input) ||
                input.ElementType != typeof(float) || !input.Dimensions.SequenceEqual([1, 3, Side, Side]))
                throw new InvalidDataException("BiRefNet_lite model metadata does not match the supported graph.");

            // The pinned ONNX export uses a different output name than the model card's JS wrapper.
            outputName = session.OutputNames.Single();
            var output = session.OutputMetadata[outputName];
            if (output.ElementType != typeof(float) || !output.Dimensions.SequenceEqual([1, 1, Side, Side]))
                throw new InvalidDataException("BiRefNet_lite model metadata does not describe a single alpha matte.");
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    internal Mat Predict(Mat bgr, CancellationToken cancellationToken)
    {
        using var resized = new Mat();
        Cv2.Resize(bgr, resized, new Size(Side, Side), interpolation: InterpolationFlags.Linear);
        var pixels = new byte[Plane * 3];
        Marshal.Copy(resized.Data, pixels, 0, pixels.Length);
        var tensor = new float[Plane * 3];
        for (int i = 0; i < Plane; i++)
        {
            tensor[i] = (pixels[i * 3 + 2] / 255f - 0.485f) / 0.229f;
            tensor[Plane + i] = (pixels[i * 3 + 1] / 255f - 0.456f) / 0.224f;
            tensor[2 * Plane + i] = (pixels[i * 3] / 255f - 0.406f) / 0.225f;
        }

        using var input = OrtValue.CreateTensorValueFromMemory(tensor, [1, 3, Side, Side]);
        using var options = new RunOptions();
        var inputs = new Dictionary<string, OrtValue>(StringComparer.Ordinal) { ["input_image"] = input };
        using var registration = cancellationToken.Register(static state => ((RunOptions)state!).Terminate = true, options);
        cancellationToken.ThrowIfCancellationRequested();
        var alpha = new float[Plane];
        try
        {
            using var outputs = session.Run(options, inputs, [outputName]);
            var logits = outputs[0].GetTensorDataAsSpan<float>();
            if (logits.Length != Plane)
                throw new InvalidDataException("BiRefNet_lite returned an unexpected mask size.");

            for (int i = 0; i < logits.Length; i++)
                alpha[i] = 1f / (1f + MathF.Exp(-logits[i]));
        }
        catch (OnnxRuntimeException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var mask = Mat.FromPixelData(Side, Side, MatType.CV_32FC1, alpha);
        using var upscaled = new Mat();
        Cv2.Resize(mask, upscaled, bgr.Size(), interpolation: InterpolationFlags.Linear);
        var result = new Mat();
        try
        {
            upscaled.ConvertTo(result, MatType.CV_8UC1, 255);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    public void Dispose() => session.Dispose();
}
