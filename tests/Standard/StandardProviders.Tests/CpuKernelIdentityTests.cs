using AIKernel.Abstractions.Compute;
using AIKernel.Providers.Standard.Compute;
using System.Runtime.InteropServices;

namespace AIKernel.Providers.Tests;

public sealed class CpuKernelIdentityTests
{
    [Theory]
    [InlineData(1, 1, "\n")]
    [InlineData(1, 1, "\r\n")]
    [InlineData(3, 64, "\n")]
    [InlineData(3, 64, "\r\n")]
    [InlineData(65, 32, "\n")]
    [InlineData(65, 32, "\r\n")]
    [InlineData(128, 256, "\n")]
    [InlineData(128, 256, "\r\n")]
    public async Task StandardVectorAdd_PreservesResults(int count, int workgroupSize, string newline)
    {
        var descriptor = ComputeKernel.CreateVectorAdd(count, workgroupSize);
        var kernel = new ComputeKernel(descriptor.Source.ReplaceLineEndings(newline), descriptor.DispatchX);
        var left = Enumerable.Range(0, count).Select(i => (float)i - 3.5f).ToArray();
        var right = Enumerable.Range(0, count).Select(i => (float)i * 2).ToArray();
        using var a = Buffer(left);
        using var b = Buffer(right);
        using var output = Buffer(Enumerable.Repeat(99f, count).ToArray());

        await new CpuComputeProvider().ExecuteKernelAsync(kernel, a, b, output);

        Assert.Equal(left.Zip(right, (x, y) => x + y).ToArray(), Floats(output));
        Assert.Equal(left, Floats(a));
        Assert.Equal(right, Floats(b));
    }

    [Theory]
    [InlineData("comment")]
    [InlineData("compact-comment")]
    [InlineData("changed-expression")]
    [InlineData("changed-binding")]
    [InlineData("changed-entry-point")]
    [InlineData("appended-source")]
    [InlineData("statement-only")]
    [InlineData("claimed-id")]
    [InlineData("unsupported-subtract")]
    public void OtherSource_IsRejectedWithoutWritingBuffers(string variant)
    {
        var source = OtherSource(variant);
        var kernel = new ComputeKernel(source, 1);
        using var a = Buffer([1f, 2f, 3f]);
        using var b = Buffer([4f, 5f, 6f]);
        using var output = Buffer([99f, 99f, 99f]);

        var result = new CpuComputeProvider().TryExecuteKernel(kernel, a, b, output);

        Assert.True(result.IsFailure, "An unregistered source must not select the CPU vector-add implementation.");
        Assert.Contains("CPU_KERNEL_NOT_SUPPORTED", result.Error!.Message);
        Assert.Equal([99f, 99f, 99f], Floats(output));
        Assert.Equal([1f, 2f, 3f], Floats(a));
        Assert.Equal([4f, 5f, 6f], Floats(b));
    }

    [Fact]
    public async Task ExecuteKernelAsync_RejectsMismatchedSourceWithoutExecuting()
    {
        using var a = Buffer([1f]);
        using var b = Buffer([2f]);
        using var output = Buffer([99f]);
        var provider = new CpuComputeProvider();
        var kernel = new ComputeKernel(OtherSource("changed-expression"), 1);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.ExecuteKernelAsync(kernel, a, b, output));

        Assert.Contains("CPU_KERNEL_NOT_SUPPORTED", error.Message);
        Assert.Equal([99f], Floats(output));
    }

    private static string OtherSource(string variant)
    {
        var standard = ComputeKernel.CreateVectorAdd(3).Source;
        return variant switch
        {
            "comment" => "@compute @workgroup_size(64)\nfn main() {\n// out[i] = a[i] + b[i]\n}",
            "compact-comment" => "@compute @workgroup_size(64)\nfn main() {\n// out[i]=a[i]+b[i]\n}",
            "changed-expression" => standard.Replace("a[i] + b[i];", "a[i] + b[i] + 100.0;", StringComparison.Ordinal),
            "changed-binding" => standard.Replace("@binding(1)", "@binding(3)", StringComparison.Ordinal),
            "changed-entry-point" => standard.Replace("fn main(", "fn other(", StringComparison.Ordinal),
            "appended-source" => standard + "\n// unregistered revision",
            "statement-only" => "out[i] = a[i] + b[i]",
            "claimed-id" => "// cpu.vector_add.f32.v1\n" + standard.Replace("a[i] + b[i];", "a[i] + b[i] * 2.0;", StringComparison.Ordinal),
            "unsupported-subtract" => standard.Replace("a[i] + b[i]", "a[i] - b[i]", StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(variant))
        };
    }

    private static ComputeBuffer Buffer(float[] values)
    {
        var buffer = new ComputeBuffer(values.Length * sizeof(float));
        MemoryMarshal.AsBytes(values.AsSpan()).CopyTo(buffer.AsMemory().Span);
        return buffer;
    }

    private static float[] Floats(ComputeBuffer buffer)
        => MemoryMarshal.Cast<byte, float>(buffer.AsMemory().Span).ToArray();
}
