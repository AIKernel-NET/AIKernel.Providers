namespace AIKernel.Providers.Tts;

/// <summary>Host-owned connection and defaults for an installed TTS runtime.</summary>
public sealed record TtsSettings
{
    /// <summary>Runtime identity expected in health and synthesis responses.</summary>
    public string Engine { get; init; } = "kokoro-82m";

    /// <summary>Base address with a trailing slash. Use HTTPS or loopback HTTP.</summary>
    public Uri? Endpoint { get; init; }

    /// <summary>Language sent when the caller omits the language argument.</summary>
    public string Language { get; init; } = "en-US";

    /// <summary>Voice sent when the caller omits the voice argument.</summary>
    public string? Voice { get; init; }

    /// <summary>Deadline covering each request, including the response body.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}
