namespace AIKernel.Providers.Perception;

using System.Globalization;

/// <summary>
/// [EN] Provides provider-neutral perception filters and quantizers for Aisthesis to Phantasia processing.
/// [JA] Aisthesis から Phantasia への処理に使う provider-neutral な perception filter / quantizer を提供します。
/// </summary>
public interface IPerceptionAlgorithmKernel
{
    /// <summary>
    /// [EN] Quantizes RGB pixels into a semantic palette.
    /// [JA] RGB pixel を semantic palette に量子化します。
    /// </summary>
    /// <param name="request">[EN] Quantization request. [JA] 量子化 request です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視するトークンです。</param>
    /// <returns>[EN] Quantization result. [JA] 量子化 result を返します。</returns>
    ValueTask<SemanticPaletteQuantizationResult> QuantizePaletteAsync(
        SemanticPaletteQuantizationRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// [EN] Calculates temporal differences between two scalar frame maps.
    /// [JA] 2 つの scalar frame map の時間差分を計算します。
    /// </summary>
    /// <param name="request">[EN] Temporal difference request. [JA] 時間差分 request です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視するトークンです。</param>
    /// <returns>[EN] Temporal difference result. [JA] 時間差分 result を返します。</returns>
    ValueTask<TemporalDifferenceResult> CalculateTemporalDifferenceAsync(
        TemporalDifferenceRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// [EN] Calculates Laplacian edge strength over a scalar frame map.
    /// [JA] scalar frame map に対する Laplacian edge strength を計算します。
    /// </summary>
    /// <param name="request">[EN] Edge detection request. [JA] edge detection request です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視するトークンです。</param>
    /// <returns>[EN] Edge detection result. [JA] edge detection result を返します。</returns>
    ValueTask<LaplacianEdgeResult> DetectEdgesAsync(
        LaplacianEdgeRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// [EN] Quantizes stereo energy balance and delay into a direction bin.
    /// [JA] stereo energy balance と delay を direction bin に量子化します。
    /// </summary>
    /// <param name="request">[EN] Binaural quantization request. [JA] binaural quantization request です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視するトークンです。</param>
    /// <returns>[EN] Binaural direction result. [JA] binaural direction result を返します。</returns>
    ValueTask<BinauralDirectionQuantizationResult> QuantizeBinauralDirectionAsync(
        BinauralDirectionQuantizationRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// [EN] Estimates low, mid, and high audio band energy.
    /// [JA] low / mid / high audio band energy を推定します。
    /// </summary>
    /// <param name="request">[EN] Frequency band request. [JA] frequency band request です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視するトークンです。</param>
    /// <returns>[EN] Frequency band result. [JA] frequency band result を返します。</returns>
    ValueTask<FrequencyBandEnergyResult> SplitFrequencyBandsAsync(
        FrequencyBandRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// [EN] Describes one semantic palette color target.
/// [JA] semantic palette の 1 つの color target を記述します。
/// </summary>
public sealed record SemanticPaletteColorDescriptor
{
    /// <summary>[EN] Gets the stable semantic label. [JA] stable な semantic label を取得します。</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>[EN] Gets the red channel target. [JA] red channel target を取得します。</summary>
    public byte Red { get; init; }

    /// <summary>[EN] Gets the green channel target. [JA] green channel target を取得します。</summary>
    public byte Green { get; init; }

    /// <summary>[EN] Gets the blue channel target. [JA] blue channel target を取得します。</summary>
    public byte Blue { get; init; }

    /// <summary>[EN] Gets the maximum RGB distance accepted for this color. [JA] この color に許容する最大 RGB distance を取得します。</summary>
    public double Tolerance { get; init; } = 96;

    /// <summary>[EN] Gets deterministic color metadata. [JA] deterministic color metadata を取得します。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// [EN] Carries semantic palette quantization input.
/// [JA] semantic palette quantization input を保持します。
/// </summary>
public sealed record SemanticPaletteQuantizationRequest
{
    /// <summary>[EN] Gets packed RGB bytes. [JA] packed RGB byte を取得します。</summary>
    public IReadOnlyList<byte> RgbBytes { get; init; } = [];

    /// <summary>[EN] Gets frame width in pixels. [JA] pixel 単位の frame width を取得します。</summary>
    public int Width { get; init; }

    /// <summary>[EN] Gets frame height in pixels. [JA] pixel 単位の frame height を取得します。</summary>
    public int Height { get; init; }

    /// <summary>[EN] Gets semantic palette colors. [JA] semantic palette color を取得します。</summary>
    public IReadOnlyList<SemanticPaletteColorDescriptor> Palette { get; init; } = [];
}

/// <summary>
/// [EN] Carries semantic palette quantization output.
/// [JA] semantic palette quantization output を保持します。
/// </summary>
public sealed record SemanticPaletteQuantizationResult
{
    /// <summary>[EN] Gets whether quantization succeeded. [JA] quantization が成功したかを取得します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Gets frame width in pixels. [JA] pixel 単位の frame width を取得します。</summary>
    public int Width { get; init; }

    /// <summary>[EN] Gets frame height in pixels. [JA] pixel 単位の frame height を取得します。</summary>
    public int Height { get; init; }

    /// <summary>[EN] Gets palette indexes per pixel where -1 means no match. [JA] pixel ごとの palette index を取得し、-1 は unmatched を表します。</summary>
    public IReadOnlyList<int> PaletteIndexes { get; init; } = [];

    /// <summary>[EN] Gets labels per pixel. [JA] pixel ごとの label を取得します。</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>[EN] Gets stable failure code. [JA] stable failure code を取得します。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Gets human-readable failure message. [JA] 人間可読 failure message を取得します。</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// [EN] Carries temporal difference input.
/// [JA] temporal difference input を保持します。
/// </summary>
public sealed record TemporalDifferenceRequest
{
    /// <summary>[EN] Gets previous scalar values. [JA] previous scalar value を取得します。</summary>
    public IReadOnlyList<double> Previous { get; init; } = [];

    /// <summary>[EN] Gets current scalar values. [JA] current scalar value を取得します。</summary>
    public IReadOnlyList<double> Current { get; init; } = [];

    /// <summary>[EN] Gets frame width. [JA] frame width を取得します。</summary>
    public int Width { get; init; }

    /// <summary>[EN] Gets frame height. [JA] frame height を取得します。</summary>
    public int Height { get; init; }

    /// <summary>[EN] Gets the minimum absolute delta retained in output. [JA] output に残す最小 absolute delta を取得します。</summary>
    public double Threshold { get; init; }
}

/// <summary>
/// [EN] Carries temporal difference output.
/// [JA] temporal difference output を保持します。
/// </summary>
public sealed record TemporalDifferenceResult
{
    /// <summary>[EN] Gets whether calculation succeeded. [JA] calculation が成功したかを取得します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Gets difference values. [JA] difference value を取得します。</summary>
    public IReadOnlyList<double> Values { get; init; } = [];

    /// <summary>[EN] Gets average absolute delta. [JA] average absolute delta を取得します。</summary>
    public double MeanDelta { get; init; }

    /// <summary>[EN] Gets maximum absolute delta. [JA] maximum absolute delta を取得します。</summary>
    public double MaxDelta { get; init; }

    /// <summary>[EN] Gets stable failure code. [JA] stable failure code を取得します。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Gets human-readable failure message. [JA] 人間可読 failure message を取得します。</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// [EN] Carries Laplacian edge detection input.
/// [JA] Laplacian edge detection input を保持します。
/// </summary>
public sealed record LaplacianEdgeRequest
{
    /// <summary>[EN] Gets scalar frame values. [JA] scalar frame value を取得します。</summary>
    public IReadOnlyList<double> Values { get; init; } = [];

    /// <summary>[EN] Gets frame width. [JA] frame width を取得します。</summary>
    public int Width { get; init; }

    /// <summary>[EN] Gets frame height. [JA] frame height を取得します。</summary>
    public int Height { get; init; }
}

/// <summary>
/// [EN] Carries Laplacian edge detection output.
/// [JA] Laplacian edge detection output を保持します。
/// </summary>
public sealed record LaplacianEdgeResult
{
    /// <summary>[EN] Gets whether detection succeeded. [JA] detection が成功したかを取得します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Gets edge strength values. [JA] edge strength value を取得します。</summary>
    public IReadOnlyList<double> Values { get; init; } = [];

    /// <summary>[EN] Gets maximum edge strength. [JA] maximum edge strength を取得します。</summary>
    public double MaxEdge { get; init; }

    /// <summary>[EN] Gets stable failure code. [JA] stable failure code を取得します。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Gets human-readable failure message. [JA] 人間可読 failure message を取得します。</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// [EN] Carries binaural direction quantization input.
/// [JA] binaural direction quantization input を保持します。
/// </summary>
public sealed record BinauralDirectionQuantizationRequest
{
    /// <summary>[EN] Gets interleaved PCM samples. [JA] interleaved PCM sample を取得します。</summary>
    public IReadOnlyList<float> InterleavedSamples { get; init; } = [];

    /// <summary>[EN] Gets the number of channels. [JA] channel 数を取得します。</summary>
    public int Channels { get; init; } = 2;

    /// <summary>[EN] Gets the number of output direction bins. [JA] output direction bin 数を取得します。</summary>
    public int DirectionBins { get; init; } = 8;
}

/// <summary>
/// [EN] Carries binaural direction quantization output.
/// [JA] binaural direction quantization output を保持します。
/// </summary>
public sealed record BinauralDirectionQuantizationResult
{
    /// <summary>[EN] Gets whether quantization succeeded. [JA] quantization が成功したかを取得します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Gets normalized right-minus-left balance. [JA] 正規化済み right-minus-left balance を取得します。</summary>
    public double Balance { get; init; }

    /// <summary>[EN] Gets estimated signed inter-channel delay in samples. [JA] sample 単位の signed inter-channel delay 推定値を取得します。</summary>
    public int DelaySamples { get; init; }

    /// <summary>[EN] Gets direction bin index. [JA] direction bin index を取得します。</summary>
    public int DirectionIndex { get; init; }

    /// <summary>[EN] Gets direction bin name. [JA] direction bin name を取得します。</summary>
    public string DirectionName { get; init; } = "unknown";

    /// <summary>[EN] Gets normalized confidence. [JA] 正規化済み confidence を取得します。</summary>
    public double Confidence { get; init; }

    /// <summary>[EN] Gets stable failure code. [JA] stable failure code を取得します。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Gets human-readable failure message. [JA] 人間可読 failure message を取得します。</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// [EN] Carries frequency band split input.
/// [JA] frequency band split input を保持します。
/// </summary>
public sealed record FrequencyBandRequest
{
    /// <summary>[EN] Gets PCM samples. [JA] PCM sample を取得します。</summary>
    public IReadOnlyList<float> Samples { get; init; } = [];

    /// <summary>[EN] Gets sample rate in hertz. [JA] hertz 単位の sample rate を取得します。</summary>
    public int SampleRate { get; init; } = 48_000;

    /// <summary>[EN] Gets the number of channels. [JA] channel 数を取得します。</summary>
    public int Channels { get; init; } = 1;
}

/// <summary>
/// [EN] Carries low, mid, and high frequency band energy.
/// [JA] low / mid / high frequency band energy を保持します。
/// </summary>
public sealed record FrequencyBandEnergyResult
{
    /// <summary>[EN] Gets whether split succeeded. [JA] split が成功したかを取得します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Gets normalized low-band energy. [JA] 正規化済み low-band energy を取得します。</summary>
    public double LowEnergy { get; init; }

    /// <summary>[EN] Gets normalized mid-band energy. [JA] 正規化済み mid-band energy を取得します。</summary>
    public double MidEnergy { get; init; }

    /// <summary>[EN] Gets normalized high-band energy. [JA] 正規化済み high-band energy を取得します。</summary>
    public double HighEnergy { get; init; }

    /// <summary>[EN] Gets dominant band name. [JA] dominant band name を取得します。</summary>
    public string DominantBand { get; init; } = "unknown";

    /// <summary>[EN] Gets stable failure code. [JA] stable failure code を取得します。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Gets human-readable failure message. [JA] 人間可読 failure message を取得します。</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// [EN] Carries scalar Kalman filter state.
/// [JA] scalar Kalman filter state を保持します。
/// </summary>
public readonly record struct KalmanFilterState
{
    /// <summary>[EN] Gets the estimated value. [JA] estimated value を取得します。</summary>
    public double Estimate { get; init; }

    /// <summary>[EN] Gets the error covariance. [JA] error covariance を取得します。</summary>
    public double ErrorCovariance { get; init; }
}

/// <summary>
/// [EN] Carries scalar spatial hash output.
/// [JA] scalar spatial hash output を保持します。
/// </summary>
public readonly record struct SpatialHashCell
{
    /// <summary>[EN] Gets quantized X cell. [JA] 量子化済み X cell を取得します。</summary>
    public int CellX { get; init; }

    /// <summary>[EN] Gets quantized Y cell. [JA] 量子化済み Y cell を取得します。</summary>
    public int CellY { get; init; }

    /// <summary>[EN] Gets stable cell key. [JA] stable cell key を取得します。</summary>
    public string Key { get; init; }
}

/// <summary>
/// [EN] Provides pure managed fallback implementations for common perception algorithms.
/// [JA] 共通 perception algorithm の pure managed fallback implementation を提供します。
/// </summary>
public static class PerceptionAlgorithmKernel
{
    private static readonly string[] Direction8 =
    [
        "left",
        "front-left",
        "front",
        "front-right",
        "right",
        "back-right",
        "back",
        "back-left"
    ];

    /// <summary>
    /// [EN] Quantizes packed RGB bytes into semantic palette labels.
    /// [JA] packed RGB byte を semantic palette label に量子化します。
    /// </summary>
    /// <param name="rgbBytes">[EN] Packed RGB bytes. [JA] packed RGB byte です。</param>
    /// <param name="width">[EN] Frame width. [JA] frame width です。</param>
    /// <param name="height">[EN] Frame height. [JA] frame height です。</param>
    /// <param name="palette">[EN] Semantic palette. [JA] semantic palette です。</param>
    /// <returns>[EN] Quantization result. [JA] quantization result を返します。</returns>
    public static SemanticPaletteQuantizationResult QuantizeSemanticPalette(
        ReadOnlySpan<byte> rgbBytes,
        int width,
        int height,
        IReadOnlyList<SemanticPaletteColorDescriptor> palette)
    {
        var pixelCount = width * height;
        if (width <= 0 || height <= 0 || rgbBytes.Length < pixelCount * 3 || palette.Count == 0)
        {
            return FailurePalette("PERCEPTION_PALETTE_INPUT_INVALID", "RGB input, dimensions, and palette are required.");
        }

        var indexes = new int[pixelCount];
        var labels = new string[pixelCount];
        for (var pixel = 0; pixel < pixelCount; pixel++)
        {
            var offset = pixel * 3;
            var bestIndex = -1;
            var bestDistance = double.MaxValue;
            for (var paletteIndex = 0; paletteIndex < palette.Count; paletteIndex++)
            {
                var color = palette[paletteIndex];
                var distance = RgbDistance(rgbBytes[offset], rgbBytes[offset + 1], rgbBytes[offset + 2], color);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = paletteIndex;
                }
            }

            var match = bestIndex >= 0 && bestDistance <= Math.Max(0, palette[bestIndex].Tolerance);
            indexes[pixel] = match ? bestIndex : -1;
            labels[pixel] = match ? palette[bestIndex].Label : "unknown";
        }

        return new SemanticPaletteQuantizationResult
        {
            Succeeded = true,
            Width = width,
            Height = height,
            PaletteIndexes = indexes,
            Labels = labels
        };
    }

    /// <summary>
    /// [EN] Calculates thresholded absolute temporal differences.
    /// [JA] threshold 適用済み absolute temporal difference を計算します。
    /// </summary>
    /// <param name="previous">[EN] Previous scalar values. [JA] previous scalar value です。</param>
    /// <param name="current">[EN] Current scalar values. [JA] current scalar value です。</param>
    /// <param name="width">[EN] Frame width. [JA] frame width です。</param>
    /// <param name="height">[EN] Frame height. [JA] frame height です。</param>
    /// <param name="threshold">[EN] Minimum absolute delta. [JA] 最小 absolute delta です。</param>
    /// <returns>[EN] Temporal difference result. [JA] temporal difference result を返します。</returns>
    public static TemporalDifferenceResult CalculateTemporalDifference(
        ReadOnlySpan<double> previous,
        ReadOnlySpan<double> current,
        int width,
        int height,
        double threshold = 0)
    {
        var count = width * height;
        if (width <= 0 || height <= 0 || previous.Length < count || current.Length < count)
        {
            return FailureTemporal("PERCEPTION_TEMPORAL_INPUT_INVALID", "Previous/current scalar maps and dimensions are required.");
        }

        var values = new double[count];
        var total = 0.0;
        var max = 0.0;
        var safeThreshold = Math.Max(0, threshold);
        for (var index = 0; index < count; index++)
        {
            var delta = Math.Abs(current[index] - previous[index]);
            var retained = delta >= safeThreshold ? delta : 0;
            values[index] = retained;
            total += delta;
            max = Math.Max(max, delta);
        }

        return new TemporalDifferenceResult
        {
            Succeeded = true,
            Values = values,
            MeanDelta = Round4(total / count),
            MaxDelta = Round4(max)
        };
    }

    /// <summary>
    /// [EN] Calculates 4-neighbor Laplacian edge strength.
    /// [JA] 4-neighbor Laplacian edge strength を計算します。
    /// </summary>
    /// <param name="values">[EN] Scalar frame values. [JA] scalar frame value です。</param>
    /// <param name="width">[EN] Frame width. [JA] frame width です。</param>
    /// <param name="height">[EN] Frame height. [JA] frame height です。</param>
    /// <returns>[EN] Edge result. [JA] edge result を返します。</returns>
    public static LaplacianEdgeResult DetectLaplacianEdges(ReadOnlySpan<double> values, int width, int height)
    {
        var count = width * height;
        if (width <= 1 || height <= 1 || values.Length < count)
        {
            return FailureEdges("PERCEPTION_EDGE_INPUT_INVALID", "Scalar frame map must include at least a 2x2 region.");
        }

        var edges = new double[count];
        var max = 0.0;
        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var index = y * width + x;
                var edge = Math.Abs((4 * values[index]) - values[index - 1] - values[index + 1] - values[index - width] - values[index + width]);
                edges[index] = edge;
                max = Math.Max(max, edge);
            }
        }

        if (max > 0)
        {
            for (var index = 0; index < edges.Length; index++)
            {
                edges[index] = Round4(edges[index] / max);
            }
        }

        return new LaplacianEdgeResult
        {
            Succeeded = true,
            Values = edges,
            MaxEdge = Round4(max)
        };
    }

    /// <summary>
    /// [EN] Quantizes stereo balance and short-lag correlation into a direction bin.
    /// [JA] stereo balance と short-lag correlation を direction bin に量子化します。
    /// </summary>
    /// <param name="interleavedSamples">[EN] Interleaved PCM samples. [JA] interleaved PCM sample です。</param>
    /// <param name="channels">[EN] Channel count. [JA] channel count です。</param>
    /// <param name="directionBins">[EN] Direction bin count. [JA] direction bin count です。</param>
    /// <returns>[EN] Direction quantization result. [JA] direction quantization result を返します。</returns>
    public static BinauralDirectionQuantizationResult QuantizeBinauralDirection(
        ReadOnlySpan<float> interleavedSamples,
        int channels = 2,
        int directionBins = 8)
    {
        if (channels < 2 || directionBins < 2 || interleavedSamples.Length < channels * 2)
        {
            return FailureBinaural("PERCEPTION_BINAURAL_INPUT_INVALID", "Stereo PCM input is required.");
        }

        var frames = interleavedSamples.Length / channels;
        var leftEnergy = 0.0;
        var rightEnergy = 0.0;
        for (var frame = 0; frame < frames; frame++)
        {
            var left = interleavedSamples[frame * channels];
            var right = interleavedSamples[(frame * channels) + 1];
            leftEnergy += Math.Abs(left);
            rightEnergy += Math.Abs(right);
        }

        var total = Math.Max(1e-9, leftEnergy + rightEnergy);
        var balance = ClampSigned((rightEnergy - leftEnergy) / total);
        var delay = EstimateStereoDelay(interleavedSamples, channels, frames);
        var directionIndex = Math.Clamp((int)Math.Round(((balance + 1) * 0.5) * (directionBins - 1), MidpointRounding.AwayFromZero), 0, directionBins - 1);
        var name = directionBins == 8 ? Direction8[directionIndex] : directionIndex.ToString(CultureInfo.InvariantCulture);
        return new BinauralDirectionQuantizationResult
        {
            Succeeded = true,
            Balance = Round4(balance),
            DelaySamples = delay,
            DirectionIndex = directionIndex,
            DirectionName = name,
            Confidence = Round4(Clamp01(Math.Abs(balance) + Math.Min(0.25, Math.Abs(delay) * 0.025)))
        };
    }

    /// <summary>
    /// [EN] Splits PCM samples into low, mid, and high energy bands using deterministic DFT bins.
    /// [JA] deterministic な DFT bin により PCM sample を low / mid / high energy band に分割します。
    /// </summary>
    /// <param name="samples">[EN] PCM samples. [JA] PCM sample です。</param>
    /// <param name="sampleRate">[EN] Sample rate in hertz. [JA] hertz 単位の sample rate です。</param>
    /// <param name="channels">[EN] Channel count. [JA] channel count です。</param>
    /// <returns>[EN] Frequency band result. [JA] frequency band result を返します。</returns>
    public static FrequencyBandEnergyResult SplitFrequencyBands(
        ReadOnlySpan<float> samples,
        int sampleRate,
        int channels = 1)
    {
        if (sampleRate <= 0 || channels <= 0 || samples.Length < channels * 8)
        {
            return FailureFrequency("PERCEPTION_FREQUENCY_INPUT_INVALID", "PCM samples, channel count, and sample rate are required.");
        }

        var frameCount = Math.Min(1024, samples.Length / channels);
        var low = 0.0;
        var mid = 0.0;
        var high = 0.0;
        var bins = Math.Max(3, Math.Min(frameCount / 2, 96));
        for (var bin = 1; bin <= bins; bin++)
        {
            var frequency = (double)bin * sampleRate / frameCount;
            var power = DftPower(samples, channels, frameCount, bin);
            if (frequency <= 250)
            {
                low += power;
            }
            else if (frequency <= 2_000)
            {
                mid += power;
            }
            else
            {
                high += power;
            }
        }

        var total = Math.Max(1e-9, low + mid + high);
        low /= total;
        mid /= total;
        high /= total;
        var dominant = low >= mid && low >= high ? "low" : (mid >= high ? "mid" : "high");
        return new FrequencyBandEnergyResult
        {
            Succeeded = true,
            LowEnergy = Round4(low),
            MidEnergy = Round4(mid),
            HighEnergy = Round4(high),
            DominantBand = dominant
        };
    }

    /// <summary>
    /// [EN] Applies an exponential moving average update.
    /// [JA] exponential moving average update を適用します。
    /// </summary>
    /// <param name="previous">[EN] Previous value. [JA] previous value です。</param>
    /// <param name="current">[EN] Current value. [JA] current value です。</param>
    /// <param name="alpha">[EN] Smoothing factor from 0 to 1. [JA] 0 から 1 の smoothing factor です。</param>
    /// <returns>[EN] Updated value. [JA] 更新済み value を返します。</returns>
    public static double ApplyEma(double previous, double current, double alpha)
        => Round4((previous * (1 - Clamp01(alpha))) + (current * Clamp01(alpha)));

    /// <summary>
    /// [EN] Applies a leaky integrator update.
    /// [JA] leaky integrator update を適用します。
    /// </summary>
    /// <param name="previous">[EN] Previous accumulator value. [JA] previous accumulator value です。</param>
    /// <param name="input">[EN] Input impulse value. [JA] input impulse value です。</param>
    /// <param name="leakRate">[EN] Leak rate from 0 to 1. [JA] 0 から 1 の leak rate です。</param>
    /// <param name="maxValue">[EN] Maximum accumulator value. [JA] maximum accumulator value です。</param>
    /// <returns>[EN] Updated accumulator value. [JA] 更新済み accumulator value を返します。</returns>
    public static double ApplyLeakyIntegrator(double previous, double input, double leakRate, double maxValue = 1)
        => Round4(Math.Clamp((previous * (1 - Clamp01(leakRate))) + input, 0, Math.Max(0, maxValue)));

    /// <summary>
    /// [EN] Quantizes a continuous point into a grid-based spatial hash cell.
    /// [JA] continuous point を grid-based spatial hash cell に量子化します。
    /// </summary>
    /// <param name="x">[EN] X coordinate. [JA] X coordinate です。</param>
    /// <param name="y">[EN] Y coordinate. [JA] Y coordinate です。</param>
    /// <param name="cellSize">[EN] Cell size. [JA] cell size です。</param>
    /// <returns>[EN] Spatial hash cell. [JA] spatial hash cell を返します。</returns>
    public static SpatialHashCell QuantizeSpatialHash(double x, double y, double cellSize)
    {
        var size = cellSize > 0 ? cellSize : 1;
        var cellX = (int)Math.Floor(x / size);
        var cellY = (int)Math.Floor(y / size);
        return new SpatialHashCell
        {
            CellX = cellX,
            CellY = cellY,
            Key = string.Create(CultureInfo.InvariantCulture, $"{cellX}:{cellY}")
        };
    }

    /// <summary>
    /// [EN] Applies one scalar Kalman filter update.
    /// [JA] 1 回の scalar Kalman filter update を適用します。
    /// </summary>
    /// <param name="state">[EN] Previous Kalman state. [JA] previous Kalman state です。</param>
    /// <param name="measurement">[EN] Measurement value. [JA] measurement value です。</param>
    /// <param name="control">[EN] Control prediction delta. [JA] control prediction delta です。</param>
    /// <param name="processNoise">[EN] Process noise. [JA] process noise です。</param>
    /// <param name="measurementNoise">[EN] Measurement noise. [JA] measurement noise です。</param>
    /// <returns>[EN] Updated Kalman state. [JA] 更新済み Kalman state を返します。</returns>
    public static KalmanFilterState ApplyKalman(
        KalmanFilterState state,
        double measurement,
        double control,
        double processNoise,
        double measurementNoise)
    {
        var predictedEstimate = state.Estimate + control;
        var predictedCovariance = Math.Max(0, state.ErrorCovariance) + Math.Max(0, processNoise);
        var gain = predictedCovariance / Math.Max(1e-9, predictedCovariance + Math.Max(0, measurementNoise));
        return new KalmanFilterState
        {
            Estimate = Round4(predictedEstimate + (gain * (measurement - predictedEstimate))),
            ErrorCovariance = Round4((1 - gain) * predictedCovariance)
        };
    }

    private static double RgbDistance(byte red, byte green, byte blue, SemanticPaletteColorDescriptor color)
    {
        var dr = red - color.Red;
        var dg = green - color.Green;
        var db = blue - color.Blue;
        return Math.Sqrt((dr * dr) + (dg * dg) + (db * db));
    }

    private static int EstimateStereoDelay(ReadOnlySpan<float> samples, int channels, int frames)
    {
        const int maxLag = 8;
        var bestLag = 0;
        var bestScore = double.MinValue;
        for (var lag = -maxLag; lag <= maxLag; lag++)
        {
            var score = 0.0;
            for (var frame = Math.Max(0, -lag); frame < Math.Min(frames, frames - lag); frame++)
            {
                score += samples[frame * channels] * samples[((frame + lag) * channels) + 1];
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestLag = lag;
            }
        }

        return bestLag;
    }

    private static double DftPower(ReadOnlySpan<float> samples, int channels, int frames, int bin)
    {
        var real = 0.0;
        var imag = 0.0;
        for (var frame = 0; frame < frames; frame++)
        {
            var sample = 0.0;
            for (var channel = 0; channel < channels; channel++)
            {
                sample += samples[(frame * channels) + channel];
            }

            sample /= channels;
            var angle = -2 * Math.PI * bin * frame / frames;
            real += sample * Math.Cos(angle);
            imag += sample * Math.Sin(angle);
        }

        return (real * real) + (imag * imag);
    }

    private static SemanticPaletteQuantizationResult FailurePalette(string code, string message)
        => new() { ErrorCode = code, ErrorMessage = message };

    private static TemporalDifferenceResult FailureTemporal(string code, string message)
        => new() { ErrorCode = code, ErrorMessage = message };

    private static LaplacianEdgeResult FailureEdges(string code, string message)
        => new() { ErrorCode = code, ErrorMessage = message };

    private static BinauralDirectionQuantizationResult FailureBinaural(string code, string message)
        => new() { ErrorCode = code, ErrorMessage = message };

    private static FrequencyBandEnergyResult FailureFrequency(string code, string message)
        => new() { ErrorCode = code, ErrorMessage = message };

    private static double Clamp01(double value)
        => Math.Clamp(value, 0, 1);

    private static double ClampSigned(double value)
        => Math.Clamp(value, -1, 1);

    private static double Round4(double value)
        => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}

/// <summary>
/// [EN] Implements provider-neutral perception algorithm interfaces using pure managed fallbacks.
/// [JA] pure managed fallback により provider-neutral な perception algorithm interface を実装します。
/// </summary>
public sealed class DefaultPerceptionAlgorithmKernel : IPerceptionAlgorithmKernel
{
    /// <summary>
    /// [EN] Initializes a provider-neutral perception algorithm kernel.
    /// [JA] provider-neutral な perception algorithm kernel を初期化します。
    /// </summary>
    public DefaultPerceptionAlgorithmKernel()
    {
    }

    /// <summary>
    /// [EN] Quantizes RGB pixels into semantic palette bins with the managed fallback implementation.
    /// [JA] managed fallback 実装により RGB pixel を semantic palette bin へ量子化します。
    /// </summary>
    /// <param name="request">[EN] Palette quantization input. [JA] palette quantization の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Palette quantization result. [JA] palette quantization の結果を返します。</returns>
    public ValueTask<SemanticPaletteQuantizationResult> QuantizePaletteAsync(
        SemanticPaletteQuantizationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(PerceptionAlgorithmKernel.QuantizeSemanticPalette(
            CopyBytes(request.RgbBytes),
            request.Width,
            request.Height,
            request.Palette));
    }

    /// <summary>
    /// [EN] Calculates temporal differences between two scalar frames.
    /// [JA] 2 つの scalar frame 間の temporal difference を計算します。
    /// </summary>
    /// <param name="request">[EN] Temporal difference input. [JA] temporal difference の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Temporal difference result. [JA] temporal difference の結果を返します。</returns>
    public ValueTask<TemporalDifferenceResult> CalculateTemporalDifferenceAsync(
        TemporalDifferenceRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(PerceptionAlgorithmKernel.CalculateTemporalDifference(
            CopyDoubles(request.Previous),
            CopyDoubles(request.Current),
            request.Width,
            request.Height,
            request.Threshold));
    }

    /// <summary>
    /// [EN] Detects Laplacian edges from a scalar frame.
    /// [JA] scalar frame から Laplacian edge を検出します。
    /// </summary>
    /// <param name="request">[EN] Edge detection input. [JA] edge detection の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Edge detection result. [JA] edge detection の結果を返します。</returns>
    public ValueTask<LaplacianEdgeResult> DetectEdgesAsync(
        LaplacianEdgeRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(PerceptionAlgorithmKernel.DetectLaplacianEdges(
            CopyDoubles(request.Values),
            request.Width,
            request.Height));
    }

    /// <summary>
    /// [EN] Quantizes stereo energy balance into a provider-neutral binaural direction.
    /// [JA] stereo energy balance を provider-neutral な binaural direction へ量子化します。
    /// </summary>
    /// <param name="request">[EN] Binaural quantization input. [JA] binaural quantization の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Binaural direction quantization result. [JA] binaural direction quantization の結果を返します。</returns>
    public ValueTask<BinauralDirectionQuantizationResult> QuantizeBinauralDirectionAsync(
        BinauralDirectionQuantizationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(PerceptionAlgorithmKernel.QuantizeBinauralDirection(
            CopyFloats(request.InterleavedSamples),
            request.Channels,
            request.DirectionBins));
    }

    /// <summary>
    /// [EN] Splits PCM samples into frequency-band energy carriers.
    /// [JA] PCM sample を frequency-band energy carrier へ分割します。
    /// </summary>
    /// <param name="request">[EN] Frequency band input. [JA] frequency band の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Frequency-band energy result. [JA] frequency-band energy の結果を返します。</returns>
    public ValueTask<FrequencyBandEnergyResult> SplitFrequencyBandsAsync(
        FrequencyBandRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(PerceptionAlgorithmKernel.SplitFrequencyBands(
            CopyFloats(request.Samples),
            request.SampleRate,
            request.Channels));
    }

    private static byte[] CopyBytes(IReadOnlyList<byte> values)
    {
        var copy = new byte[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            copy[index] = values[index];
        }

        return copy;
    }

    private static double[] CopyDoubles(IReadOnlyList<double> values)
    {
        var copy = new double[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            copy[index] = values[index];
        }

        return copy;
    }

    private static float[] CopyFloats(IReadOnlyList<float> values)
    {
        var copy = new float[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            copy[index] = values[index];
        }

        return copy;
    }
}
