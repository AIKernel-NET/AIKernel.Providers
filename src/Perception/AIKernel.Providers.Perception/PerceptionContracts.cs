namespace AIKernel.Providers.Perception;

/// <summary>
/// [EN] Performs provider-neutral frame perception without owning browser or scenario-specific runtime code.
/// [JA] browser や scenario 固有 runtime code を所有せず provider-neutral な frame perception を実行します。
/// </summary>
public interface IFramePerceptionProvider
{
    /// <summary>
    /// [EN] Analyzes a frame perception request.
    /// [JA] frame perception request を解析します。
    /// </summary>
    /// <param name="request">[EN] Frame perception request. [JA] frame perception request です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視するトークンです。</param>
    /// <returns>[EN] Frame perception result. [JA] frame perception result を返します。</returns>
    ValueTask<FramePerceptionProviderResult> AnalyzeFrameAsync(
        FramePerceptionProviderRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// [EN] Performs provider-neutral auditory perception over PCM-like descriptors.
/// [JA] PCM 風 descriptor に対する provider-neutral な auditory perception を実行します。
/// </summary>
public interface IAuditoryPerceptionProvider
{
    /// <summary>
    /// [EN] Analyzes an auditory perception request.
    /// [JA] auditory perception request を解析します。
    /// </summary>
    /// <param name="request">[EN] Auditory perception request. [JA] auditory perception request です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視するトークンです。</param>
    /// <returns>[EN] Auditory perception result. [JA] auditory perception result を返します。</returns>
    ValueTask<AuditoryPerceptionResult> AnalyzeAudioAsync(
        AuditoryPerceptionRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// [EN] Composes perception outputs into provider-neutral spatial cognition snapshots.
/// [JA] perception output を provider-neutral な spatial cognition snapshot に合成します。
/// </summary>
public interface ISpatialCognitionProvider
{
    /// <summary>
    /// [EN] Builds a spatial cognition snapshot.
    /// [JA] spatial cognition snapshot を構築します。
    /// </summary>
    /// <param name="request">[EN] Spatial cognition request. [JA] spatial cognition request です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視するトークンです。</param>
    /// <returns>[EN] Spatial cognition snapshot. [JA] spatial cognition snapshot を返します。</returns>
    ValueTask<SpatialCognitionSnapshot> BuildSnapshotAsync(
        SpatialCognitionRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// [EN] Normalizes arbitrary sensor inputs into a deterministic sensor map.
/// [JA] 任意の sensor input を deterministic な sensor map に正規化します。
/// </summary>
public interface ISensorStateNormalizer
{
    /// <summary>
    /// [EN] Normalizes named sensor inputs for downstream cognition.
    /// [JA] downstream cognition 用に名前付き sensor input を正規化します。
    /// </summary>
    /// <param name="sensorInputs">[EN] Raw named sensor inputs. [JA] raw な名前付き sensor input です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視するトークンです。</param>
    /// <returns>[EN] Normalized sensor map. [JA] 正規化済み sensor map を返します。</returns>
    ValueTask<IReadOnlyDictionary<string, SensorStateDescriptor>> NormalizeAsync(
        IReadOnlyDictionary<string, SensorStateDescriptor> sensorInputs,
        CancellationToken cancellationToken);
}

/// <summary>
/// [EN] Fuses normalized sensor inputs into provider-neutral spatial cognition carriers.
/// [JA] 正規化済み sensor input を provider-neutral な spatial cognition carrier に融合します。
/// </summary>
public interface ISpatialSensorFusionKernel
{
    /// <summary>
    /// [EN] Fuses named sensor inputs into a spatial vector.
    /// [JA] 名前付き sensor input を spatial vector に融合します。
    /// </summary>
    /// <param name="sensorInputs">[EN] Named sensor inputs. [JA] 名前付き sensor input です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視するトークンです。</param>
    /// <returns>[EN] Fused spatial vector. [JA] 融合済み spatial vector を返します。</returns>
    ValueTask<SpatialFusionVector> FuseAsync(
        IReadOnlyDictionary<string, SensorStateDescriptor> sensorInputs,
        CancellationToken cancellationToken);
}

/// <summary>
/// [EN] Resolves high-priority retry intent from normalized sensor inputs.
/// [JA] 正規化済み sensor input から高優先 retry intent を解決します。
/// </summary>
public interface IRetryIntentResolver
{
    /// <summary>
    /// [EN] Resolves retry intent without invoking Control or Gate logic.
    /// [JA] Control / Gate logic を呼び出さず retry intent を解決します。
    /// </summary>
    /// <param name="sensorInputs">[EN] Named sensor inputs. [JA] 名前付き sensor input です。</param>
    /// <param name="cancellationToken">[EN] Cancellation token. [JA] キャンセル通知を監視するトークンです。</param>
    /// <returns>[EN] Retry carrier when requested; otherwise null. [JA] retry 要求時は retry carrier、それ以外は null を返します。</returns>
    ValueTask<RetryIntentCarrier?> ResolveAsync(
        IReadOnlyDictionary<string, SensorStateDescriptor> sensorInputs,
        CancellationToken cancellationToken);
}

/// <summary>
/// [EN] Carries a provider-neutral frame perception request.
/// [JA] provider-neutral な frame perception request を保持します。
/// </summary>
public sealed record FramePerceptionProviderRequest
{
    /// <summary>[EN] Gets the frame identifier. [JA] frame 識別子を取得します。</summary>
    public string FrameId { get; init; } = string.Empty;

    /// <summary>[EN] Gets the surface identifier. [JA] surface 識別子を取得します。</summary>
    public string? SurfaceId { get; init; }

    /// <summary>[EN] Gets normalized metadata about the frame buffer. [JA] frame buffer に関する正規化済み metadata を取得します。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// [EN] Carries provider-neutral frame perception output.
/// [JA] provider-neutral な frame perception output を保持します。
/// </summary>
public sealed record FramePerceptionProviderResult
{
    /// <summary>[EN] Gets whether perception succeeded. [JA] perception が成功したかを取得します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Gets the observation identifier. [JA] observation 識別子を取得します。</summary>
    public string ObservationId { get; init; } = string.Empty;

    /// <summary>[EN] Gets extracted provider-neutral signals. [JA] 抽出された provider-neutral signal を取得します。</summary>
    public IReadOnlyList<PerceptionSignalDescriptor> Signals { get; init; } = [];

    /// <summary>[EN] Gets stable failure code when perception failed. [JA] perception が失敗した場合の stable failure code を取得します。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Gets human-readable failure message when perception failed. [JA] perception が失敗した場合の人間可読 message を取得します。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>[EN] Gets deterministic perception diagnostics. [JA] deterministic perception diagnostics を取得します。</summary>
    public IReadOnlyList<PerceptionDiagnosticDescriptor> Diagnostics { get; init; } = [];

    /// <summary>[EN] Gets deterministic perception metadata. [JA] deterministic perception metadata を取得します。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// [EN] Carries provider-neutral auditory perception input.
/// [JA] provider-neutral な auditory perception input を保持します。
/// </summary>
public sealed record AuditoryPerceptionRequest
{
    /// <summary>[EN] Gets the audio frame identifier. [JA] audio frame 識別子を取得します。</summary>
    public string AudioFrameId { get; init; } = string.Empty;

    /// <summary>[EN] Gets the sample rate in hertz. [JA] hertz 単位の sample rate を取得します。</summary>
    public int SampleRate { get; init; }

    /// <summary>[EN] Gets the channel count. [JA] channel count を取得します。</summary>
    public int Channels { get; init; }

    /// <summary>[EN] Gets the sample format identifier. [JA] sample format 識別子を取得します。</summary>
    public string SampleFormat { get; init; } = string.Empty;

    /// <summary>[EN] Gets the payload byte length. [JA] payload byte length を取得します。</summary>
    public int ByteLength { get; init; }

    /// <summary>[EN] Gets deterministic request metadata. [JA] deterministic request metadata を取得します。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// [EN] Carries provider-neutral auditory perception output.
/// [JA] provider-neutral な auditory perception output を保持します。
/// </summary>
public sealed record AuditoryPerceptionResult
{
    /// <summary>[EN] Gets whether perception succeeded. [JA] perception が成功したかを取得します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Gets the observation identifier. [JA] observation 識別子を取得します。</summary>
    public string ObservationId { get; init; } = string.Empty;

    /// <summary>[EN] Gets extracted provider-neutral signals. [JA] 抽出された provider-neutral signal を取得します。</summary>
    public IReadOnlyList<PerceptionSignalDescriptor> Signals { get; init; } = [];

    /// <summary>[EN] Gets stable failure code when perception failed. [JA] perception が失敗した場合の stable failure code を取得します。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Gets human-readable failure message when perception failed. [JA] perception が失敗した場合の人間可読 message を取得します。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>[EN] Gets deterministic perception diagnostics. [JA] deterministic perception diagnostics を取得します。</summary>
    public IReadOnlyList<PerceptionDiagnosticDescriptor> Diagnostics { get; init; } = [];

    /// <summary>[EN] Gets deterministic perception metadata. [JA] deterministic perception metadata を取得します。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// [EN] Carries provider-neutral spatial cognition composition input.
/// [JA] provider-neutral な spatial cognition composition input を保持します。
/// </summary>
public sealed record SpatialCognitionRequest
{
    /// <summary>[EN] Gets the request identifier. [JA] request 識別子を取得します。</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>[EN] Gets visual perception signals. [JA] visual perception signal を取得します。</summary>
    public IReadOnlyList<PerceptionSignalDescriptor> VisualSignals { get; init; } = [];

    /// <summary>[EN] Gets auditory perception signals. [JA] auditory perception signal を取得します。</summary>
    public IReadOnlyList<PerceptionSignalDescriptor> AuditorySignals { get; init; } = [];

    /// <summary>[EN] Gets named sensor inputs for extensible cognition fusion. [JA] 拡張可能な cognition fusion 用の名前付き sensor input を取得します。</summary>
    public IReadOnlyDictionary<string, SensorStateDescriptor> SensorInputs { get; init; } =
        new Dictionary<string, SensorStateDescriptor>(StringComparer.Ordinal);

    /// <summary>[EN] Gets deterministic request metadata. [JA] deterministic request metadata を取得します。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// [EN] Carries a provider-neutral spatial cognition snapshot.
/// [JA] provider-neutral な spatial cognition snapshot を保持します。
/// </summary>
public sealed record SpatialCognitionSnapshot
{
    /// <summary>[EN] Gets the snapshot identifier. [JA] snapshot 識別子を取得します。</summary>
    public string SnapshotId { get; init; } = string.Empty;

    /// <summary>[EN] Gets whether snapshot composition succeeded. [JA] snapshot composition が成功したかを取得します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Gets composed spatial signals. [JA] 合成された spatial signal を取得します。</summary>
    public IReadOnlyList<PerceptionSignalDescriptor> Signals { get; init; } = [];

    /// <summary>[EN] Gets normalized sensor states used by cognition fusion. [JA] cognition fusion で使用した正規化済み sensor state を取得します。</summary>
    public IReadOnlyDictionary<string, SensorStateDescriptor> SensorInputs { get; init; } =
        new Dictionary<string, SensorStateDescriptor>(StringComparer.Ordinal);

    /// <summary>[EN] Gets the optional retry carrier produced by high-priority safety sensors. [JA] 高優先 safety sensor が生成した任意の retry carrier を取得します。</summary>
    public RetryIntentCarrier? RetryIntent { get; init; }

    /// <summary>[EN] Gets stable failure code when composition failed. [JA] composition が失敗した場合の stable failure code を取得します。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Gets human-readable failure message when composition failed. [JA] composition が失敗した場合の人間可読 message を取得します。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>[EN] Gets deterministic cognition diagnostics. [JA] deterministic cognition diagnostics を取得します。</summary>
    public IReadOnlyList<PerceptionDiagnosticDescriptor> Diagnostics { get; init; } = [];

    /// <summary>[EN] Gets deterministic cognition metadata. [JA] deterministic cognition metadata を取得します。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// [EN] Describes one provider-neutral sensor input state.
/// [JA] 1 つの provider-neutral な sensor input state を記述します。
/// </summary>
public sealed record SensorStateDescriptor
{
    /// <summary>[EN] Gets the stable sensor name. [JA] 安定した sensor 名を取得します。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>[EN] Gets the philosophical concept name for canonical elevation. [JA] 正典昇格用の哲学的 concept 名を取得します。</summary>
    public string ConceptName { get; init; } = string.Empty;

    /// <summary>[EN] Gets the English sensor name associated with the concept. [JA] concept に対応する英語 sensor 名を取得します。</summary>
    public string EnglishName { get; init; } = string.Empty;

    /// <summary>[EN] Gets the sensor category such as primary or derived. [JA] primary / derived などの sensor category を取得します。</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>[EN] Gets whether this sensor is enabled. [JA] この sensor が有効かどうかを取得します。</summary>
    public bool Enabled { get; init; }

    /// <summary>[EN] Gets whether this sensor is directly observed. [JA] この sensor が直接観測されたものかどうかを取得します。</summary>
    public bool Observed { get; init; }

    /// <summary>[EN] Gets normalized sensor confidence outside CTG GateInput. [JA] CTG GateInput の外側に保持する正規化済み sensor confidence を取得します。</summary>
    public double? Confidence { get; init; }

    /// <summary>[EN] Gets sensor timestamp in ISO-8601 form. [JA] ISO-8601 形式の sensor timestamp を取得します。</summary>
    public string? Timestamp { get; init; }

    /// <summary>[EN] Gets deterministic sensor metadata. [JA] deterministic sensor metadata を取得します。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// [EN] Carries low-level spatial fusion values without scenario semantics.
/// [JA] scenario semantics を含まない low-level spatial fusion value を保持します。
/// </summary>
public readonly record struct SpatialFusionVector
{
    /// <summary>[EN] Gets visual direction in normalized degrees. [JA] normalized degree 単位の visual direction を取得します。</summary>
    public double VisualDirection { get; init; }

    /// <summary>[EN] Gets auditory correction in normalized degrees. [JA] normalized degree 単位の auditory correction を取得します。</summary>
    public double AuditoryCorrection { get; init; }

    /// <summary>[EN] Gets fused direction in normalized degrees. [JA] normalized degree 単位の fused direction を取得します。</summary>
    public double FusedDirection { get; init; }

    /// <summary>[EN] Gets normalized HUD X coordinate. [JA] normalized HUD X coordinate を取得します。</summary>
    public double HudX { get; init; }

    /// <summary>[EN] Gets normalized HUD Y coordinate. [JA] normalized HUD Y coordinate を取得します。</summary>
    public double HudY { get; init; }

    /// <summary>[EN] Gets normalized fusion confidence. [JA] 正規化済み fusion confidence を取得します。</summary>
    public double Confidence { get; init; }

    /// <summary>[EN] Gets whether a spatial event was detected. [JA] spatial event が検知されたかどうかを取得します。</summary>
    public bool EventDetected { get; init; }
}

/// <summary>
/// [EN] Carries a platform-neutral high-priority retry intent.
/// [JA] platform-neutral な高優先 retry intent を保持します。
/// </summary>
public sealed record RetryIntentCarrier
{
    /// <summary>[EN] Gets whether retry is requested. [JA] retry が要求されているかどうかを取得します。</summary>
    public bool Requested { get; init; }

    /// <summary>[EN] Gets the retry reason code. [JA] retry reason code を取得します。</summary>
    public string ReasonCode { get; init; } = string.Empty;

    /// <summary>[EN] Gets deterministic retry priority where larger values win. [JA] 値が大きいほど優先される deterministic retry priority を取得します。</summary>
    public int Priority { get; init; }

    /// <summary>[EN] Gets normalized retry confidence outside CTG GateInput. [JA] CTG GateInput の外側に保持する正規化済み retry confidence を取得します。</summary>
    public double Confidence { get; init; }

    /// <summary>[EN] Gets the source sensor name. [JA] source sensor 名を取得します。</summary>
    public string SourceSensor { get; init; } = string.Empty;

    /// <summary>[EN] Gets deterministic retry metadata. [JA] deterministic retry metadata を取得します。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// [EN] Describes one provider-neutral perception signal.
/// [JA] 1 つの provider-neutral な perception signal を記述します。
/// </summary>
public sealed record PerceptionSignalDescriptor
{
    /// <summary>[EN] Gets the signal identifier. [JA] signal 識別子を取得します。</summary>
    public string SignalId { get; init; } = string.Empty;

    /// <summary>[EN] Gets the signal kind. [JA] signal kind を取得します。</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>[EN] Gets provider-neutral normalized value. [JA] provider-neutral な normalized value を取得します。</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>[EN] Gets optional signal confidence retained as adapter-facing semantic material. [JA] adapter 向け semantic material として保持する任意の signal confidence を取得します。</summary>
    public double? Confidence { get; init; }

    /// <summary>[EN] Gets deterministic signal metadata. [JA] deterministic signal metadata を取得します。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// [EN] Describes one provider-neutral perception diagnostic.
/// [JA] 1 つの provider-neutral な perception diagnostic を記述します。
/// </summary>
public sealed record PerceptionDiagnosticDescriptor
{
    /// <summary>[EN] Gets the diagnostic code. [JA] diagnostic code を取得します。</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>[EN] Gets the diagnostic message. [JA] diagnostic message を取得します。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>[EN] Gets diagnostic metadata. [JA] diagnostic metadata を取得します。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
