using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using AIKernel.Contracts.Images;

namespace AIKernel.Providers.Flux;

/// <summary>Observations from the weight page cache after the last successful FLUX edit.</summary>
/// <param name="ResidentWeightBytes">Weight bytes currently in the AIKernel RAM tier.</param>
/// <param name="NativeReadRequests">Native tensor reads fulfilled by the ABI.</param>
/// <param name="CheckpointPageReads">Logical SSD checkpoint page reads during the edit.</param>
public sealed record FluxWeightStatistics(long ResidentWeightBytes, long NativeReadRequests, long CheckpointPageReads);

/// <summary>Local FLUX.2 klein 4B CPU editing using an AIKernel-managed weight source.</summary>
public sealed class FluxImageProvider : IImagePromptEditProvider, IDisposable
{
    private readonly string modelDirectory;
    private readonly string swapDirectory;
    private readonly int cachePages;
    private readonly int cpuThreads;
    private readonly SemaphoreSlim execution = new(1);
    private FluxCheckpointPages? weights;
    private FluxWeightStatistics? lastWeightStatistics;
    private bool disposed;

    /// <summary>Configures locally installed checkpoints and a bounded RAM cache for weight pages.</summary>
    /// <param name="modelDirectory">Directory containing the registered FLUX, Qwen3, and VAE files.</param>
    /// <param name="swapDirectory">Private local SSD directory for the AIKernel virtual memory arena.</param>
    /// <param name="weightCacheMiB">RAM budget for checkpoint pages in 4 MiB increments, from 4 to 4096 MiB.</param>
    /// <param name="cpuThreads">Number of native CPU workers.</param>
    public FluxImageProvider(string modelDirectory, string swapDirectory, int weightCacheMiB = 256, int cpuThreads = 4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(swapDirectory);
        if (weightCacheMiB is < 4 or > 4096 || weightCacheMiB % 4 != 0)
            throw new ArgumentOutOfRangeException(nameof(weightCacheMiB));
        if (cpuThreads is < 1 or > 16)
            throw new ArgumentOutOfRangeException(nameof(cpuThreads));
        this.modelDirectory = Path.GetFullPath(modelDirectory);
        this.swapDirectory = Path.GetFullPath(swapDirectory);
        cachePages = weightCacheMiB / 4;
        this.cpuThreads = cpuThreads;
    }

    /// <summary>Gets the completed inference page statistics. Native activation memory is outside these counters.</summary>
    public FluxWeightStatistics? LastWeightStatistics => Volatile.Read(ref lastWeightStatistics);

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> EditAsync(ReadOnlyMemory<byte> referenceRgb24, int width, int height,
        string prompt, long seed, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (width is < 128 or > 1024 || width % 16 != 0 || height is < 128 or > 1024 || height % 16 != 0)
            throw new ArgumentException("Dimensions must be multiples of 16 between 128 and 1024.");
        if (referenceRgb24.Length != width * height * 3)
            throw new ArgumentException("Expected packed RGB24 pixels.", nameof(referenceRgb24));
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 2048 || prompt.Contains('\0'))
            throw new ArgumentException("Use a non-empty editing instruction of at most 2048 characters.", nameof(prompt));
        ArgumentOutOfRangeException.ThrowIfNegative(seed);

        await execution.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Volatile.Write(ref lastWeightStatistics, null);
            return await Task.Run(() => Run(referenceRgb24, width, height, prompt, seed, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }
        finally { execution.Release(); }
    }

    private unsafe byte[] Run(ReadOnlyMemory<byte> reference, int width, int height, string prompt, long seed, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("The packaged FLUX ABI supports Windows x64.");
        token.ThrowIfCancellationRequested();
        if (FluxNative.Version() != 1)
            throw new InvalidOperationException("Unsupported FLUX ABI version.");

        var source = weights ??= new FluxCheckpointPages(modelDirectory, swapDirectory, cachePages, token);
        using var job = FluxNative.Create();
        if (job.IsInvalid) throw new OutOfMemoryException("Could not allocate a native FLUX job.");
        using var cancel = token.UnsafeRegister(static state => FluxNative.Cancel((FluxNative.FluxJob)state!), job);
        token.ThrowIfCancellationRequested();

        var output = new byte[reference.Length];
        Span<byte> error = stackalloc byte[1024];
        var operation = new ReadOperation(source, token);
        long initialReads = source.GetStatistics().BackingReads;
        var state = GCHandle.Alloc(operation);
        try
        {
            fixed (byte* diffusion = Utf8(source.Paths[0]), encoder = Utf8(source.Paths[1]), vae = Utf8(source.Paths[2]),
                instruction = Utf8(prompt), input = reference.Span, result = output, diagnostic = error)
            {
                var request = new FluxNative.Request
                {
                    StructureSize = (uint)sizeof(FluxNative.Request),
                    Width = (uint)width, Height = (uint)height, CpuThreads = (uint)cpuThreads, Seed = seed,
                    DiffusionPath = diffusion, EncoderPath = encoder, VaePath = vae, Prompt = instruction,
                    Reference = input, ReferenceSize = (nuint)reference.Length
                };
                int status = FluxNative.Edit(job, &request, &ReadWeight, GCHandle.ToIntPtr(state), result,
                    (nuint)output.Length, diagnostic, (nuint)error.Length);
                token.ThrowIfCancellationRequested();
                operation.Failure?.Throw();
                if (status == FluxNative.Canceled) throw new OperationCanceledException("Native FLUX edit canceled.");
                if (status != 0)
                {
                    int end = error.IndexOf((byte)0);
                    throw new InvalidOperationException($"FLUX edit failed ({status}). {Encoding.UTF8.GetString(end >= 0 ? error[..end] : error)}");
                }
            }
        }
        finally { state.Free(); }

        var statistics = source.GetStatistics();
        Volatile.Write(ref lastWeightStatistics, new FluxWeightStatistics(
            (long)statistics.RamBlocks * FluxCheckpointPages.PageBytes, operation.ReadRequests, statistics.BackingReads - initialReads));
        return output;
    }

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value + '\0');

    private sealed class ReadOperation(FluxCheckpointPages source, CancellationToken token)
    {
        internal ExceptionDispatchInfo? Failure;
        internal long ReadRequests;
        internal unsafe void Read(byte* path, ulong offset, void* destination, nuint size)
        {
            string name = Marshal.PtrToStringUTF8((nint)path) ?? throw new InvalidDataException("Missing checkpoint path.");
            source.Read(name, offset, (nint)destination, size, token);
            Interlocked.Increment(ref ReadRequests);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int ReadWeight(nint state, byte* path, ulong offset, void* destination, nuint size)
    {
        var operation = (ReadOperation)GCHandle.FromIntPtr(state).Target!;
        try
        {
            operation.Read(path, offset, destination, size);
            return 0;
        }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(ref operation.Failure, ExceptionDispatchInfo.Capture(exception), null);
            return 1;
        }
    }

    /// <summary>Waits for the current native edit, then closes the model files and their cache.</summary>
    public void Dispose()
    {
        execution.Wait();
        try
        {
            disposed = true;
            weights?.Dispose();
            weights = null;
        }
        finally { execution.Release(); }
    }
}
