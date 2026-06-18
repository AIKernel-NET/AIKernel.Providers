namespace AIKernel.Providers.Perception;

/// <summary>
/// [EN] Provides provider-neutral sensor fusion helpers for spatial cognition.
/// [JA] spatial cognition 用の provider-neutral な sensor fusion helper を提供します。
/// </summary>
public static class SpatialSensorFusionKernel
{
    private const double DefaultAuditoryCorrectionGain = 35;

    /// <summary>
    /// [EN] Fuses visual, auditory, motor, movement, and compass sensor states into a spatial vector.
    /// [JA] visual / auditory / motor / movement / compass sensor state を spatial vector に融合します。
    /// </summary>
    /// <param name="sensorInputs">[EN] Named sensor inputs. [JA] 名前付き sensor input です。</param>
    /// <returns>[EN] Provider-neutral spatial fusion vector. [JA] provider-neutral な spatial fusion vector を返します。</returns>
    public static SpatialFusionVector Fuse(IReadOnlyDictionary<string, SensorStateDescriptor> sensorInputs)
    {
        ArgumentNullException.ThrowIfNull(sensorInputs);

        var visual = Sensor(sensorInputs, "visual");
        var audio = Sensor(sensorInputs, "audio");
        var motor = Sensor(sensorInputs, "motor");
        var movement = Sensor(sensorInputs, "movement");
        var compass = Sensor(sensorInputs, "compass");
        var spatial = Sensor(sensorInputs, "spatial");

        if (spatial is { Enabled: false })
        {
            return new SpatialFusionVector
            {
                HudX = 0.5,
                HudY = 0.45
            };
        }

        var visualBias = visual?.Enabled == false ? 0 : ClampSigned(Number(visual, "bias"));
        var motorBias = motor?.Enabled == false ? 0 : ClampSigned(Number(motor, "vectorX") * 0.45);
        var movementBias = movement?.Enabled == false ? 0 : ClampSigned(Number(movement, "vectorX"));
        var compassBias = 0.0;
        if (compass?.Enabled != false)
        {
            var heading = Number(compass, "heading");
            var relative = (((heading + 540) % 360) - 180) / 180;
            compassBias = ClampSigned(relative) * Clamp01(Number(compass, "confidence"));
        }

        var auditoryBalance = audio?.Enabled == false ? 0 : ClampSigned(Number(audio, "balance"));
        var gain = Number(audio, "correctionGain");
        if (Math.Abs(gain) <= double.Epsilon)
        {
            gain = DefaultAuditoryCorrectionGain;
        }

        var auditoryCorrection = auditoryBalance * gain;
        var visualDirection = ClampSigned((visualBias * 0.55) + (movementBias * 0.2) + (motorBias * 0.15) + (compassBias * 0.1)) * 45;
        var fusedDirection = visualDirection + auditoryCorrection;
        var audioEnergy = audio?.Enabled == false
            ? 0
            : Math.Max(Clamp01(Number(audio, "leftEnergy")), Clamp01(Number(audio, "rightEnergy")));
        var visualConfidence = visual?.Enabled == false ? 0 : Clamp01(Number(visual, "confidence"));
        var compassConfidence = compass?.Enabled == false ? 0 : Clamp01(Number(compass, "confidence"));
        var confidence = Clamp01(Math.Max(visualConfidence, Math.Max(audioEnergy * 0.85, compassConfidence * 0.55)));
        var eventDetected = Boolean(audio, "eventDetected")
            || visualConfidence >= 0.62
            || (Math.Abs(fusedDirection) >= 18 && confidence >= 0.35);

        return new SpatialFusionVector
        {
            VisualDirection = Round2(visualDirection),
            AuditoryCorrection = Round2(auditoryCorrection),
            FusedDirection = Round2(fusedDirection),
            HudX = Round2(Clamp01(0.5 + (ClampSigned(fusedDirection / 90) * 0.4))),
            HudY = Round2(Clamp01(0.46 - (confidence * 0.16))),
            Confidence = Round2(confidence),
            EventDetected = eventDetected
        };
    }

    /// <summary>
    /// [EN] Builds a high-priority retry carrier from a health sensor state.
    /// [JA] health sensor state から高優先 retry carrier を構築します。
    /// </summary>
    /// <param name="sensorInputs">[EN] Named sensor inputs. [JA] 名前付き sensor input です。</param>
    /// <returns>[EN] Retry carrier when health requests retry; otherwise null. [JA] health が retry を要求する場合は retry carrier、それ以外は null を返します。</returns>
    public static RetryIntentCarrier? ResolveRetryIntent(IReadOnlyDictionary<string, SensorStateDescriptor> sensorInputs)
    {
        ArgumentNullException.ThrowIfNull(sensorInputs);

        var health = Sensor(sensorInputs, "health");
        if (health is null || !health.Enabled)
        {
            return null;
        }

        var zeroScore = Clamp01(Number(health, "zeroScore"));
        var likelyDead = Boolean(health, "likelyDead");
        if (!likelyDead && zeroScore < 0.78)
        {
            return null;
        }

        return new RetryIntentCarrier
        {
            Requested = true,
            ReasonCode = "health-death",
            Priority = 100,
            Confidence = Round2(Math.Max(zeroScore, Clamp01(health.Confidence ?? 0))),
            SourceSensor = "health",
            Metadata = CopyMetadata(health.Metadata)
        };
    }

    private static SensorStateDescriptor? Sensor(IReadOnlyDictionary<string, SensorStateDescriptor> sensors, string name)
        => sensors.TryGetValue(name, out var sensor) ? sensor : null;

    private static double Number(SensorStateDescriptor? sensor, string key)
        => sensor is not null
            && sensor.Metadata.TryGetValue(key, out var value)
            && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
                ? number
                : 0;

    private static bool Boolean(SensorStateDescriptor? sensor, string key)
        => sensor is not null
            && sensor.Metadata.TryGetValue(key, out var value)
            && bool.TryParse(value, out var parsed)
            && parsed;

    private static IReadOnlyDictionary<string, string> CopyMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in metadata.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            copy[item.Key] = item.Value;
        }

        return copy;
    }

    private static double Clamp01(double value)
        => Math.Clamp(value, 0, 1);

    private static double ClampSigned(double value)
        => Math.Clamp(value, -1, 1);

    private static double Round2(double value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// [EN] Normalizes sensor input maps with deterministic key ordering and default sensor states.
/// [JA] deterministic な key ordering と default sensor state により sensor input map を正規化します。
/// </summary>
public sealed class SensorStateNormalizer : ISensorStateNormalizer
{
    private static readonly string[] DefaultSensors =
    [
        "visual",
        "audio",
        "motor",
        "movement",
        "compass",
        "spatial",
        "health"
    ];

    /// <summary>
    /// [EN] Initializes a provider-neutral sensor state normalizer.
    /// [JA] provider-neutral な sensor state normalizer を初期化します。
    /// </summary>
    public SensorStateNormalizer()
    {
    }

    /// <summary>[EN] Documents this public package API member. [JA] NormalizeAsync を取得します。</summary>
    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, SensorStateDescriptor>> NormalizeAsync(
        IReadOnlyDictionary<string, SensorStateDescriptor> sensorInputs,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(sensorInputs);

        var normalized = new SortedDictionary<string, SensorStateDescriptor>(StringComparer.Ordinal);
        for (var index = 0; index < DefaultSensors.Length; index++)
        {
            var name = DefaultSensors[index];
            normalized[name] = CreateDefault(name);
        }

        foreach (var item in sensorInputs.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var name = NormalizeName(item.Key);
            normalized[name] = NormalizeState(name, item.Value);
        }

        return ValueTask.FromResult<IReadOnlyDictionary<string, SensorStateDescriptor>>(
            new Dictionary<string, SensorStateDescriptor>(normalized, StringComparer.Ordinal));
    }

    private static SensorStateDescriptor CreateDefault(string name)
        => new()
        {
            Name = name,
            ConceptName = ConceptName(name),
            EnglishName = EnglishName(name),
            Category = name is "movement" or "spatial" ? "derived" : "primary",
            Enabled = true,
            Observed = false
        };

    private static SensorStateDescriptor NormalizeState(string name, SensorStateDescriptor source)
        => source with
        {
            Name = name,
            ConceptName = string.IsNullOrWhiteSpace(source.ConceptName) ? ConceptName(name) : source.ConceptName,
            EnglishName = string.IsNullOrWhiteSpace(source.EnglishName) ? EnglishName(name) : source.EnglishName,
            Category = string.IsNullOrWhiteSpace(source.Category)
                ? (name is "movement" or "spatial" ? "derived" : "primary")
                : source.Category,
            Enabled = source.Enabled,
            Metadata = CopyMetadata(source.Metadata)
        };

    private static string NormalizeName(string name)
    {
        var normalized = StringComparer.OrdinalIgnoreCase.Equals(name, "vision")
            ? "visual"
            : StringComparer.OrdinalIgnoreCase.Equals(name, "auditory")
                ? "audio"
                : StringComparer.OrdinalIgnoreCase.Equals(name, "heading")
                    || StringComparer.OrdinalIgnoreCase.Equals(name, "bearing")
                        ? "compass"
                        : name;
        return normalized.Trim().ToLowerInvariant();
    }

    private static string ConceptName(string name)
        => name switch
        {
            "visual" or "audio" => "Aisthesis",
            "motor" or "movement" => "Kinesis",
            "compass" or "spatial" => "Phantasia",
            "health" => "Aisthesis",
            _ => string.Empty
        };

    private static string EnglishName(string name)
        => name switch
        {
            "visual" => "visual",
            "audio" => "audio",
            "motor" => "motor",
            "movement" => "movement",
            "compass" => "compass",
            "health" => "health",
            "spatial" => "spatial",
            _ => name
        };

    private static IReadOnlyDictionary<string, string> CopyMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in metadata.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            copy[item.Key] = item.Value;
        }

        return copy;
    }
}

/// <summary>
/// [EN] Implements provider-neutral spatial sensor fusion behind an extractable interface.
/// [JA] 抽出可能な interface の背後で provider-neutral な spatial sensor fusion を実装します。
/// </summary>
public sealed class DefaultSpatialSensorFusionKernel : ISpatialSensorFusionKernel
{
    /// <summary>
    /// [EN] Initializes a provider-neutral spatial sensor fusion kernel.
    /// [JA] provider-neutral な spatial sensor fusion kernel を初期化します。
    /// </summary>
    public DefaultSpatialSensorFusionKernel()
    {
    }

    /// <summary>[EN] Documents this public package API member. [JA] FuseAsync を取得します。</summary>
    /// <inheritdoc />
    public ValueTask<SpatialFusionVector> FuseAsync(
        IReadOnlyDictionary<string, SensorStateDescriptor> sensorInputs,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(SpatialSensorFusionKernel.Fuse(sensorInputs));
    }
}

/// <summary>
/// [EN] Resolves retry intent from provider-neutral health sensor inputs.
/// [JA] provider-neutral な health sensor input から retry intent を解決します。
/// </summary>
public sealed class HealthRetryIntentResolver : IRetryIntentResolver
{
    /// <summary>
    /// [EN] Initializes a provider-neutral health retry intent resolver.
    /// [JA] provider-neutral な health retry intent resolver を初期化します。
    /// </summary>
    public HealthRetryIntentResolver()
    {
    }

    /// <summary>[EN] Documents this public package API member. [JA] ResolveAsync を取得します。</summary>
    /// <inheritdoc />
    public ValueTask<RetryIntentCarrier?> ResolveAsync(
        IReadOnlyDictionary<string, SensorStateDescriptor> sensorInputs,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(SpatialSensorFusionKernel.ResolveRetryIntent(sensorInputs));
    }
}
