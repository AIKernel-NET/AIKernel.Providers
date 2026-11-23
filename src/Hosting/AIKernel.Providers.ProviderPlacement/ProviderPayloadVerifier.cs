using System.Buffers;
using System.Security.Cryptography;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Providers;

namespace AIKernel.Providers.ProviderPlacement;

/// <summary>[EN] Verifies a bounded transfer to a staging stream. [JA] サイズ上限付きの転送を一時ストリームで検証します。</summary>
public static class ProviderPayloadVerifier
{
    /// <summary>[EN] Copies while verifying length and SHA-256. Discard the staged data on failure. [JA] 長さと SHA-256 を検証しながらコピーし、失敗時は一時データを破棄します。</summary>
    public static async ValueTask CopyAsync(
        Stream input, Stream staging, ProviderPayloadReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(staging);
        if (!IsValid(reference)) throw InvalidOutput();

        var expected = Convert.FromHexString(reference.Sha256);
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long remaining = reference.SizeBytes;
        try
        {
            while (true)
            {
                int readLimit = remaining >= buffer.Length ? buffer.Length : (int)remaining + 1;
                var count = await input.ReadAsync(buffer.AsMemory(0, readLimit), cancellationToken);
                if (count == 0) break;
                if (count > remaining) throw InvalidOutput();
                hash.AppendData(buffer, 0, count);
                await staging.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                remaining -= count;
            }
            if (remaining != 0 || !CryptographicOperations.FixedTimeEquals(expected, hash.GetHashAndReset()))
                throw InvalidOutput();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    internal static bool IsValid(ProviderPayloadReference? reference) =>
        reference is { SizeBytes: >= 0, Sha256.Length: 64 } && reference.Sha256.All(Uri.IsHexDigit);

    internal static ProviderOperationException InvalidOutput() => new(
        new ExecutionError(ProviderErrorCodes.InvalidOutput, "The transferred provider payload failed integrity validation."));
}
