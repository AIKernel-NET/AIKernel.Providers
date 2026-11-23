using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using AIKernel.Dtos.Providers;
using AIKernel.Dtos.Providers.SpeechRecognition;
using Microsoft.Win32.SafeHandles;

namespace AIKernel.Providers.Stt;

internal sealed partial class WhisperModel : IDisposable
{
    internal const string ModelId = "openai/whisper-small";
    internal const string ModelRevision = "5359861c739e955e79d9a303bcbc70fb988958b1";
    internal const string ModelSha256 = "1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b";
    internal const string NativeVersion = "1.9.5";
    private const long ModelBytes = 487_601_967;
    private const string LibraryName = "aikernel_whisper";
    private const long TimestampUnit = 10 * TimeSpan.TicksPerMillisecond;
    private const long AnalysisWindow = 30 * TimeSpan.TicksPerSecond / TimestampUnit;
    private readonly WhisperHandle context;

    internal unsafe WhisperModel(string modelPath)
    {
        using var file = new FileStream(modelPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, FileOptions.SequentialScan);
        if (file.Length != ModelBytes || !SHA256.HashData(file).AsSpan().SequenceEqual(Convert.FromHexString(ModelSha256)))
            throw new InvalidDataException("The Whisper small model does not match the pinned file.");
        if (GetAbiVersion() != 1)
            throw new InvalidDataException("Unexpected Whisper bridge ABI version.");
        ValidateCpuRequirements();

        file.Position = 0;
        var state = new ModelReader(file);
        GCHandle reader = GCHandle.Alloc(state);
        try
        {
            // The native loader reads from the same open handle that passed verification.
            var opened = Open(&ReadModel, GCHandle.ToIntPtr(reader));
            if (opened.IsInvalid || state.Failed)
            {
                opened.Dispose();
                throw new InvalidDataException("Whisper could not load the verified model.");
            }
            context = opened;
        }
        finally
        {
            reader.Free();
        }
    }

    internal unsafe TranscriptionSegment[] Transcribe(Pcm16Wave wave, string? requestedLanguage, int cpuThreads,
        CancellationToken ct, out string? language)
    {
        ct.ThrowIfCancellationRequested();
        GCHandle cancellation = GCHandle.Alloc(ct);
        try
        {
            fixed (float* samples = wave.Samples)
            {
                int status = Run(context, samples, wave.Samples.Length, requestedLanguage ?? "auto",
                    cpuThreads, &ShouldCancel, GCHandle.ToIntPtr(cancellation));
                ct.ThrowIfCancellationRequested();
                if (status != 0)
                    throw LocalSpeechRecognitionProvider.Error(ProviderErrorCodes.ExecutionFailed, "Whisper CPU transcription failed.");
            }
        }
        finally
        {
            cancellation.Free();
        }

        language = Marshal.PtrToStringUTF8(GetLanguage(context));
        if (requestedLanguage is not null && requestedLanguage != language)
            throw BadOutput();

        int count = GetSegmentCount(context);
        if (count < 0)
            throw BadOutput();
        var segments = new List<TranscriptionSegment>(count);
        long inputEnd = (wave.Duration.Ticks + TimestampUnit - 1) / TimestampUnit;
        long previousStart = 0;
        for (int index = 0; index < count; index++)
        {
            ct.ThrowIfCancellationRequested();
            nint textPointer = GetSegment(context, index, out long start, out long end);
            string text = Marshal.PtrToStringUTF8(textPointer)?.Trim() ?? throw BadOutput();
            if (text.Length == 0)
                continue;
            if (start < previousStart || start >= end || end > inputEnd + AnalysisWindow)
                throw BadOutput();
            previousStart = start;
            // Whisper decodes padded 30-second windows and can timestamp beyond the last sample.
            // Keep segments on the original audio timeline and discard segments wholly in padding.
            if (start >= inputEnd)
                continue;
            long startTicks = start * TimestampUnit;
            long endTicks = Math.Min(end * TimestampUnit, wave.Duration.Ticks);
            if (startTicks >= endTicks)
                throw BadOutput();
            segments.Add(new()
            {
                Index = segments.Count,
                Start = TimeSpan.FromTicks(startTicks),
                End = TimeSpan.FromTicks(endTicks),
                Text = text
            });
        }
        return segments.ToArray();
    }

    private static Exception BadOutput() =>
        LocalSpeechRecognitionProvider.Error(ProviderErrorCodes.InvalidOutput, "Whisper returned invalid language or segment information.");

    private static void ValidateCpuRequirements()
    {
        int requirements = GetCpuRequirements();
        bool f16c = Avx.IsSupported && (X86Base.CpuId(1, 0).Ecx & (1 << 29)) != 0;
        if (((requirements & 1) != 0 && !Avx.IsSupported) ||
            ((requirements & 2) != 0 && !Avx2.IsSupported) ||
            ((requirements & 4) != 0 && !Bmi2.IsSupported) ||
            ((requirements & 8) != 0 && !Fma.IsSupported) ||
            ((requirements & 16) != 0 && !f16c))
            throw new PlatformNotSupportedException("The native Whisper CPU build requires an unavailable instruction set.");
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe nuint ReadModel(nint user, void* output, nuint count)
    {
        var state = (ModelReader)GCHandle.FromIntPtr(user).Target!;
        try
        {
            return (nuint)state.Stream.ReadAtLeast(new Span<byte>(output, checked((int)count)), (int)count, false);
        }
        catch (Exception error) when (error is IOException or OverflowException or ArgumentException)
        {
            state.Failed = true;
            return 0;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ShouldCancel(nint user) =>
        ((CancellationToken)GCHandle.FromIntPtr(user).Target!).IsCancellationRequested ? 1 : 0;

    public void Dispose() => context.Dispose();

    private sealed class ModelReader(FileStream stream)
    {
        internal FileStream Stream { get; } = stream;
        internal bool Failed { get; set; }
    }

    private sealed class WhisperHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public WhisperHandle() : base(ownsHandle: true) { }

        protected override bool ReleaseHandle()
        {
            WhisperModel.Close(handle);
            return true;
        }
    }

    [LibraryImport(LibraryName, EntryPoint = "ak_whisper_abi_version")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int GetAbiVersion();

    [LibraryImport(LibraryName, EntryPoint = "ak_whisper_cpu_requirements")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int GetCpuRequirements();

    [LibraryImport(LibraryName, EntryPoint = "ak_whisper_open")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe partial WhisperHandle Open(delegate* unmanaged[Cdecl]<nint, void*, nuint, nuint> reader, nint user);

    [LibraryImport(LibraryName, EntryPoint = "ak_whisper_close")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void Close(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "ak_whisper_transcribe", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe partial int Run(WhisperHandle handle, float* mono, int count, string language, int threads,
        delegate* unmanaged[Cdecl]<nint, int> cancel, nint user);

    [LibraryImport(LibraryName, EntryPoint = "ak_whisper_language")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial nint GetLanguage(WhisperHandle handle);

    [LibraryImport(LibraryName, EntryPoint = "ak_whisper_segment_count")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int GetSegmentCount(WhisperHandle handle);

    [LibraryImport(LibraryName, EntryPoint = "ak_whisper_segment")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial nint GetSegment(WhisperHandle handle, int index, out long start, out long end);
}
