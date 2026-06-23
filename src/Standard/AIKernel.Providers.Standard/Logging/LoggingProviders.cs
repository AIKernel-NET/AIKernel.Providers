namespace AIKernel.Providers.Standard.Logging;

using AIKernel.Abstractions.Logging;
using AIKernel.Common.Results;
using AIKernel.Providers.Standard;
using LogLevel = AIKernel.Abstractions.Logging.LogLevel;

/// <summary>
/// [EN] Base class for OS logging providers.
/// [JA] OS logging Provider の base class です。
/// </summary>
public abstract class LoggingProviderBase : StandardProviderBase, ILoggingProvider
{
    /// <summary>[EN] Initializes a logging provider. [JA] logging Provider を初期化します。</summary>
    protected LoggingProviderBase(string providerId, string name)
        : base(providerId, name, "0.1.3", ["log.write", "log.flush"], ["log", "text"])
    {
    }

    /// <summary>[EN] Writes one log record. [JA] 1 件の log record を書き込みます。</summary>
    public abstract void Write(string level, string message);

    /// <summary>[EN] Writes one typed log record. [JA] 1 件の typed log record を書き込みます。</summary>
    public void Log(LogLevel level, string message, Exception? ex = null)
        => Write(
            level.ToString(),
            MonadicDecision.Optional(ex)
                .Match(() => message, value => $"{message} {value}"));

    /// <summary>[EN] Normalizes a log level for deterministic output. [JA] 決定論的 output 向けに log level を正規化します。</summary>
    protected static string NormalizeLevel(string level)
        => MonadicDecision.TextOrDefault(level, "INFO", text => text.Trim().ToUpperInvariant());
}

/// <summary>
/// [EN] Console logging provider for AIKernel OS logs.
/// [JA] AIKernel OS log 向けの console logging Provider です。
/// </summary>
public sealed class ConsoleLoggingProvider : LoggingProviderBase
{
    /// <summary>[EN] Initializes the console logging provider. [JA] console logging Provider を初期化します。</summary>
    public ConsoleLoggingProvider()
        : base("providers.logging.console", "Console Logging Provider")
    {
    }

    /// <summary>[EN] Documents this public package API member. [JA] Write を実行します。</summary>
    /// <inheritdoc />
    public override void Write(string level, string message)
        => Console.WriteLine($"[{NormalizeLevel(level)}] {message}");
}

/// <summary>
/// [EN] File logging provider for durable AIKernel OS logs.
/// [JA] durable な AIKernel OS log 向けの file logging Provider です。
/// </summary>
public sealed class FileLoggingProvider : LoggingProviderBase
{
    private readonly string _path;

    /// <summary>[EN] Initializes the file logging provider. [JA] file logging Provider を初期化します。</summary>
    public FileLoggingProvider(string path)
        : base("providers.logging.file", "File Logging Provider")
    {
        _path = ValidatePath(path).Match(
            error => throw new ArgumentException(error.Message, nameof(path)),
            value => value);
    }

    /// <summary>[EN] Documents this public package API member. [JA] Write を実行します。</summary>
    /// <inheritdoc />
    public override void Write(string level, string message)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(_path, $"[{DateTimeOffset.UtcNow:O}] [{NormalizeLevel(level)}] {message}{Environment.NewLine}");
    }

    private static Result<string> ValidatePath(string path)
        => string.IsNullOrWhiteSpace(path)
            ? Result<string>.Fail("Log file path is required. ErrorCode=LOG_PATH_REQUIRED")
            : Result<string>.Success(path);
}
