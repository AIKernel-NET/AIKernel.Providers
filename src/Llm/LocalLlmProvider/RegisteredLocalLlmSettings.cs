namespace AIKernel.Providers.LocalLlm;

/// <summary>Host-owned profile and optional bounded RAM cache for a registered local checkpoint.</summary>
public sealed record RegisteredLocalLlmSettings : LocalLlmSettings
{
    /// <summary>Explicit external registration. Omit to use the versioned built-in profile for the checkpoint format.</summary>
    public string? RegistrationFile { get; init; }
    /// <summary>Limits for the Kernel virtual-memory weight store. Omit to memory-map the weight backing.</summary>
    public AIKernel.Kernel.Inference.GgufMemoryOptions? WeightMemory { get; init; }
}
