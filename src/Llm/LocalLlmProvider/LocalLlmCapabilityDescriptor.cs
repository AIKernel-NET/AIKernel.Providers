namespace AIKernel.Providers.LocalLlm;

/// <summary>
/// [EN] Public capability descriptor for the local LLM provider.
/// [JA] Local LLM Provider の公開 capability descriptor です。
/// </summary>
public sealed record LocalLlmCapabilityDescriptor(
    string CapabilityId,
    string Runtime,
    IReadOnlyDictionary<string, string> Metadata);
