namespace AIKernel.Providers.ChatOpenAI;

/// <summary>
/// [EN] Public capability descriptor for the ChatOpenAI external provider.
/// [JA] ChatOpenAI 外部 Provider の公開 capability descriptor です。
/// </summary>
public sealed record ChatOpenAICapabilityDescriptor(
    string CapabilityId,
    string Model,
    IReadOnlyDictionary<string, string> Metadata);
