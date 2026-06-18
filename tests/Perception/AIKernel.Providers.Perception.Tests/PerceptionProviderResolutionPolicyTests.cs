namespace AIKernel.Providers.Perception.Tests;

using AIKernel.Providers.Perception.DependencyInjection;
using AIKernel.Providers.Perception.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// [EN] Tests perception provider substrate routing contracts.
/// [JA] perception Provider substrate routing contract をテストします。
/// </summary>
public sealed class PerceptionProviderResolutionPolicyTests
{
    /// <summary>
    /// [EN] Verifies frame policy capability remains deterministic.
    /// [JA] frame policy capability が deterministic に維持されることを検証します。
    /// </summary>
    [Fact]
    public void CreateFramePolicy_DefaultRequest_UsesFrameCapability()
    {
        var policy = new PerceptionProviderResolutionPolicy().CreateFramePolicy();

        Assert.Equal("perception.frame", policy.RequiredCapability);
    }

    /// <summary>
    /// [EN] Verifies service registration exposes routing helpers without concrete providers.
    /// [JA] service registration が concrete Provider なしで routing helper を公開することを検証します。
    /// </summary>
    [Fact]
    public void AddAIKernelPerceptionProviderSubstrate_DefaultServices_RegistersRoutingPolicy()
    {
        var services = new ServiceCollection();

        services.AddAIKernelPerceptionProviderSubstrate();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<PerceptionProviderResolutionPolicy>());
        Assert.NotNull(provider.GetService<ISensorStateNormalizer>());
        Assert.NotNull(provider.GetService<ISpatialSensorFusionKernel>());
        Assert.NotNull(provider.GetService<IRetryIntentResolver>());
        Assert.NotNull(provider.GetService<IPerceptionAlgorithmKernel>());
        Assert.NotNull(provider.GetService<IResidentPerceptionAlgorithmKernel>());
    }

    /// <summary>
    /// [EN] Verifies sensor normalization keeps extensible map semantics.
    /// [JA] sensor normalization が拡張可能な map semantics を維持することを検証します。
    /// </summary>
    [Fact]
    public async Task NormalizeAsync_CustomSensor_PreservesExtensibleSensorMap()
    {
        var normalizer = new SensorStateNormalizer();

        var sensors = await normalizer.NormalizeAsync(
            new Dictionary<string, SensorStateDescriptor>(StringComparer.Ordinal)
            {
                ["external-imu"] = new()
                {
                    Enabled = true,
                    Observed = true,
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["yaw"] = "12"
                    }
                }
            },
            TestContext.Current.CancellationToken);

        Assert.True(sensors.ContainsKey("visual"));
        Assert.True(sensors.ContainsKey("external-imu"));
        Assert.Equal("primary", sensors["external-imu"].Category);
        Assert.Equal("Aisthesis", sensors["visual"].ConceptName);
        Assert.Equal("Aisthesis", sensors["audio"].ConceptName);
        Assert.Equal("Kinesis", sensors["motor"].ConceptName);
        Assert.Equal("Phantasia", sensors["compass"].ConceptName);
        Assert.Equal("Aisthesis", sensors["health"].ConceptName);
        Assert.Equal("Phantasia", sensors["spatial"].ConceptName);
    }

    /// <summary>
    /// [EN] Verifies spatial fusion remains valid when the auditory sensor is cut off.
    /// [JA] auditory sensor が cut off されても spatial fusion が有効に維持されることを検証します。
    /// </summary>
    [Fact]
    public async Task FuseAsync_AudioCutOff_UsesVisualMotorCompassOnly()
    {
        var kernel = new DefaultSpatialSensorFusionKernel();
        var sensors = new Dictionary<string, SensorStateDescriptor>(StringComparer.Ordinal)
        {
            ["visual"] = Sensor("visual", true, true, 0.8, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["bias"] = "0.4",
                ["confidence"] = "0.6"
            }),
            ["audio"] = Sensor("audio", false, false, 0, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["balance"] = "1",
                ["leftEnergy"] = "1",
                ["rightEnergy"] = "1"
            }),
            ["motor"] = Sensor("motor", true, true, 0.5, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vectorX"] = "1"
            }),
            ["compass"] = Sensor("compass", true, true, 0.5, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["heading"] = "30",
                ["confidence"] = "0.5"
            }),
            ["spatial"] = Sensor("spatial", true, true, 0, new Dictionary<string, string>(StringComparer.Ordinal))
        };

        var fused = await kernel.FuseAsync(sensors, TestContext.Current.CancellationToken);

        Assert.NotEqual(0, fused.VisualDirection);
        Assert.Equal(0, fused.AuditoryCorrection);
        Assert.True(fused.Confidence > 0);
    }

    /// <summary>
    /// [EN] Verifies health-death detection emits a high-priority retry carrier.
    /// [JA] health-death detection が高優先 retry carrier を出力することを検証します。
    /// </summary>
    [Fact]
    public async Task ResolveAsync_HealthLikelyDead_ReturnsHighPriorityRetryCarrier()
    {
        var resolver = new HealthRetryIntentResolver();
        var sensors = new Dictionary<string, SensorStateDescriptor>(StringComparer.Ordinal)
        {
            ["health"] = Sensor("health", true, true, 0.95, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["likelyDead"] = "true",
                ["zeroScore"] = "0.91",
                ["faceQuantizedFrameChange"] = "2"
            })
        };

        var carrier = await resolver.ResolveAsync(sensors, TestContext.Current.CancellationToken);

        Assert.NotNull(carrier);
        Assert.True(carrier.Requested);
        Assert.Equal("health-death", carrier.ReasonCode);
        Assert.Equal(100, carrier.Priority);
    }

    /// <summary>
    /// [EN] Verifies semantic palette quantization classifies RGB colors deterministically.
    /// [JA] semantic palette quantization が RGB color を deterministic に分類することを検証します。
    /// </summary>
    [Fact]
    public async Task QuantizePaletteAsync_RgbPixels_ReturnsSemanticLabels()
    {
        var kernel = new DefaultResidentPerceptionAlgorithmKernel();

        var result = await kernel.QuantizePaletteAsync(
            new SemanticPaletteQuantizationRequest
            {
                Width = 2,
                Height = 1,
                RgbBytes = [250, 80, 20, 10, 220, 60],
                Palette =
                [
                    new SemanticPaletteColorDescriptor { Label = "warm", Red = 255, Green = 80, Blue = 16, Tolerance = 64 },
                    new SemanticPaletteColorDescriptor { Label = "green", Red = 16, Green = 220, Blue = 64, Tolerance = 64 }
                ]
            },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(["warm", "green"], result.Labels);
    }

    /// <summary>
    /// [EN] Verifies resident vision algorithms produce deterministic mask, pooling, morphology, and flow carriers.
    /// [JA] resident vision algorithm が deterministic な mask / pooling / morphology / flow carrier を生成することを検証します。
    /// </summary>
    [Fact]
    public async Task ResidentVisionAlgorithms_ValidInputs_ReturnDeterministicCarriers()
    {
        var kernel = new DefaultResidentPerceptionAlgorithmKernel();

        var hsv = await kernel.CreateHsvThresholdMaskAsync(
            new HsvThresholdMaskRequest
            {
                Width = 2,
                Height = 1,
                RgbBytes = [255, 80, 20, 20, 20, 20],
                MinHue = 0,
                MaxHue = 40,
                MinSaturation = 0.4,
                IgnoreValue = true
            },
            TestContext.Current.CancellationToken);
        var pooled = await kernel.MaxPoolAsync(
            new MaxPoolingRequest
            {
                Width = 4,
                Height = 4,
                OutputWidth = 2,
                OutputHeight = 2,
                Values = [0, 0, 0.2, 0.1, 0, 0.9, 0, 0, 0.1, 0, 0.8, 0, 0, 0, 0, 0.7]
            },
            TestContext.Current.CancellationToken);
        var morphology = await kernel.ApplyMorphologyAsync(
            new MorphologyRequest
            {
                Width = 3,
                Height = 3,
                Values = [0, 0, 0, 0, 1, 0, 0, 0, 0],
                Operation = "dilation",
                Radius = 1
            },
            TestContext.Current.CancellationToken);
        var flow = await kernel.EstimateDenseOpticalFlowAsync(
            new DenseOpticalFlowRequest
            {
                Width = 2,
                Height = 2,
                Previous = [0, 1, 0, 0],
                Current = [0, 0, 1, 0],
                SearchRadius = 1
            },
            TestContext.Current.CancellationToken);

        Assert.True(hsv.Succeeded);
        Assert.Equal([1, 0], hsv.Mask);
        Assert.True(pooled.Succeeded);
        Assert.Equal(4, pooled.Values.Count);
        Assert.True(morphology.Succeeded);
        Assert.All(morphology.Values, value => Assert.Equal(1, value));
        Assert.True(flow.Succeeded);
        Assert.Equal(4, flow.Vectors.Count);
    }

    /// <summary>
    /// [EN] Verifies resident temporal, audio, state, hash, and Kalman helpers are available as library algorithms.
    /// [JA] resident temporal / audio / state / hash / Kalman helper が library algorithm として利用できることを検証します。
    /// </summary>
    [Fact]
    public async Task ResidentStateAndAudioAlgorithms_ValidInputs_ReturnResults()
    {
        var kernel = new DefaultResidentPerceptionAlgorithmKernel();

        var temporal = await kernel.CalculateTemporalDifferenceAsync(
            new TemporalDifferenceRequest
            {
                Width = 2,
                Height = 1,
                Previous = [0, 0.5],
                Current = [1, 0.5],
                Threshold = 0.1
            },
            TestContext.Current.CancellationToken);
        var binaural = await kernel.QuantizeBinauralDirectionAsync(
            new BinauralDirectionQuantizationRequest
            {
                InterleavedSamples = [0.1f, 0.8f, 0.2f, 0.7f, 0.1f, 0.9f, 0.2f, 0.8f],
                Channels = 2,
                DirectionBins = 8
            },
            TestContext.Current.CancellationToken);
        var spectrum = await kernel.ComputeAudioSpectrumAsync(
            new AudioSpectrumRequest
            {
                Samples = [0, 1, 0, -1, 0, 1, 0, -1],
                Channels = 1,
                MaxFftSize = 8
            },
            TestContext.Current.CancellationToken);
        var ema = await kernel.ApplyEmaAsync(
            new EmaFilterRequest { Previous = [0, 1], Current = [1, 0], Alpha = 0.5 },
            TestContext.Current.CancellationToken);
        var leaky = await kernel.ApplyLeakyIntegratorAsync(
            new LeakyIntegratorRequest { Previous = [0.5], Inputs = [0.3], LeakRate = 0.2, MaxValue = 1 },
            TestContext.Current.CancellationToken);
        var hash = await kernel.QuantizeSpatialHashAsync(
            new SpatialHashRequest { X = [0.1, 1.2, 1.6], Y = [0.2, 0.8, 1.1], CellSize = 1 },
            TestContext.Current.CancellationToken);
        var kalman = await kernel.ApplyKalmanAsync(
            new KalmanFilterRequest
            {
                States = [new KalmanFilterState { Estimate = 0, ErrorCovariance = 1 }],
                Measurements = [1],
                Controls = [0],
                ProcessNoise = 0.01,
                MeasurementNoise = 0.1
            },
            TestContext.Current.CancellationToken);

        Assert.True(temporal.Succeeded);
        Assert.Equal(1, temporal.MaxDelta);
        Assert.True(binaural.Succeeded);
        Assert.True(binaural.Balance > 0);
        Assert.True(spectrum.Succeeded);
        Assert.NotEmpty(spectrum.Magnitudes);
        Assert.True(ema.Succeeded);
        Assert.Equal([0.5, 0.5], ema.Values);
        Assert.True(leaky.Succeeded);
        Assert.Single(leaky.Values);
        Assert.True(hash.Succeeded);
        Assert.Equal(3, hash.Cells.Count);
        Assert.True(kalman.Succeeded);
        Assert.Single(kalman.States);
    }

    private static SensorStateDescriptor Sensor(
        string name,
        bool enabled,
        bool observed,
        double confidence,
        IReadOnlyDictionary<string, string> metadata)
        => new()
        {
            Name = name,
            Category = name == "spatial" ? "derived" : "primary",
            Enabled = enabled,
            Observed = observed,
            Confidence = confidence,
            Metadata = metadata
        };
}
