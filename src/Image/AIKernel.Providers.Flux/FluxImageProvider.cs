using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using AIKernel.Contracts.Images;

namespace AIKernel.Providers.Flux;

#if AK_FLUX_CUDA
/// <summary>Weight page observations from the last successful CUDA edit.</summary>
/// <param name="ResidentHostWeightBytes">Checkpoint pages in the RAM cache.</param>
/// <param name="ResidentDeviceWeightBytes">Checkpoint pages in the GPU cache.</param>
/// <param name="HostReads">Tensor reads delivered to host buffers.</param>
/// <param name="DeviceReads">Tensor reads delivered directly to GPU buffers.</param>
/// <param name="CheckpointPageReads">Logical checkpoint reads across both caches.</param>
/// <param name="DevicePageIns">Checkpoint pages uploaded to the GPU during this edit.</param>
public sealed record FluxCudaWeightStatistics(long ResidentHostWeightBytes, long ResidentDeviceWeightBytes,
    long HostReads, long DeviceReads, long CheckpointPageReads, long DevicePageIns);

/// <summary>Local FLUX.2 klein 4B CUDA editing using AIKernel-managed weight pages.</summary>
public sealed class FluxCudaImageProvider : IImagePromptEditProvider, IDisposable
#else
/// <summary>Observations from the weight page cache after the last successful FLUX edit.</summary>
/// <param name="ResidentWeightBytes">Weight bytes currently in the AIKernel RAM tier.</param>
/// <param name="NativeReadRequests">Native tensor reads fulfilled by the ABI.</param>
/// <param name="CheckpointPageReads">Logical SSD checkpoint page reads during the edit.</param>
public sealed record FluxWeightStatistics(long ResidentWeightBytes, long NativeReadRequests, long CheckpointPageReads);

/// <summary>Local FLUX.2 klein 4B CPU editing using an AIKernel-managed weight source.</summary>
public sealed class FluxImageProvider : IImagePromptEditProvider, IDisposable
#endif
{
    private readonly string modelDirectory;
    private readonly string swapDirectory;
    private readonly int cachePages;
    private readonly int cpuThreads;
    private readonly SemaphoreSlim execution = new(1);
    private FluxCheckpointPages? weights;
#if AK_FLUX_CUDA
    private readonly int deviceOrdinal;
    private readonly int deviceCachePages;
    private FluxCudaDevice? cuda;
    private FluxCudaWeightStatistics? lastWeightStatistics;
#else
    private FluxWeightStatistics? lastWeightStatistics;
#endif
    private bool disposed;

#if AK_FLUX_CUDA
    /// <summary>Configures the local checkpoints and the AIKernel RAM and GPU caches.</summary>
    /// <param name="modelDirectory">Directory containing the registered FLUX, Qwen3, and VAE files.</param>
    /// <param name="swapDirectory">Private local SSD directory for the AIKernel virtual memory arenas.</param>
    /// <param name="weightCacheMiB">Host weight cache budget from 8 to 4096 MiB in 4 MiB increments.</param>
    /// <param name="cpuThreads">Number of native CPU workers.</param>
    /// <param name="deviceOrdinal">CUDA ordinal among the devices visible to this process.</param>
    /// <param name="deviceCacheMiB">GPU weight cache budget from 4 to 4096 MiB in 4 MiB increments.</param>
    public FluxCudaImageProvider(string modelDirectory, string swapDirectory, int weightCacheMiB = 256,
        int cpuThreads = 4, int deviceOrdinal = 0, int deviceCacheMiB = 64)
#else
    /// <summary>Configures locally installed checkpoints and a bounded RAM cache for weight pages.</summary>
    /// <param name="modelDirectory">Directory containing the registered FLUX, Qwen3, and VAE files.</param>
    /// <param name="swapDirectory">Private local SSD directory for the AIKernel virtual memory arena.</param>
    /// <param name="weightCacheMiB">Host budget for checkpoint pages in 4 MiB increments.</param>
    /// <param name="cpuThreads">Number of native CPU workers.</param>
    public FluxImageProvider(string modelDirectory, string swapDirectory, int weightCacheMiB = 256, int cpuThreads = 4)
#endif
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(swapDirectory);
        if (weightCacheMiB is < 4 or > 4096 || weightCacheMiB % 4 != 0)
            throw new ArgumentOutOfRangeException(nameof(weightCacheMiB));
        if (cpuThreads is < 1 or > 16)
            throw new ArgumentOutOfRangeException(nameof(cpuThreads));
        this.modelDirectory = Path.GetFullPath(modelDirectory);
        this.swapDirectory = Path.GetFullPath(swapDirectory);
#if AK_FLUX_CUDA
        if (weightCacheMiB < 8)
            throw new ArgumentOutOfRangeException(nameof(weightCacheMiB), "CUDA requires a host budget of at least 8 MiB.");
        if (deviceCacheMiB is < 4 or > 4096 || deviceCacheMiB % 4 != 0)
            throw new ArgumentOutOfRangeException(nameof(deviceCacheMiB));
        ArgumentOutOfRangeException.ThrowIfNegative(deviceOrdinal);
        this.deviceOrdinal = deviceOrdinal;
        deviceCachePages = deviceCacheMiB / 4;
        cachePages = weightCacheMiB / 4 - 1;
#else
        cachePages = weightCacheMiB / 4;
#endif
        this.cpuThreads = cpuThreads;
    }

#if AK_FLUX_CUDA
    /// <summary>Gets the completed edit's checkpoint paging statistics.</summary>
    public FluxCudaWeightStatistics? LastWeightStatistics => Volatile.Read(ref lastWeightStatistics);
#else
    /// <summary>Gets the completed inference page statistics. Native activation memory is outside these counters.</summary>
    public FluxWeightStatistics? LastWeightStatistics => Volatile.Read(ref lastWeightStatistics);
#endif

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

#if AK_FLUX_CUDA
        cuda ??= FluxCudaDevice.Open(deviceOrdinal);
        var source = weights ??= new FluxCheckpointPages(modelDirectory, swapDirectory, cachePages, token, cuda, deviceCachePages);
#else
        var source = weights ??= new FluxCheckpointPages(modelDirectory, swapDirectory, cachePages, token);
#endif
        using var job = FluxNative.Create();
        if (job.IsInvalid) throw new OutOfMemoryException("Could not allocate a native FLUX job.");
        using var cancel = token.UnsafeRegister(static state => FluxNative.Cancel((FluxNative.FluxJob)state!), job);
        token.ThrowIfCancellationRequested();

        var output = new byte[reference.Length];
        Span<byte> error = stackalloc byte[1024];
        var operation = new ReadOperation(source, token);
        long initialReads = source.GetStatistics().BackingReads;
#if AK_FLUX_CUDA
        var initialDevice = source.GetDeviceStatistics();
#endif
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
                int status = FluxNative.Edit(job, &request, &ReadWeight,
#if AK_FLUX_CUDA
                    &ReadDeviceWeight, GCHandle.ToIntPtr(state), cuda,
#else
                    GCHandle.ToIntPtr(state),
#endif
                    result,
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
#if AK_FLUX_CUDA
        var deviceStatistics = source.GetDeviceStatistics();
        Volatile.Write(ref lastWeightStatistics, new FluxCudaWeightStatistics(
            (long)(statistics.RamBlocks + deviceStatistics.RamBlocks) * FluxCheckpointPages.PageBytes,
            (long)deviceStatistics.VramBlocks * FluxCheckpointPages.PageBytes,
            operation.ReadRequests, operation.DeviceReadRequests,
            statistics.BackingReads - initialReads + deviceStatistics.BackingReads - initialDevice.BackingReads,
            deviceStatistics.DevicePageIns - initialDevice.DevicePageIns));
#else
        Volatile.Write(ref lastWeightStatistics, new FluxWeightStatistics(
            (long)statistics.RamBlocks * FluxCheckpointPages.PageBytes, operation.ReadRequests, statistics.BackingReads - initialReads));
#endif
        return output;
    }

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value + '\0');

    private sealed class ReadOperation(FluxCheckpointPages source, CancellationToken token)
    {
        internal ExceptionDispatchInfo? Failure;
        internal long ReadRequests;
#if AK_FLUX_CUDA
        internal long DeviceReadRequests;
#endif

        internal unsafe void Read(byte* path, ulong offset, void* destination, nuint size, bool deviceDestination)
        {
            string name = Marshal.PtrToStringUTF8((nint)path) ?? throw new InvalidDataException("Missing checkpoint path.");
            source.Read(name, offset, (nint)destination, size, token, deviceDestination);
#if AK_FLUX_CUDA
            if (deviceDestination)
                Interlocked.Increment(ref DeviceReadRequests);
            else
#endif
                Interlocked.Increment(ref ReadRequests);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int ReadWeight(nint state, byte* path, ulong offset, void* destination, nuint size)
        => ReadWeight(state, path, offset, destination, size, deviceDestination: false);

#if AK_FLUX_CUDA
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int ReadDeviceWeight(nint state, byte* path, ulong offset, void* destination, nuint size)
        => ReadWeight(state, path, offset, destination, size, deviceDestination: true);
#endif

    private static unsafe int ReadWeight(nint state, byte* path, ulong offset, void* destination, nuint size,
        bool deviceDestination)
    {
        var operation = (ReadOperation)GCHandle.FromIntPtr(state).Target!;
        try
        {
            operation.Read(path, offset, destination, size, deviceDestination);
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
            try { weights?.Dispose(); }
            finally
            {
                weights = null;
#if AK_FLUX_CUDA
                cuda?.Dispose();
                cuda = null;
#endif
            }
        }
        finally { execution.Release(); }
    }
}
