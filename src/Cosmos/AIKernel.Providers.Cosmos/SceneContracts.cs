namespace AIKernel.Providers.Cosmos;

public sealed class SceneOptions
{
    public bool Enabled { get; init; }
    public required string JournalDirectory { get; init; }
    public int MaxRequestBytes { get; init; } = 64 * 1024;
    public int MaxResponseBytes { get; init; } = 64 * 1024;
    public int MaxImageBytes { get; init; } = 4 * 1024 * 1024;
    public long MaxStoredMediaBytes { get; init; } = 64 * 1024 * 1024;
    public int MaxImagePixels { get; init; } = 1920 * 1080;
    public int MaxFrames { get; init; } = 8;
    public int MaxActors { get; init; } = 64;
    public int MaxProposals { get; init; } = 16;
    public int MaxJobs { get; init; } = 1000;
    public int MaxRunningPerCaller { get; init; } = 1;
    public int MaxPendingPerCaller { get; init; } = 1;
    public int MaxConcurrentInference { get; init; } = 1;
    public int MaxConcurrentUploads { get; init; } = 2;
    public int MaxObservationAgeMs { get; init; } = 60_000;
    public TimeSpan MediaRetention { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan IdempotencyRetention { get; init; } = TimeSpan.FromHours(24);
    public TimeSpan ReadinessMaxAge { get; init; } = TimeSpan.FromMinutes(1);
}

public sealed class SceneException(string code, bool workMayStillBeRunning = false) : Exception(code)
{
    public string Code { get; } = code;
    public bool WorkMayStillBeRunning { get; } = workMayStillBeRunning;
}
