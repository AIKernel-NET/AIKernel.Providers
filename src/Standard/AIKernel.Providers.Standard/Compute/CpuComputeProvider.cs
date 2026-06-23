namespace AIKernel.Providers.Standard.Compute;

using AIKernel.Abstractions.Compute;
using AIKernel.Common.Results;
using AIKernel.Providers.Standard;
using System.Runtime.InteropServices;

/// <summary>
/// [EN] Standard CPU compute provider for deterministic local numeric operations.
/// [JA] 決定論的な local numeric operation 向けの標準 CPU compute Provider です。
/// </summary>
public sealed class CpuComputeProvider : StandardProviderBase, IComputeProvider
{
    /// <summary>[EN] Initializes the CPU compute provider. [JA] CPU compute Provider を初期化します。</summary>
    public CpuComputeProvider()
        : base(
            "providers.compute.cpu",
            "CPU Compute Provider",
            "0.1.3",
            ["compute.vector_add", "compute.dot", "compute.fill"],
            ["float32", "float64", "int32", "buffer"])
    {
    }

    /// <summary>
    /// [EN] Adds two float vectors deterministically.
    /// [JA] 2 つの float vector を決定論的に加算します。
    /// </summary>
    public IReadOnlyList<float> AddVectors(
        IReadOnlyList<float> left,
        IReadOnlyList<float> right)
        => RequireSuccess(TryAddVectors(left, right));

    /// <summary>
    /// [EN] Safely adds two float vectors as a Result pipeline.
    /// [JA] 2 つの float vector を Result pipeline として安全に加算します。
    /// </summary>
    public Result<IReadOnlyList<float>> TryAddVectors(
        IReadOnlyList<float>? left,
        IReadOnlyList<float>? right)
    {
        return
            from validLeft in ValidateVector(left, nameof(left))
            from validRight in ValidateVector(right, nameof(right))
            from _ in ValidateEqualLength(validLeft, validRight)
            select AddVectorsCore(validLeft, validRight);
    }

    /// <summary>
    /// [EN] Computes a deterministic float dot product.
    /// [JA] 決定論的な float dot product を計算します。
    /// </summary>
    public float Dot(
        IReadOnlyList<float> left,
        IReadOnlyList<float> right)
        => RequireSuccess(TryDot(left, right));

    /// <summary>
    /// [EN] Safely computes a deterministic float dot product as a Result pipeline.
    /// [JA] 決定論的な float dot product を Result pipeline として安全に計算します。
    /// </summary>
    public Result<float> TryDot(
        IReadOnlyList<float>? left,
        IReadOnlyList<float>? right)
    {
        return
            from validLeft in ValidateVector(left, nameof(left))
            from validRight in ValidateVector(right, nameof(right))
            from _ in ValidateEqualLength(validLeft, validRight)
            select DotCore(validLeft, validRight);
    }

    /// <summary>[EN] Returns whether CPU compute is available. [JA] CPU compute が利用可能かどうかを返します。</summary>
    public bool IsAvailable() => true;

    /// <summary>[EN] Creates a CPU compute buffer. [JA] CPU compute buffer を作成します。</summary>
    public Task<ComputeBuffer> CreateBufferAsync(int size)
        => Task.FromResult(RequireSuccess(TryCreateBuffer(size)));

    /// <summary>[EN] Safely creates a CPU compute buffer. [JA] CPU compute buffer を安全に作成します。</summary>
    public Result<ComputeBuffer> TryCreateBuffer(int size)
        => size >= 0
            ? Result<ComputeBuffer>.Success(new ComputeBuffer(size))
            : Result<ComputeBuffer>.Fail("Compute buffer size cannot be negative. ErrorCode=CPU_BUFFER_SIZE_NEGATIVE");

    /// <summary>[EN] Writes bytes into a CPU compute buffer. [JA] CPU compute buffer へ byte を書き込みます。</summary>
    public Task WriteBufferAsync(ComputeBuffer buffer, ReadOnlyMemory<byte> data)
    {
        RequireSuccess(TryWriteBuffer(buffer, data));
        return Task.CompletedTask;
    }

    /// <summary>[EN] Safely writes bytes into a CPU compute buffer. [JA] CPU compute buffer へ byte を安全に書き込みます。</summary>
    public Result<bool> TryWriteBuffer(ComputeBuffer? buffer, ReadOnlyMemory<byte> data)
        => buffer is null
            ? Result<bool>.Fail("Compute buffer is required. ErrorCode=CPU_BUFFER_REQUIRED")
            : Try.Run(() =>
            {
                buffer.Write(data);
                return true;
            });

    /// <summary>[EN] Reads bytes from a CPU compute buffer. [JA] CPU compute buffer から byte を読み取ります。</summary>
    public Task ReadBufferAsync(ComputeBuffer buffer, Memory<byte> destination)
    {
        RequireSuccess(TryReadBuffer(buffer, destination));
        return Task.CompletedTask;
    }

    /// <summary>[EN] Safely reads bytes from a CPU compute buffer. [JA] CPU compute buffer から byte を安全に読み取ります。</summary>
    public Result<bool> TryReadBuffer(ComputeBuffer? buffer, Memory<byte> destination)
        => buffer is null
            ? Result<bool>.Fail("Compute buffer is required. ErrorCode=CPU_BUFFER_REQUIRED")
            : Try.Run(() =>
            {
                buffer.Read(destination);
                return true;
            });

    /// <summary>[EN] Executes supported CPU kernels deterministically. [JA] 対応する CPU kernel を決定論的に実行します。</summary>
    public Task ExecuteKernelAsync(ComputeKernel kernel, params ComputeBuffer[] buffers)
    {
        RequireSuccess(TryExecuteKernel(kernel, buffers));
        return Task.CompletedTask;
    }

    /// <summary>[EN] Safely executes supported CPU kernels. [JA] 対応する CPU kernel を安全に実行します。</summary>
    public Result<bool> TryExecuteKernel(ComputeKernel? kernel, params ComputeBuffer[]? buffers)
        =>
            from validKernel in ValidateKernel(kernel)
            from validBuffers in ValidateBuffers(buffers)
            from supported in ValidateVectorAddKernel(validKernel)
            from executed in Try.Run(() =>
            {
                ExecuteVectorAdd(validBuffers);
                return true;
            })
            select executed;

    private static bool IsVectorAddKernel(ComputeKernel kernel)
        => kernel.Wgsl.Contains("out[i] = a[i] + b[i]", StringComparison.Ordinal)
           || kernel.Wgsl.Contains("out[i]=a[i]+b[i]", StringComparison.Ordinal);

    private static void ExecuteVectorAdd(IReadOnlyList<ComputeBuffer> buffers)
    {
        if (buffers.Count < 3)
        {
            throw new ArgumentException("Vector add requires three buffers: a, b, and output. ErrorCode=CPU_VECTOR_ADD_BUFFER_COUNT", nameof(buffers));
        }

        var left = MemoryMarshal.Cast<byte, float>(buffers[0].AsMemory().Span);
        var right = MemoryMarshal.Cast<byte, float>(buffers[1].AsMemory().Span);
        var output = MemoryMarshal.Cast<byte, float>(buffers[2].AsMemory().Span);
        if (left.Length != right.Length || output.Length < left.Length)
        {
            throw new ArgumentException("Vector add buffer sizes do not match. ErrorCode=CPU_VECTOR_ADD_BUFFER_SIZE_MISMATCH", nameof(buffers));
        }

        for (var index = 0; index < left.Length; index++)
        {
            output[index] = left[index] + right[index];
        }
    }

    private static Result<IReadOnlyList<float>> ValidateVector(IReadOnlyList<float>? vector, string name)
        => vector is null
            ? Result<IReadOnlyList<float>>.Fail($"{name} vector is required. ErrorCode=CPU_VECTOR_REQUIRED")
            : Result<IReadOnlyList<float>>.Success(vector);

    private static Result<bool> ValidateEqualLength(IReadOnlyList<float> left, IReadOnlyList<float> right)
        => left.Count == right.Count
            ? Result<bool>.Success(true)
            : Result<bool>.Fail("Vector lengths must match. ErrorCode=CPU_VECTOR_LENGTH_MISMATCH");

    private static IReadOnlyList<float> AddVectorsCore(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var result = new float[left.Count];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = left[index] + right[index];
        }

        return result;
    }

    private static float DotCore(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var sum = 0.0f;
        for (var index = 0; index < left.Count; index++)
        {
            sum += left[index] * right[index];
        }

        return sum;
    }

    private static Result<ComputeKernel> ValidateKernel(ComputeKernel? kernel)
        => kernel is null
            ? Result<ComputeKernel>.Fail("Compute kernel is required. ErrorCode=CPU_KERNEL_REQUIRED")
            : Result<ComputeKernel>.Success(kernel);

    private static Result<ComputeBuffer[]> ValidateBuffers(ComputeBuffer[]? buffers)
        => buffers is null
            ? Result<ComputeBuffer[]>.Fail("Compute buffers are required. ErrorCode=CPU_BUFFERS_REQUIRED")
            : Result<ComputeBuffer[]>.Success(buffers);

    private static Result<bool> ValidateVectorAddKernel(ComputeKernel kernel)
        => IsVectorAddKernel(kernel)
            ? Result<bool>.Success(true)
            : Result<bool>.Fail("CpuComputeProvider currently supports the vector-add compute kernel. ErrorCode=CPU_KERNEL_NOT_SUPPORTED");

    private static T RequireSuccess<T>(Result<T> result)
        => result.Match(
            error => throw new InvalidOperationException(error.Message),
            value => value);
}
