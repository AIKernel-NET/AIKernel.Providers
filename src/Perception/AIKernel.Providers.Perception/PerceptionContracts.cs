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
