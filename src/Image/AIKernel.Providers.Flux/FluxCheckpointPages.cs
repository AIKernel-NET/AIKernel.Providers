using System.Security.Cryptography;
using AIKernel.Kernel.Memory;

namespace AIKernel.Providers.Flux;

internal sealed class FluxCheckpointPages : IVirtualMemorySource, IDisposable
{
    internal const int PageBytes = 4 * 1024 * 1024;
    private static readonly (string Name, long Bytes, string Hash)[] Checkpoints =
    [
        ("flux-2-klein-4b-Q4_0.gguf", 2_460_378_560, "d1023499ef3f2f82ff7c50e6778495195c1b6cc34835741778868428111f9ff4"),
        ("Qwen3-4B-Q4_K_M.gguf", 2_497_280_256, "7485fe6f11af29433bc51cab58009521f205840f5b4ae3a32fa7f92e8534fdf5"),
        ("flux2-vae.safetensors", 168_120_878, "ca70d2202afe6415bdbcb8793ba8cd99fd159cfe6192381504d6c4d3036e0f04")
    ];

    private readonly Dictionary<string, Checkpoint> byPath = new(OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly VirtualMemorySpace pages;
    internal string[] Paths { get; } = new string[Checkpoints.Length];

    private sealed record Checkpoint(FileStream File, long Bytes, long FirstPage, long PageCount);

    internal FluxCheckpointPages(string directory, string swapDirectory, int pageCount, CancellationToken token)
    {
        long nextPage = 0;
        var scratch = new byte[PageBytes];
        try
        {
            for (int i = 0; i < Checkpoints.Length; i++)
            {
                var (name, bytes, hash) = Checkpoints[i];
                var path = Path.Combine(directory, name);
                if (!File.Exists(path))
                    throw new InvalidOperationException($"The registered checkpoint '{name}' is missing.");

                var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess);
                long count = (bytes + PageBytes - 1) / PageBytes;
                byPath.Add(path, new Checkpoint(file, bytes, nextPage, count));
                Paths[i] = path;
                VerifyCheckpoint(file, bytes, hash, name, scratch, token);
                nextPage += count;
            }
            pages = new VirtualMemorySpace(nextPage, PageBytes, pageCount, swapDirectory, source: this);
        }
        catch
        {
            foreach (var checkpoint in byPath.Values)
                checkpoint.File.Dispose();
            throw;
        }
    }

    private static void VerifyCheckpoint(FileStream file, long bytes, string hash, string name, byte[] scratch, CancellationToken token)
    {
        if (file.Length != bytes)
            throw new InvalidDataException($"The size of '{name}' does not match the registered FLUX.2 4B release.");

        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int count;
        while ((count = file.Read(scratch)) != 0)
        {
            token.ThrowIfCancellationRequested();
            digest.AppendData(scratch, 0, count);
        }
        if (!digest.GetHashAndReset().AsSpan().SequenceEqual(Convert.FromHexString(hash)))
            throw new InvalidDataException($"The SHA-256 of '{name}' does not match the registered FLUX.2 4B release.");
    }

    void IVirtualMemorySource.ReadBlock(long blockIndex, Span<byte> destination)
    {
        var checkpoint = byPath.Values.Single(entry => blockIndex >= entry.FirstPage && blockIndex - entry.FirstPage < entry.PageCount);
        long offset = (blockIndex - checkpoint.FirstPage) * PageBytes;
        int actual = (int)Math.Min(destination.Length, checkpoint.Bytes - offset);
        int next = 0;
        while (next < actual)
        {
            int count = RandomAccess.Read(checkpoint.File.SafeFileHandle, destination[next..actual], offset + next);
            if (count == 0)
                throw new EndOfStreamException("The registered checkpoint was truncated.");
            next += count;
        }
        destination[actual..].Clear();
    }

    internal unsafe void Read(string path, ulong offset, nint destination, nuint size, CancellationToken token)
    {
        if (!byPath.TryGetValue(Path.GetFullPath(path), out var checkpoint) || size > int.MaxValue ||
            offset > (ulong)checkpoint.Bytes || (ulong)size > (ulong)checkpoint.Bytes - offset)
            throw new InvalidDataException("The native runtime requested a read outside its registered checkpoints.");

        int remaining = (int)size;
        long position = (long)offset;
        while (remaining > 0)
        {
            token.ThrowIfCancellationRequested();
            int start = (int)(position % PageBytes);
            int length = Math.Min(remaining, PageBytes - start);
            nint target = destination;
            pages.UseComputeBlock(checkpoint.FirstPage + position / PageBytes,
                (source, _) => source.Slice(start, length).CopyTo(new Span<byte>((void*)target, length)));
            position += length;
            remaining -= length;
            destination += length;
        }
    }

    internal VirtualMemoryStatistics GetStatistics() => pages.GetStatistics();

    public void Dispose()
    {
        try { pages.Dispose(); }
        finally
        {
            foreach (var checkpoint in byPath.Values)
                checkpoint.File.Dispose();
        }
    }
}
