namespace AIKernel.Providers.Perception;

using System.Globalization;

/// <summary>
/// [EN] Provides pure managed resident perception algorithm fallbacks.
/// [JA] resident perception algorithm の pure managed fallback を提供します。
/// </summary>
public static class ResidentPerceptionAlgorithmKernel
{
    /// <summary>
    /// [EN] Creates a HSV threshold mask from packed RGB bytes.
    /// [JA] packed RGB byte から HSV threshold mask を作成します。
    /// </summary>
    /// <param name="rgbBytes">[EN] Packed RGB bytes. [JA] packed RGB byte です。</param>
    /// <param name="width">[EN] Frame width. [JA] frame width です。</param>
    /// <param name="height">[EN] Frame height. [JA] frame height です。</param>
    /// <param name="minHue">[EN] Minimum hue. [JA] minimum hue です。</param>
    /// <param name="maxHue">[EN] Maximum hue. [JA] maximum hue です。</param>
    /// <param name="minSaturation">[EN] Minimum saturation. [JA] minimum saturation です。</param>
    /// <param name="minValue">[EN] Minimum value. [JA] minimum value です。</param>
    /// <param name="ignoreValue">[EN] Whether value should be ignored. [JA] value を無視するかどうかです。</param>
    /// <returns>[EN] HSV threshold mask result. [JA] HSV threshold mask result を返します。</returns>
    public static HsvThresholdMaskResult CreateHsvThresholdMask(
        ReadOnlySpan<byte> rgbBytes,
        int width,
        int height,
        double minHue,
        double maxHue,
        double minSaturation,
        double minValue,
        bool ignoreValue)
    {
        var count = width * height;
        if (width <= 0 || height <= 0 || rgbBytes.Length < count * 3)
        {
            return FailureHsv("PERCEPTION_HSV_INPUT_INVALID", "RGB input and dimensions are required.");
        }

        var mask = new byte[count];
        var matched = 0;
        for (var pixel = 0; pixel < count; pixel++)
        {
            var offset = pixel * 3;
            var hsv = RgbToHsv(rgbBytes[offset], rgbBytes[offset + 1], rgbBytes[offset + 2]);
            var hueMatch = HueInRange(hsv.Hue, minHue, maxHue);
            var valueMatch = ignoreValue || hsv.Value >= minValue;
            if (hueMatch && hsv.Saturation >= minSaturation && valueMatch)
            {
                mask[pixel] = 1;
                matched += 1;
            }
        }

        return new HsvThresholdMaskResult
        {
            Succeeded = true,
            Mask = mask,
            MatchRatio = Round4((double)matched / count)
        };
    }

    /// <summary>
    /// [EN] Performs max pooling over scalar values.
    /// [JA] scalar value に max pooling を実行します。
    /// </summary>
    /// <param name="values">[EN] Input values. [JA] input value です。</param>
    /// <param name="width">[EN] Input width. [JA] input width です。</param>
    /// <param name="height">[EN] Input height. [JA] input height です。</param>
    /// <param name="outputWidth">[EN] Output width. [JA] output width です。</param>
    /// <param name="outputHeight">[EN] Output height. [JA] output height です。</param>
    /// <returns>[EN] Max pooling result. [JA] max pooling result を返します。</returns>
    public static MaxPoolingResult MaxPool(
        ReadOnlySpan<double> values,
        int width,
        int height,
        int outputWidth,
        int outputHeight)
    {
        if (width <= 0 || height <= 0 || outputWidth <= 0 || outputHeight <= 0 || values.Length < width * height)
        {
            return FailureMaxPool("PERCEPTION_MAX_POOL_INPUT_INVALID", "Input/output dimensions and values are required.");
        }

        var output = new double[outputWidth * outputHeight];
        for (var oy = 0; oy < outputHeight; oy++)
        {
            var y0 = oy * height / outputHeight;
            var y1 = Math.Max(y0 + 1, (oy + 1) * height / outputHeight);
            for (var ox = 0; ox < outputWidth; ox++)
            {
                var x0 = ox * width / outputWidth;
                var x1 = Math.Max(x0 + 1, (ox + 1) * width / outputWidth);
                var max = double.MinValue;
                for (var y = y0; y < y1; y++)
                {
                    for (var x = x0; x < x1; x++)
                    {
                        max = Math.Max(max, values[(y * width) + x]);
                    }
                }

                output[(oy * outputWidth) + ox] = max == double.MinValue ? 0 : Round4(max);
            }
        }

        return new MaxPoolingResult
        {
            Succeeded = true,
            Values = output,
            OutputWidth = outputWidth,
            OutputHeight = outputHeight
        };
    }

    /// <summary>
    /// [EN] Applies dilation or erosion over scalar masks.
    /// [JA] scalar mask に dilation または erosion を適用します。
    /// </summary>
    /// <param name="values">[EN] Input values. [JA] input value です。</param>
    /// <param name="width">[EN] Width. [JA] width です。</param>
    /// <param name="height">[EN] Height. [JA] height です。</param>
    /// <param name="operation">[EN] Operation name. [JA] operation 名です。</param>
    /// <param name="radius">[EN] Radius. [JA] radius です。</param>
    /// <returns>[EN] Morphology result. [JA] morphology result を返します。</returns>
    public static MorphologyResult ApplyMorphology(
        ReadOnlySpan<double> values,
        int width,
        int height,
        string operation,
        int radius)
    {
        if (width <= 0 || height <= 0 || values.Length < width * height)
        {
            return FailureMorphology("PERCEPTION_MORPHOLOGY_INPUT_INVALID", "Mask values and dimensions are required.");
        }

        var op = string.IsNullOrWhiteSpace(operation) ? "dilation" : operation.Trim().ToLowerInvariant();
        var useErosion = op == "erosion";
        var safeRadius = Math.Max(1, radius);
        var output = new double[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = useErosion ? double.MaxValue : double.MinValue;
                for (var dy = -safeRadius; dy <= safeRadius; dy++)
                {
                    var yy = Math.Clamp(y + dy, 0, height - 1);
                    for (var dx = -safeRadius; dx <= safeRadius; dx++)
                    {
                        var xx = Math.Clamp(x + dx, 0, width - 1);
                        var candidate = values[(yy * width) + xx];
                        value = useErosion ? Math.Min(value, candidate) : Math.Max(value, candidate);
                    }
                }

                output[(y * width) + x] = Round4(value);
            }
        }

        return new MorphologyResult { Succeeded = true, Values = output };
    }

    /// <summary>
    /// [EN] Estimates dense optical flow with local block matching.
    /// [JA] local block matching により dense optical flow を推定します。
    /// </summary>
    /// <param name="previous">[EN] Previous values. [JA] previous value です。</param>
    /// <param name="current">[EN] Current values. [JA] current value です。</param>
    /// <param name="width">[EN] Width. [JA] width です。</param>
    /// <param name="height">[EN] Height. [JA] height です。</param>
    /// <param name="searchRadius">[EN] Search radius. [JA] search radius です。</param>
    /// <returns>[EN] Optical flow result. [JA] optical flow result を返します。</returns>
    public static DenseOpticalFlowResult EstimateDenseOpticalFlow(
        ReadOnlySpan<double> previous,
        ReadOnlySpan<double> current,
        int width,
        int height,
        int searchRadius)
    {
        if (width <= 1 || height <= 1 || previous.Length < width * height || current.Length < width * height)
        {
            return FailureOpticalFlow("PERCEPTION_OPTICAL_FLOW_INPUT_INVALID", "Previous/current frames and dimensions are required.");
        }

        var radius = Math.Clamp(searchRadius, 1, 4);
        var vectors = new OpticalFlowVector[width * height];
        var totalX = 0.0;
        var totalY = 0.0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = (y * width) + x;
                var target = current[index];
                var bestDx = 0;
                var bestDy = 0;
                var bestError = double.MaxValue;
                for (var dy = -radius; dy <= radius; dy++)
                {
                    var yy = Math.Clamp(y + dy, 0, height - 1);
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var xx = Math.Clamp(x + dx, 0, width - 1);
                        var error = Math.Abs(target - previous[(yy * width) + xx]);
                        if (error < bestError)
                        {
                            bestError = error;
                            bestDx = dx;
                            bestDy = dy;
                        }
                    }
                }

                var confidence = 1 / (1 + bestError);
                vectors[index] = new OpticalFlowVector
                {
                    X = bestDx,
                    Y = bestDy,
                    Confidence = Round4(confidence)
                };
                totalX += bestDx;
                totalY += bestDy;
            }
        }

        return new DenseOpticalFlowResult
        {
            Succeeded = true,
            Vectors = vectors,
            AverageX = Round4(totalX / vectors.Length),
            AverageY = Round4(totalY / vectors.Length)
        };
    }

    /// <summary>
    /// [EN] Computes an audio spectrum using a radix-2 FFT fallback.
    /// [JA] radix-2 FFT fallback により audio spectrum を計算します。
    /// </summary>
    /// <param name="samples">[EN] PCM samples. [JA] PCM sample です。</param>
    /// <param name="channels">[EN] Channel count. [JA] channel count です。</param>
    /// <param name="maxFftSize">[EN] Maximum FFT size. [JA] maximum FFT size です。</param>
    /// <returns>[EN] Audio spectrum result. [JA] audio spectrum result を返します。</returns>
    public static AudioSpectrumResult ComputeAudioSpectrum(
        ReadOnlySpan<float> samples,
        int channels,
        int maxFftSize)
    {
        if (channels <= 0 || samples.Length < channels * 2)
        {
            return FailureSpectrum("PERCEPTION_SPECTRUM_INPUT_INVALID", "PCM samples and channel count are required.");
        }

        var frames = samples.Length / channels;
        var fftSize = PreviousPowerOfTwo(Math.Min(Math.Max(2, maxFftSize), frames));
        if (fftSize < 2)
        {
            return FailureSpectrum("PERCEPTION_SPECTRUM_SIZE_INVALID", "FFT size must be at least 2.");
        }

        var real = new double[fftSize];
        var imaginary = new double[fftSize];
        for (var index = 0; index < fftSize; index++)
        {
            var value = 0.0;
            for (var channel = 0; channel < channels; channel++)
            {
                value += samples[(index * channels) + channel];
            }

            real[index] = value / channels;
        }

        ApplyRadix2Fft(real, imaginary);
        var magnitudes = new double[fftSize / 2];
        var max = 0.0;
        for (var index = 0; index < magnitudes.Length; index++)
        {
            var magnitude = Math.Sqrt((real[index] * real[index]) + (imaginary[index] * imaginary[index]));
            magnitudes[index] = magnitude;
            max = Math.Max(max, magnitude);
        }

        if (max > 0)
        {
            for (var index = 0; index < magnitudes.Length; index++)
            {
                magnitudes[index] = Round4(magnitudes[index] / max);
            }
        }

        return new AudioSpectrumResult
        {
            Succeeded = true,
            Magnitudes = magnitudes,
            FftSize = fftSize
        };
    }

    /// <summary>
    /// [EN] Applies exponential moving average to value arrays.
    /// [JA] value array に exponential moving average を適用します。
    /// </summary>
    /// <param name="previous">[EN] Previous values. [JA] previous value です。</param>
    /// <param name="current">[EN] Current values. [JA] current value です。</param>
    /// <param name="alpha">[EN] Smoothing factor. [JA] smoothing factor です。</param>
    /// <returns>[EN] EMA result. [JA] EMA result を返します。</returns>
    public static EmaFilterResult ApplyEma(ReadOnlySpan<double> previous, ReadOnlySpan<double> current, double alpha)
    {
        if (previous.Length == 0 || current.Length == 0 || previous.Length != current.Length)
        {
            return FailureEma("PERCEPTION_EMA_INPUT_INVALID", "Previous and current values must have the same non-zero length.");
        }

        var values = new double[current.Length];
        for (var index = 0; index < current.Length; index++)
        {
            values[index] = PerceptionAlgorithmKernel.ApplyEma(previous[index], current[index], alpha);
        }

        return new EmaFilterResult { Succeeded = true, Values = values };
    }

    /// <summary>
    /// [EN] Applies leaky integration to value arrays.
    /// [JA] value array に leaky integration を適用します。
    /// </summary>
    /// <param name="previous">[EN] Previous values. [JA] previous value です。</param>
    /// <param name="inputs">[EN] Input impulses. [JA] input impulse です。</param>
    /// <param name="leakRate">[EN] Leak rate. [JA] leak rate です。</param>
    /// <param name="maxValue">[EN] Maximum value. [JA] maximum value です。</param>
    /// <returns>[EN] Leaky integrator result. [JA] leaky integrator result を返します。</returns>
    public static LeakyIntegratorResult ApplyLeakyIntegrator(
        ReadOnlySpan<double> previous,
        ReadOnlySpan<double> inputs,
        double leakRate,
        double maxValue)
    {
        if (previous.Length == 0 || inputs.Length == 0 || previous.Length != inputs.Length)
        {
            return FailureLeaky("PERCEPTION_LEAKY_INPUT_INVALID", "Previous values and inputs must have the same non-zero length.");
        }

        var values = new double[inputs.Length];
        for (var index = 0; index < inputs.Length; index++)
        {
            values[index] = PerceptionAlgorithmKernel.ApplyLeakyIntegrator(previous[index], inputs[index], leakRate, maxValue);
        }

        return new LeakyIntegratorResult { Succeeded = true, Values = values };
    }

    /// <summary>
    /// [EN] Quantizes coordinate arrays into spatial hash cells.
    /// [JA] coordinate array を spatial hash cell に量子化します。
    /// </summary>
    /// <param name="x">[EN] X coordinates. [JA] X coordinate です。</param>
    /// <param name="y">[EN] Y coordinates. [JA] Y coordinate です。</param>
    /// <param name="cellSize">[EN] Cell size. [JA] cell size です。</param>
    /// <returns>[EN] Spatial hash result. [JA] spatial hash result を返します。</returns>
    public static SpatialHashResult QuantizeSpatialHash(ReadOnlySpan<double> x, ReadOnlySpan<double> y, double cellSize)
    {
        if (x.Length == 0 || y.Length == 0 || x.Length != y.Length)
        {
            return FailureSpatialHash("PERCEPTION_SPATIAL_HASH_INPUT_INVALID", "X and Y arrays must have the same non-zero length.");
        }

        var cells = new SpatialHashCell[x.Length];
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < x.Length; index++)
        {
            var cell = PerceptionAlgorithmKernel.QuantizeSpatialHash(x[index], y[index], cellSize);
            cells[index] = cell;
            counts[cell.Key] = counts.TryGetValue(cell.Key, out var count) ? count + 1 : 1;
        }

        return new SpatialHashResult
        {
            Succeeded = true,
            Cells = cells,
            VisitCounts = counts.OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)
        };
    }

    /// <summary>
    /// [EN] Applies scalar Kalman updates to state arrays.
    /// [JA] state array に scalar Kalman update を適用します。
    /// </summary>
    /// <param name="states">[EN] Previous states. [JA] previous state です。</param>
    /// <param name="measurements">[EN] Measurements. [JA] measurement です。</param>
    /// <param name="controls">[EN] Controls. [JA] control です。</param>
    /// <param name="processNoise">[EN] Process noise. [JA] process noise です。</param>
    /// <param name="measurementNoise">[EN] Measurement noise. [JA] measurement noise です。</param>
    /// <returns>[EN] Kalman result. [JA] Kalman result を返します。</returns>
    public static KalmanFilterResult ApplyKalman(
        IReadOnlyList<KalmanFilterState> states,
        IReadOnlyList<double> measurements,
        IReadOnlyList<double> controls,
        double processNoise,
        double measurementNoise)
    {
        if (states.Count == 0 || measurements.Count != states.Count)
        {
            return FailureKalman("PERCEPTION_KALMAN_INPUT_INVALID", "States and measurements must have the same non-zero length.");
        }

        var output = new KalmanFilterState[states.Count];
        for (var index = 0; index < states.Count; index++)
        {
            var control = controls.Count == states.Count ? controls[index] : 0;
            output[index] = PerceptionAlgorithmKernel.ApplyKalman(
                states[index],
                measurements[index],
                control,
                processNoise,
                measurementNoise);
        }

        return new KalmanFilterResult { Succeeded = true, States = output };
    }

    private readonly record struct Hsv(double Hue, double Saturation, double Value);

    private static Hsv RgbToHsv(byte red, byte green, byte blue)
    {
        var r = red / 255.0;
        var g = green / 255.0;
        var b = blue / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var hue = 0.0;
        if (delta > 0)
        {
            if (Math.Abs(max - r) <= double.Epsilon)
            {
                hue = 60 * (((g - b) / delta) % 6);
            }
            else if (Math.Abs(max - g) <= double.Epsilon)
            {
                hue = 60 * (((b - r) / delta) + 2);
            }
            else
            {
                hue = 60 * (((r - g) / delta) + 4);
            }
        }

        if (hue < 0)
        {
            hue += 360;
        }

        return new Hsv(hue, max <= double.Epsilon ? 0 : delta / max, max);
    }

    private static bool HueInRange(double hue, double minHue, double maxHue)
    {
        var min = NormalizeHue(minHue);
        var max = NormalizeHue(maxHue);
        return min <= max
            ? hue >= min && hue <= max
            : hue >= min || hue <= max;
    }

    private static double NormalizeHue(double hue)
        => ((hue % 360) + 360) % 360;

    private static int PreviousPowerOfTwo(int value)
    {
        var power = 1;
        while (power * 2 <= value)
        {
            power *= 2;
        }

        return power;
    }

    private static void ApplyRadix2Fft(double[] real, double[] imaginary)
    {
        var n = real.Length;
        for (var i = 1; i < n; i++)
        {
            var bit = n >> 1;
            var j = 0;
            while ((i & bit) == 0)
            {
                bit >>= 1;
            }

            j = ((i & (bit - 1)) << 1) | bit | (i >> 1 & ~(bit - 1));
            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
            }
        }

        for (var length = 2; length <= n; length <<= 1)
        {
            var angle = -2 * Math.PI / length;
            var wlenReal = Math.Cos(angle);
            var wlenImaginary = Math.Sin(angle);
            for (var i = 0; i < n; i += length)
            {
                var wReal = 1.0;
                var wImaginary = 0.0;
                for (var j = 0; j < length / 2; j++)
                {
                    var even = i + j;
                    var odd = even + (length / 2);
                    var uReal = real[even];
                    var uImaginary = imaginary[even];
                    var vReal = (real[odd] * wReal) - (imaginary[odd] * wImaginary);
                    var vImaginary = (real[odd] * wImaginary) + (imaginary[odd] * wReal);
                    real[even] = uReal + vReal;
                    imaginary[even] = uImaginary + vImaginary;
                    real[odd] = uReal - vReal;
                    imaginary[odd] = uImaginary - vImaginary;
                    (wReal, wImaginary) = ((wReal * wlenReal) - (wImaginary * wlenImaginary), (wReal * wlenImaginary) + (wImaginary * wlenReal));
                }
            }
        }
    }

    private static HsvThresholdMaskResult FailureHsv(string code, string message)
        => new() { ErrorCode = code, ErrorMessage = message };

    private static MaxPoolingResult FailureMaxPool(string code, string message)
        => new() { ErrorCode = code, ErrorMessage = message };

    private static MorphologyResult FailureMorphology(string code, string message)
        => new() { ErrorCode = code, ErrorMessage = message };

    private static DenseOpticalFlowResult FailureOpticalFlow(string code, string message)
        => new() { ErrorCode = code, ErrorMessage = message };

    private static AudioSpectrumResult FailureSpectrum(string code, string message)
        => new() { ErrorCode = code, ErrorMessage = message };

    private static EmaFilterResult FailureEma(string code, string message)
        => new() { ErrorCode = code, ErrorMessage = message };

    private static LeakyIntegratorResult FailureLeaky(string code, string message)
        => new() { ErrorCode = code, ErrorMessage = message };

    private static SpatialHashResult FailureSpatialHash(string code, string message)
        => new() { ErrorCode = code, ErrorMessage = message };

    private static KalmanFilterResult FailureKalman(string code, string message)
        => new() { ErrorCode = code, ErrorMessage = message };

    private static double Round4(double value)
        => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}

/// <summary>
/// [EN] Implements resident perception algorithms with pure managed fallback logic.
/// [JA] pure managed fallback logic により resident perception algorithm を実装します。
/// </summary>
public sealed class DefaultResidentPerceptionAlgorithmKernel : IResidentPerceptionAlgorithmKernel
{
    private readonly DefaultPerceptionAlgorithmKernel _baseKernel = new();

    /// <summary>
    /// [EN] Initializes a resident perception algorithm kernel.
    /// [JA] resident perception algorithm kernel を初期化します。
    /// </summary>
    public DefaultResidentPerceptionAlgorithmKernel()
    {
    }

    /// <summary>
    /// [EN] Quantizes RGB pixels into semantic palette bins.
    /// [JA] RGB pixel を semantic palette bin へ量子化します。
    /// </summary>
    /// <param name="request">[EN] Palette quantization input. [JA] palette quantization の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Palette quantization result. [JA] palette quantization の結果を返します。</returns>
    public ValueTask<SemanticPaletteQuantizationResult> QuantizePaletteAsync(SemanticPaletteQuantizationRequest request, CancellationToken cancellationToken)
        => _baseKernel.QuantizePaletteAsync(request, cancellationToken);

    /// <summary>
    /// [EN] Calculates temporal differences between scalar frames.
    /// [JA] scalar frame 間の temporal difference を計算します。
    /// </summary>
    /// <param name="request">[EN] Temporal difference input. [JA] temporal difference の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Temporal difference result. [JA] temporal difference の結果を返します。</returns>
    public ValueTask<TemporalDifferenceResult> CalculateTemporalDifferenceAsync(TemporalDifferenceRequest request, CancellationToken cancellationToken)
        => _baseKernel.CalculateTemporalDifferenceAsync(request, cancellationToken);

    /// <summary>
    /// [EN] Detects Laplacian edges from scalar input.
    /// [JA] scalar input から Laplacian edge を検出します。
    /// </summary>
    /// <param name="request">[EN] Edge detection input. [JA] edge detection の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Edge detection result. [JA] edge detection の結果を返します。</returns>
    public ValueTask<LaplacianEdgeResult> DetectEdgesAsync(LaplacianEdgeRequest request, CancellationToken cancellationToken)
        => _baseKernel.DetectEdgesAsync(request, cancellationToken);

    /// <summary>
    /// [EN] Quantizes stereo PCM balance into a binaural direction.
    /// [JA] stereo PCM balance を binaural direction へ量子化します。
    /// </summary>
    /// <param name="request">[EN] Binaural quantization input. [JA] binaural quantization の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Binaural direction quantization result. [JA] binaural direction quantization の結果を返します。</returns>
    public ValueTask<BinauralDirectionQuantizationResult> QuantizeBinauralDirectionAsync(BinauralDirectionQuantizationRequest request, CancellationToken cancellationToken)
        => _baseKernel.QuantizeBinauralDirectionAsync(request, cancellationToken);

    /// <summary>
    /// [EN] Splits PCM samples into frequency-band energy carriers.
    /// [JA] PCM sample を frequency-band energy carrier へ分割します。
    /// </summary>
    /// <param name="request">[EN] Frequency band input. [JA] frequency band の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Frequency-band energy result. [JA] frequency-band energy の結果を返します。</returns>
    public ValueTask<FrequencyBandEnergyResult> SplitFrequencyBandsAsync(FrequencyBandRequest request, CancellationToken cancellationToken)
        => _baseKernel.SplitFrequencyBandsAsync(request, cancellationToken);

    /// <summary>
    /// [EN] Creates an HSV threshold mask from RGB input.
    /// [JA] RGB input から HSV threshold mask を作成します。
    /// </summary>
    /// <param name="request">[EN] HSV threshold input. [JA] HSV threshold の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] HSV threshold mask result. [JA] HSV threshold mask の結果を返します。</returns>
    public ValueTask<HsvThresholdMaskResult> CreateHsvThresholdMaskAsync(HsvThresholdMaskRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(ResidentPerceptionAlgorithmKernel.CreateHsvThresholdMask(CopyBytes(request.RgbBytes), request.Width, request.Height, request.MinHue, request.MaxHue, request.MinSaturation, request.MinValue, request.IgnoreValue));
    }

    /// <summary>
    /// [EN] Downsamples a scalar buffer by preserving block maxima.
    /// [JA] block maximum を保持して scalar buffer を downsample します。
    /// </summary>
    /// <param name="request">[EN] Max-pooling input. [JA] max-pooling の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Max-pooling result. [JA] max-pooling の結果を返します。</returns>
    public ValueTask<MaxPoolingResult> MaxPoolAsync(MaxPoolingRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(ResidentPerceptionAlgorithmKernel.MaxPool(CopyDoubles(request.Values), request.Width, request.Height, request.OutputWidth, request.OutputHeight));
    }

    /// <summary>
    /// [EN] Applies morphology to a scalar or binary mask.
    /// [JA] scalar または binary mask に morphology を適用します。
    /// </summary>
    /// <param name="request">[EN] Morphology input. [JA] morphology の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Morphology result. [JA] morphology の結果を返します。</returns>
    public ValueTask<MorphologyResult> ApplyMorphologyAsync(MorphologyRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(ResidentPerceptionAlgorithmKernel.ApplyMorphology(CopyDoubles(request.Values), request.Width, request.Height, request.Operation, request.Radius));
    }

    /// <summary>
    /// [EN] Estimates dense optical flow between two scalar frames.
    /// [JA] 2 つの scalar frame 間の dense optical flow を推定します。
    /// </summary>
    /// <param name="request">[EN] Optical-flow input. [JA] optical-flow の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Dense optical-flow result. [JA] dense optical-flow の結果を返します。</returns>
    public ValueTask<DenseOpticalFlowResult> EstimateDenseOpticalFlowAsync(DenseOpticalFlowRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(ResidentPerceptionAlgorithmKernel.EstimateDenseOpticalFlow(CopyDoubles(request.Previous), CopyDoubles(request.Current), request.Width, request.Height, request.SearchRadius));
    }

    /// <summary>
    /// [EN] Computes an audio spectrum from PCM samples.
    /// [JA] PCM sample から audio spectrum を計算します。
    /// </summary>
    /// <param name="request">[EN] Audio spectrum input. [JA] audio spectrum の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Audio spectrum result. [JA] audio spectrum の結果を返します。</returns>
    public ValueTask<AudioSpectrumResult> ComputeAudioSpectrumAsync(AudioSpectrumRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(ResidentPerceptionAlgorithmKernel.ComputeAudioSpectrum(CopyFloats(request.Samples), request.Channels, request.MaxFftSize));
    }

    /// <summary>
    /// [EN] Applies an exponential moving average to current observations.
    /// [JA] current observation に exponential moving average を適用します。
    /// </summary>
    /// <param name="request">[EN] EMA input. [JA] EMA の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] EMA result. [JA] EMA の結果を返します。</returns>
    public ValueTask<EmaFilterResult> ApplyEmaAsync(EmaFilterRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(ResidentPerceptionAlgorithmKernel.ApplyEma(CopyDoubles(request.Previous), CopyDoubles(request.Current), request.Alpha));
    }

    /// <summary>
    /// [EN] Applies a leaky integrator to short-term event state.
    /// [JA] short-term event state に leaky integrator を適用します。
    /// </summary>
    /// <param name="request">[EN] Leaky-integrator input. [JA] leaky-integrator の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Leaky-integrator result. [JA] leaky-integrator の結果を返します。</returns>
    public ValueTask<LeakyIntegratorResult> ApplyLeakyIntegratorAsync(LeakyIntegratorRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(ResidentPerceptionAlgorithmKernel.ApplyLeakyIntegrator(CopyDoubles(request.Previous), CopyDoubles(request.Inputs), request.LeakRate, request.MaxValue));
    }

    /// <summary>
    /// [EN] Quantizes continuous coordinates into spatial hash cells.
    /// [JA] continuous coordinate を spatial hash cell へ量子化します。
    /// </summary>
    /// <param name="request">[EN] Spatial-hash input. [JA] spatial-hash の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Spatial-hash result. [JA] spatial-hash の結果を返します。</returns>
    public ValueTask<SpatialHashResult> QuantizeSpatialHashAsync(SpatialHashRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(ResidentPerceptionAlgorithmKernel.QuantizeSpatialHash(CopyDoubles(request.X), CopyDoubles(request.Y), request.CellSize));
    }

    /// <summary>
    /// [EN] Applies a simple Kalman-style state update to observations.
    /// [JA] observation に simple Kalman-style state update を適用します。
    /// </summary>
    /// <param name="request">[EN] Kalman-filter input. [JA] Kalman-filter の入力です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視する token です。</param>
    /// <returns>[EN] Kalman-filter result. [JA] Kalman-filter の結果を返します。</returns>
    public ValueTask<KalmanFilterResult> ApplyKalmanAsync(KalmanFilterRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(ResidentPerceptionAlgorithmKernel.ApplyKalman(request.States, request.Measurements, request.Controls, request.ProcessNoise, request.MeasurementNoise));
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
