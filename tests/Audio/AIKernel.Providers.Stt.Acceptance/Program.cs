using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Providers;
using AIKernel.Dtos.Providers.SpeechRecognition;
using AIKernel.Providers.Stt;
using Microsoft.Extensions.Configuration;

if (args.Length < 3 || args.Length > 4 || args.Length == 4 && args[3] != "--quick")
    throw new ArgumentException("Provide the model path, fixture directory and output JSON. Use --quick to omit the 10-minute workload.");
bool quick = args.Length == 4;

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
string fixtures = Path.GetFullPath(args[1]);
var manifest = JsonSerializer.Deserialize<FixtureManifest>(File.ReadAllText(Path.Combine(fixtures, "cases.json")), json)!;
var defaults = new LocalSpeechRecognitionSettings();
Require(defaults.MaxAudioDuration == TimeSpan.FromMinutes(10) && defaults.MaxRequestBytes == 128L * 1024 * 1024 &&
    defaults.CpuThreads == Math.Min(4, Environment.ProcessorCount), "Default settings changed.");

using var configFile = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new
{
    SpeechRecognition = new
    {
        ModelPath = Path.GetFullPath(args[0]),
        MaxAudioDuration = "00:10:00",
        MaxRequestBytes = 134217728,
        CpuThreads = 4
    }
}));
var configuration = new ConfigurationBuilder().AddJsonStream(configFile).Build();
var settings = configuration.GetRequiredSection("SpeechRecognition").Get<LocalSpeechRecognitionSettings>()!;
Require(settings.CpuThreads == 4 && settings.MaxAudioDuration == TimeSpan.FromMinutes(10), "Settings did not bind.");
await using var unregistered = new LocalSpeechRecognitionProvider(new()
{
    MaxAudioDuration = TimeSpan.FromSeconds(3),
    MaxRequestBytes = 8_192,
    CpuThreads = 1
});
await unregistered.InitializeAsync();
var absent = unregistered.GetCapabilityDescriptor();
Require(absent.ModelIds.Count == 0 && absent.Limits.MaxRequestBytes == 8_192 &&
    ((SpeechRecognitionCapabilityExtension)absent.Extension!).MaxAudioDuration == TimeSpan.FromSeconds(3),
    "Configured limits were not reflected in the descriptor.");
await ExpectError(unregistered, Request(new AudioSource.Bytes(ReadOnlyMemory<byte>.Empty, "audio/wav")), ProviderErrorCodes.ModelNotRegistered);

using var process = Process.GetCurrentProcess();
await using var local = new LocalSpeechRecognitionProvider(settings);
ISpeechRecognitionProvider provider = local;
var loadTime = Stopwatch.StartNew();
await provider.InitializeAsync();
loadTime.Stop();
long initializationPeak = Peak();
var descriptor = provider.GetCapabilityDescriptor();
var extension = (SpeechRecognitionCapabilityExtension)descriptor.Extension!;
Require(await provider.IsAvailableAsync() && descriptor.ProviderId == "asr.local" &&
    descriptor.ModelIds.Count == 1 && descriptor.Kind == ProviderKind.SpeechRecognition &&
    descriptor.Modes == ExecutionMode.Single && descriptor.ExecutionTargets.SequenceEqual(new[] { ExecutionTarget.Cpu }) &&
    descriptor.Limits.MaxRequestBytes == settings.MaxRequestBytes && extension.MaxAudioDuration == settings.MaxAudioDuration &&
    extension.Languages.SequenceEqual(new[] { "ja", "en" }) && extension.SupportsLanguageDetection &&
    extension.TimestampGranularities.Contains(TimestampGranularity.Segment), "Unexpected descriptor.");

var results = new List<ScoredFixture>();
foreach (var test in manifest.Cases)
{
    bool auto = !results.Any(result => result.Language == test.Language);
    string path = Path.Combine(fixtures, test.File);
    byte[] bytes = await File.ReadAllBytesAsync(path);
    await using var input = new ForwardOnlyStream(bytes);
    AudioSource source = auto ? new AudioSource.FromStream(input, "audio/wav") : new AudioSource.Bytes(bytes, "audio/wav");
    var timer = Stopwatch.StartNew();
    var result = await provider.TranscribeAsync(Request(source, auto ? null : test.Language));
    timer.Stop();
    Require(result.Language == test.Language && result.Duration.Ticks == test.DurationTicks && result.Segments.Count > 0 &&
        result.ProviderId == provider.ProviderId && result.ModelId == descriptor.ModelIds[0], $"Unexpected recognition result for {test.File}.");
    Require(result.Text == string.Join(test.Language == "ja" ? string.Empty : " ", result.Segments.Select(item => item.Text)),
        "Full text does not match the returned segments.");
    Require(!input.Disposed, "The provider closed the caller's stream.");
    CheckSegments(result);
    var referenceTokens = Normalize(test.Reference, test.Language);
    var actualTokens = Normalize(result.Text, test.Language);
    Require(referenceTokens.Length > 0 && actualTokens.Length > 0, "Missing scored tokens.");
    results.Add(new(test.Language, test.File, test.SentenceId, auto, test.Reference, result.Text, result.Duration.TotalSeconds,
        timer.Elapsed.TotalSeconds, Distance(referenceTokens, actualTokens), referenceTokens.Length, result.Segments));
    Console.Error.WriteLine($"{test.Language} {test.SentenceId} {timer.Elapsed.TotalSeconds:F2}s over {result.Duration.TotalSeconds:F2}s");
}

var rate = results.GroupBy(result => result.Language).ToDictionary(group => group.Key, group =>
{
    var errors = group.Sum(fixture => fixture.EditDistance);
    var units = group.Sum(fixture => fixture.ReferenceUnits);
    var seconds = group.Sum(fixture => fixture.ProcessingSeconds);
    var audioSeconds = group.Sum(fixture => fixture.AudioSeconds);
    return new { samples = group.Count(), errors, units, rate = errors / (double)units, seconds, audioSeconds, realTimeFactor = seconds / audioSeconds };
});
long shortCasePeak = Peak();

var silent = new AudioSource.Bytes(CreateSilence(16_000), "audio/wav");
var silence = await provider.TranscribeAsync(Request(silent, "ja"));
Require(silence is { Language: "ja", Text.Length: 0, Segments.Count: 0 } && silence.Duration == TimeSpan.FromSeconds(1),
    "Silence did not return empty text and segments.");
var autoSilence = await provider.TranscribeAsync(Request(silent));
Require(autoSilence is { Language: null, Text.Length: 0, Segments.Count: 0 }, "Silence received an invented language or text.");
var request = Request(new AudioSource.Bytes(await File.ReadAllBytesAsync(Path.Combine(fixtures, manifest.Cases[0].File)), "audio/wav"), "ja");
var textOnly = await provider.TranscribeAsync(request with { Timestamps = TimestampGranularity.None });
Require(textOnly.Text == results[0].Text && textOnly.Segments.Count == 0, "The full-text mode did not return the expected text.");

await using var oversize = new DeclaredLengthStream(settings.MaxRequestBytes + 1);
await ExpectError(provider, Request(new AudioSource.FromStream(oversize, "audio/wav")), ProviderErrorCodes.InputLimitExceeded);
await ExpectError(provider, Request(new AudioSource.Bytes(CreateSilence(600 * 16_000 + 1), "audio/wav")), ProviderErrorCodes.InputLimitExceeded);
await ExpectError(provider, Request(new AudioSource.Bytes(CreateSilence(16_000)[..60], "audio/wav")), SpeechRecognitionErrorCodes.DecodeFailed);
await using var truncated = new ForwardOnlyStream(CreateSilence(16_000)[..60]);
await ExpectError(provider, Request(new AudioSource.FromStream(truncated, "audio/wav")), SpeechRecognitionErrorCodes.DecodeFailed);
await ExpectError(provider, Request(new AudioSource.Bytes(ReadOnlyMemory<byte>.Empty, "audio/flac")), ProviderErrorCodes.UnsupportedFormat);
var floatFormat = CreateSilence(1);
BinaryPrimitives.WriteUInt16LittleEndian(floatFormat.AsSpan(20, 2), 3);
await ExpectError(provider, Request(new AudioSource.Bytes(floatFormat, "audio/wav")), ProviderErrorCodes.UnsupportedFormat);
await ExpectError(provider, request with { Language = "fr" }, SpeechRecognitionErrorCodes.UnsupportedLanguage);
await ExpectError(provider, request with { ModelId = "unregistered" }, ProviderErrorCodes.ModelNotRegistered);
await ExpectError(provider, request with { Timestamps = TimestampGranularity.Word }, ProviderErrorCodes.UnsupportedOption);

using (var canceled = new CancellationTokenSource())
{
    canceled.Cancel();
    await ExpectCanceled(provider.TranscribeAsync(request, canceled.Token), canceled.Token);
}

using var inFlight = new CancellationTokenSource();
var cancelAudio = await File.ReadAllBytesAsync(Path.Combine(fixtures, "cancel-en.wav"));
await using var cancelInput = new ForwardOnlyStream(cancelAudio);
var pending = provider.TranscribeAsync(Request(new AudioSource.FromStream(cancelInput, "audio/wav"), "en"), inFlight.Token);
await cancelInput.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
var beforeWork = process.TotalProcessorTime;
var nativeStart = Stopwatch.StartNew();
while (!pending.IsCompleted && process.TotalProcessorTime - beforeWork < TimeSpan.FromMilliseconds(100) &&
       nativeStart.Elapsed < TimeSpan.FromSeconds(5))
    await Task.Delay(25);
double cpuMsBeforeCancel = (process.TotalProcessorTime - beforeWork).TotalMilliseconds;
Require(!pending.IsCompleted && cpuMsBeforeCancel >= 100, "The cancellation test did not observe an active CPU run.");
var stopTime = Stopwatch.StartNew();
inFlight.Cancel();
await ExpectCanceled(pending, inFlight.Token);
stopTime.Stop();
double cpuMsAfter;
{
    var cpuAtStop = process.TotalProcessorTime;
    await Task.Delay(300);
    cpuMsAfter = (process.TotalProcessorTime - cpuAtStop).TotalMilliseconds;
}
Require(cpuMsAfter <= 100, "The inference process kept using CPU after cancellation returned.");
var recovery = await provider.TranscribeAsync(request);
Require(recovery.Text == results[0].Text, "Inference did not recover after cancellation.");
Console.Error.WriteLine($"Native cancellation returned in {stopTime.Elapsed.TotalMilliseconds:F0} ms.");

object? maximum = null;
if (!quick)
{
    await using var fullLength = File.OpenRead(Path.Combine(fixtures, "max-48k-stereo.wav"));
    var timer = Stopwatch.StartNew();
    var result = await provider.TranscribeAsync(Request(new AudioSource.FromStream(fullLength, "audio/wav"), "en"));
    timer.Stop();
    CheckSegments(result);
    Require(result.Duration == settings.MaxAudioDuration && result.Segments.Count > 0 && fullLength.CanRead,
        "The maximum-length WAV did not complete as expected.");
    maximum = new
    {
        sampleRate = 48000, channels = 2, dataBytes = fullLength.Length, audioSeconds = result.Duration.TotalSeconds,
        seconds = timer.Elapsed.TotalSeconds, realTimeFactor = timer.Elapsed.TotalSeconds / result.Duration.TotalSeconds,
        segments = result.Segments.Count, lastSegmentEnd = result.Segments[^1].End, peakWorkingSetBytes = Peak()
    };
}

long peak = Peak();
await provider.ShutdownAsync();
Require(!await provider.IsAvailableAsync() && provider.GetCapabilityDescriptor().ModelIds.Count == 0, "Shutdown did not unregister the model.");
var report = new
{
    dataset = new { manifest.Source, manifest.License, manifest.Revision },
    os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    runtime = RuntimeInformation.FrameworkDescription, executionTarget = "CPU",
    settings.CpuThreads, settings.MaxAudioDuration, settings.MaxRequestBytes,
    nativeVersion = descriptor.Metadata["whisper_cpp_version"], modelSha256 = descriptor.Metadata["weights_sha256"],
    initializationSeconds = loadTime.Elapsed.TotalSeconds, initializationPeakWorkingSetBytes = initializationPeak,
    shortCasePeakWorkingSetBytes = shortCasePeak, peakWorkingSetBytes = peak,
    estimatedWorkingSetBytes = descriptor.Memory.WorkingSetBytes, maximum,
    quality = rate, cases = results,
    cancellation = new { stopMilliseconds = stopTime.Elapsed.TotalMilliseconds, cpuMsBeforeCancel, cpuMsAfter, idleObservationMs = 300 },
    checks = new[] { "language and auto-detection", "segment timing and text-only", "stream ownership",
        "size and duration limits", "malformed and unsupported audio", "silence", "pre-start and in-flight cancellation",
        "post-cancel recovery", "shutdown" }
};
string output = JsonSerializer.Serialize(report, json);
await File.WriteAllTextAsync(args[2], output);
Console.WriteLine(JsonSerializer.Serialize(new { rate, maximum, peak, cancelMs = stopTime.Elapsed.TotalMilliseconds }, json));

long Peak()
{
    process.Refresh();
    return process.PeakWorkingSet64;
}

static TranscriptionRequest Request(AudioSource audio, string? language = null) => new() { Audio = audio, Language = language };

static void CheckSegments(TranscriptionResult result)
{
    TimeSpan previous = TimeSpan.Zero;
    for (int index = 0; index < result.Segments.Count; index++)
    {
        var item = result.Segments[index];
        Require(item.Index == index && item.Start >= previous && item.Start < item.End && item.End <= result.Duration &&
            item.Words is null && item.Text.Length > 0, $"Invalid segment {index}: {item.Start} - {item.End}, duration {result.Duration}.");
        previous = item.Start;
    }
}

static string[] Normalize(string text, string language)
{
    var builder = new StringBuilder();
    foreach (var rune in text.Normalize(NormalizationForm.FormKC).ToLowerInvariant().EnumerateRunes())
    {
        if (Rune.IsLetterOrDigit(rune) || Rune.GetUnicodeCategory(rune) == UnicodeCategory.NonSpacingMark)
            builder.Append(rune.ToString());
        else if (language == "en")
            builder.Append(rune.Value is '\'' or '’' ? '\'' : ' ');
    }
    var clean = builder.ToString();
    return language == "ja" ? clean.EnumerateRunes().Select(item => item.ToString()).ToArray()
        : clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}

static int Distance(string[] reference, string[] actual)
{
    var row = Enumerable.Range(0, actual.Length + 1).ToArray();
    for (int i = 1; i <= reference.Length; i++)
    {
        int diagonal = row[0];
        row[0] = i;
        for (int j = 1; j <= actual.Length; j++)
        {
            int previous = row[j];
            row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (reference[i - 1] == actual[j - 1] ? 0 : 1));
            diagonal = previous;
        }
    }
    return row[^1];
}

static byte[] CreateSilence(int frames)
{
    using var output = new MemoryStream(44 + frames * 2);
    using var writer = new BinaryWriter(output);
    writer.Write("RIFF"u8); writer.Write(36 + frames * 2); writer.Write("WAVEfmt "u8);
    writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(16000);
    writer.Write(32000); writer.Write((short)2); writer.Write((short)16);
    writer.Write("data"u8); writer.Write(frames * 2);
    output.SetLength(44 + frames * 2);
    return output.ToArray();
}

static async Task ExpectError(ISpeechTranscriptionProvider provider, TranscriptionRequest request, string code)
{
    try { await provider.TranscribeAsync(request); }
    catch (ProviderOperationException error) when (error.Error.Code == code) { return; }
    throw new InvalidOperationException($"Expected {code}.");
}

static async Task ExpectCanceled(Task<TranscriptionResult> pending, CancellationToken token)
{
    try { await pending; }
    catch (OperationCanceledException error) when (error.CancellationToken == token) { return; }
    throw new InvalidOperationException("Expected cancellation without a partial output.");
}

static void Require(bool valid, string message)
{
    if (!valid)
        throw new InvalidOperationException(message);
}

internal sealed record FixtureManifest(string Source, string License, string Revision, Fixture[] Cases);
internal sealed record Fixture(string Language, string File, string SentenceId, string Reference, long DurationTicks);
internal sealed record ScoredFixture(string Language, string File, string SentenceId, bool AutoLanguage,
    string Reference, string Text, double AudioSeconds, double ProcessingSeconds, int EditDistance, int ReferenceUnits,
    IReadOnlyList<TranscriptionSegment> Segments);

internal class DeclaredLengthStream(long length) : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => length;
    public override long Position { get => 0; set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

internal sealed class ForwardOnlyStream(ReadOnlyMemory<byte> bytes) : DeclaredLengthStream(bytes.Length)
{
    private int position;
    public override bool CanSeek => false;
    internal bool Disposed { get; private set; }
    internal TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int count = Math.Min(Math.Min(buffer.Length, 4096), bytes.Length - position);
        bytes.Slice(position, count).CopyTo(buffer);
        position += count;
        if (position == bytes.Length)
            Completed.TrySetResult();
        return ValueTask.FromResult(count);
    }

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}
