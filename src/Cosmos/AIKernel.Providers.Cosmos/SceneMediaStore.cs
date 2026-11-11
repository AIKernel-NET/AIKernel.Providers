using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

namespace AIKernel.Providers.Cosmos;

internal sealed class SceneMediaStore(SceneOptions options, TimeProvider clock)
{
    private sealed record Entry(string Scope, SceneMediaRegistration Registration, byte[] Bytes);
    private readonly object _sync = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private long _size;
    private bool _closed;
    private readonly SemaphoreSlim _uploads = new(options.MaxConcurrentUploads, options.MaxConcurrentUploads);

    internal async Task<SceneMediaReceipt> RegisterAsync(SceneGrant grant, SceneMediaRegistration registration,
        Stream source, CancellationToken cancellationToken)
    {
        if (!_uploads.Wait(0, cancellationToken)) throw new SceneException("media_busy");
        try { return await RegisterCoreAsync(grant, registration, source, cancellationToken).ConfigureAwait(false); }
        finally { _uploads.Release(); }
    }

    private async Task<SceneMediaReceipt> RegisterCoreAsync(SceneGrant grant, SceneMediaRegistration registration,
        Stream source, CancellationToken cancellationToken)
    {
        if (registration.MimeType != "image/png") throw new SceneException("media_unsupported");
        SceneJson.Id(registration.FrameId);
        var now = clock.GetUtcNow();
        if (registration.ExpiresAtUtc <= now || registration.ExpiresAtUtc > now + options.MediaRetention)
            throw new SceneException("media_expired");
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var count = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            if (bytes.Length + count > options.MaxImageBytes) throw new SceneException("media_too_large");
            await bytes.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }
        var data = bytes.ToArray();
        if (SceneJson.Hash(data) != registration.ContentHash) throw new SceneException("hash_mismatch");
        await Task.Run(() => ValidatePng(data, registration.Width, registration.Height, options.MaxImagePixels), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (_closed) throw new SceneException("disabled");
            Purge();
            if (_size + data.Length > options.MaxStoredMediaBytes || _entries.Count >= options.MaxJobs * options.MaxFrames)
                throw new SceneException("media_capacity");
            if (registration.ExpiresAtUtc <= clock.GetUtcNow()) throw new SceneException("media_expired");
            var id = "media-" + Guid.NewGuid().ToString("N");
            _entries.Add(id, new(SceneJson.Scope(grant), registration, data));
            _size += data.Length;
            return new(id, registration.ContentHash, data.Length, registration.ExpiresAtUtc);
        }
    }

    internal IReadOnlyList<ReadOnlyMemory<byte>> Resolve(SceneRequest request, SceneGrant grant, bool copy = true)
    {
        lock (_sync)
        {
            Purge();
            var images = new List<ReadOnlyMemory<byte>>();
            foreach (var reference in request.Media)
            {
                if (!_entries.TryGetValue(reference.MediaId, out var entry) || entry.Scope != SceneJson.Scope(grant))
                    throw new SceneException("media_unavailable");
                var r = entry.Registration;
                if (r.Snapshot != SceneJson.Snapshot(request) || r.FrameId != reference.FrameId || r.OffsetMs != reference.OffsetMs
                    || r.MimeType != reference.MimeType || r.Width != reference.Width || r.Height != reference.Height)
                    throw new SceneException("inconsistent_snapshot");
                // A private bounded copy stays valid if a concurrent purge erases storage.
                if (copy) images.Add(entry.Bytes.ToArray());
            }
            return images;
        }
    }

    internal void Clear()
    {
        lock (_sync)
        {
            foreach (var entry in _entries.Values) CryptographicOperations.ZeroMemory(entry.Bytes);
            _closed = true;
            _entries.Clear(); _size = 0;
        }
    }

    private void Purge()
    {
        foreach (var pair in _entries.Where(e => e.Value.Registration.ExpiresAtUtc <= clock.GetUtcNow()).ToArray())
        {
            _size -= pair.Value.Bytes.Length;
            CryptographicOperations.ZeroMemory(pair.Value.Bytes);
            _entries.Remove(pair.Key);
        }
    }

    // Deliberately small, fully validated initial format: 8-bit RGB/RGBA PNG,
    // non-interlaced. No EXIF/text/profile data is retained or forwarded.
    internal static void ValidatePng(byte[] data, int expectedWidth, int expectedHeight, int maxPixels)
    {
        try
        {
            if (data.Length < 45 || !data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new SceneException("invalid_media");
            int position = 8, width = 0, height = 0, channels = 0;
            bool header = false, ended = false, sawData = false, dataEnded = false;
            using var compressed = new MemoryStream();
            while (position < data.Length)
            {
                if (data.Length - position < 12) throw new SceneException("invalid_media");
                var length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position, 4));
                if (length > data.Length - position - 12) throw new SceneException("invalid_media");
                int size = (int)length;
                var type = System.Text.Encoding.ASCII.GetString(data, position + 4, 4);
                var payload = data.AsSpan(position + 8, size);
                var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position + 8 + size, 4));
                if (Crc(data.AsSpan(position + 4, size + 4)) != expectedCrc) throw new SceneException("invalid_media");
                if (!header && type != "IHDR") throw new SceneException("invalid_media");
                switch (type)
                {
                    case "IHDR":
                        if (header || size != 13) throw new SceneException("invalid_media");
                        width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(payload[..4]));
                        height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4)));
                        if (width <= 0 || height <= 0 || (long)width * height > maxPixels) throw new SceneException("media_too_large");
                        if (width != expectedWidth || height != expectedHeight) throw new SceneException("inconsistent_snapshot");
                        if (payload[8] != 8 || payload[9] is not (2 or 6) || payload[10] != 0 || payload[11] != 0 || payload[12] != 0)
                            throw new SceneException("media_unsupported");
                        channels = payload[9] == 2 ? 3 : 4;
                        header = true;
                        break;
                    case "IDAT":
                        if (ended || dataEnded) throw new SceneException("invalid_media");
                        compressed.Write(payload); sawData = true;
                        break;
                    case "IEND":
                        if (size != 0 || !sawData || position + 12 != data.Length) throw new SceneException("invalid_media");
                        ended = true; dataEnded = true;
                        break;
                    // Browser PNGs may include standard colour hints. They carry no text.
                    case "sRGB":
                        if (sawData || size != 1 || payload[0] > 3) throw new SceneException("invalid_media");
                        break;
                    case "gAMA":
                        if (sawData || size != 4) throw new SceneException("invalid_media");
                        break;
                    default: throw new SceneException("media_unsupported");
                }
                position += size + 12;
            }
            if (!ended) throw new SceneException("invalid_media");
            compressed.Position = 0;
            using var inflater = new ZLibStream(compressed, CompressionMode.Decompress);
            var row = new byte[checked(width * channels + 1)];
            for (int y = 0; y < height; y++)
            {
                inflater.ReadExactly(row);
                if (row[0] > 4) throw new SceneException("invalid_media");
            }
            if (inflater.ReadByte() != -1) throw new SceneException("media_too_large");
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or OverflowException or ArgumentException)
        { throw new SceneException("invalid_media"); }
    }

    private static uint Crc(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
        }
        return ~crc;
    }
}
