using System.Buffers.Binary;
using AIKernel.Dtos.Providers;
using AIKernel.Dtos.Providers.SpeechRecognition;

namespace AIKernel.Providers.Stt;

internal sealed record Pcm16Wave(float[] Samples, TimeSpan Duration)
{
    internal const int ModelSampleRate = 16_000;
    internal const int MaxSampleRate = 48_000;
    private const int MinSampleRate = 8_000;
    private static readonly Guid PcmSubtype = new("00000001-0000-0010-8000-00aa00389b71");

    internal static async Task<Pcm16Wave> ReadAsync(AudioSource audio, LocalSpeechRecognitionSettings settings, CancellationToken ct)
    {
        try
        {
            var reader = audio switch
            {
                AudioSource.Bytes bytes => new WaveReader(bytes.Data),
                AudioSource.FromStream { Content: { CanRead: true } stream } => new WaveReader(stream),
                _ => throw LocalSpeechRecognitionProvider.Error(ProviderErrorCodes.InvalidRequest, "A readable WAV input is required.")
            };
            return await ParseAsync(reader, settings, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        {
            ct.ThrowIfCancellationRequested();
            throw BadWave();
        }
    }

    private static async Task<Pcm16Wave> ParseAsync(WaveReader reader, LocalSpeechRecognitionSettings settings, CancellationToken ct)
    {
        long? knownLength = reader.Length;
        if (knownLength > settings.MaxRequestBytes)
            throw SizeLimit();

        var buffer = new byte[64 * 1024];
        await reader.ReadExactlyAsync(buffer.AsMemory(0, 12), ct).ConfigureAwait(false);
        if (!buffer.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !buffer.AsSpan(8, 4).SequenceEqual("WAVE"u8))
            throw BadWave();
        long riffSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(4));
        if (riffSize + 8 > settings.MaxRequestBytes)
            throw SizeLimit();
        if (riffSize < 4 || (knownLength.HasValue && knownLength != riffSize + 8))
            throw BadWave();

        Format? format = null;
        Pcm16Wave? wave = null;
        long remaining = riffSize - 4;
        while (remaining > 0)
        {
            if (remaining < 8)
                throw BadWave();
            await reader.ReadExactlyAsync(buffer.AsMemory(0, 8), ct).ConfigureAwait(false);
            long size = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(4));
            long padded = size + (size & 1);
            remaining -= 8;
            if (padded > remaining)
                throw BadWave();

            if (buffer.AsSpan(0, 4).SequenceEqual("fmt "u8))
            {
                if (format.HasValue || size < 16)
                    throw BadWave();
                int bytes = (int)Math.Min(size, 40);
                await reader.ReadExactlyAsync(buffer.AsMemory(0, bytes), ct).ConfigureAwait(false);
                format = ReadFormat(buffer.AsSpan(0, bytes), size);
                await SkipAsync(reader, buffer, padded - bytes, ct).ConfigureAwait(false);
            }
            else if (buffer.AsSpan(0, 4).SequenceEqual("data"u8))
            {
                if (format is not Format pcm || wave is not null || size < pcm.FrameBytes || size % pcm.FrameBytes != 0)
                    throw BadWave();
                wave = await ReadSamplesAsync(reader, buffer, pcm, (int)(size / pcm.FrameBytes), settings, ct).ConfigureAwait(false);
            }
            else
            {
                await SkipAsync(reader, buffer, padded, ct).ConfigureAwait(false);
            }
            remaining -= padded;
        }

        if (knownLength is null && await reader.ReadExtraAsync(buffer.AsMemory(0, 1), ct).ConfigureAwait(false))
            throw riffSize + 8 >= settings.MaxRequestBytes ? SizeLimit() : BadWave();
        return wave ?? throw BadWave();
    }

    private static Format ReadFormat(ReadOnlySpan<byte> bytes, long chunkSize)
    {
        int tag = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]);
        uint sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        uint byteRate = BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]);
        int frameBytes = BinaryPrimitives.ReadUInt16LittleEndian(bytes[12..]);
        int bits = BinaryPrimitives.ReadUInt16LittleEndian(bytes[14..]);
        if (tag == 0xfffe)
        {
            if (bytes.Length < 40)
                throw BadWave();
            int extensionSize = BinaryPrimitives.ReadUInt16LittleEndian(bytes[16..]);
            if (extensionSize < 22 || 18L + extensionSize > chunkSize)
                throw BadWave();
            if (new Guid(bytes[24..40]) != PcmSubtype || BinaryPrimitives.ReadUInt16LittleEndian(bytes[18..]) != 16)
                throw UnsupportedWave();
        }
        else if (tag != 1)
        {
            throw UnsupportedWave();
        }
        if (channels is not (1 or 2) || bits != 16 || sampleRate is < MinSampleRate or > MaxSampleRate)
            throw UnsupportedWave();
        if (frameBytes != 2 * channels || byteRate != sampleRate * frameBytes)
            throw BadWave();
        return new((int)sampleRate, frameBytes);
    }

    private static async Task<Pcm16Wave> ReadSamplesAsync(WaveReader reader, byte[] buffer, Format format,
        int frames, LocalSpeechRecognitionSettings settings, CancellationToken ct)
    {
        if ((long)frames * TimeSpan.TicksPerSecond > settings.MaxAudioDuration.Ticks * format.SampleRate)
            throw LocalSpeechRecognitionProvider.Error(ProviderErrorCodes.InputLimitExceeded, "The WAV duration exceeds the configured limit.");
        var duration = TimeSpan.FromTicks((long)frames * TimeSpan.TicksPerSecond / format.SampleRate);
        var samples = GC.AllocateUninitializedArray<float>(frames);
        for (int offset = 0; offset < frames;)
        {
            int count = Math.Min(frames - offset, buffer.Length / format.FrameBytes);
            await reader.ReadExactlyAsync(buffer.AsMemory(0, count * format.FrameBytes), ct).ConfigureAwait(false);
            DecodeFrames(buffer, samples.AsSpan(offset, count), format.FrameBytes);
            offset += count;
        }
        return new(Resample(samples, format.SampleRate, ct), duration);
    }

    private static void DecodeFrames(ReadOnlySpan<byte> bytes, Span<float> output, int frameBytes)
    {
        for (int index = 0; index < output.Length; index++)
        {
            int offset = index * frameBytes;
            int left = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..]);
            output[index] = frameBytes == 2 ? left / 32768f
                : (left + BinaryPrimitives.ReadInt16LittleEndian(bytes[(offset + 2)..])) / 65536f;
        }
    }

    private static float[] Resample(float[] source, int rate, CancellationToken ct)
    {
        if (rate == ModelSampleRate)
            return source;
        int count = Math.Max(1, (int)((long)source.Length * ModelSampleRate / rate));
        var result = GC.AllocateUninitializedArray<float>(count);
        for (int index = 0; index < count; index++)
        {
            if ((index & 0x3fff) == 0)
                ct.ThrowIfCancellationRequested();
            long start = (long)index * rate;
            int frame = (int)(start / ModelSampleRate);
            if (rate < ModelSampleRate)
            {
                float fraction = (start % ModelSampleRate) / (float)ModelSampleRate;
                result[index] = source[frame] + fraction * (source[Math.Min(frame + 1, source.Length - 1)] - source[frame]);
                continue;
            }

            // Average over the input interval before decimation, using fractional frame boundaries.
            long end = Math.Min(start + rate, (long)source.Length * ModelSampleRate);
            double sum = 0;
            for (long cursor = start; cursor < end;)
            {
                int sourceIndex = (int)(cursor / ModelSampleRate);
                long next = Math.Min(end, (sourceIndex + 1L) * ModelSampleRate);
                sum += source[sourceIndex] * (next - cursor);
                cursor = next;
            }
            result[index] = (float)(sum / (end - start));
        }
        return result;
    }

    private static async Task SkipAsync(WaveReader reader, byte[] buffer, long remaining, CancellationToken ct)
    {
        while (remaining > 0)
        {
            int count = (int)Math.Min(remaining, buffer.Length);
            await reader.ReadExactlyAsync(buffer.AsMemory(0, count), ct).ConfigureAwait(false);
            remaining -= count;
        }
    }

    private static Exception BadWave() =>
        LocalSpeechRecognitionProvider.Error(SpeechRecognitionErrorCodes.DecodeFailed, "The WAV data is truncated or malformed.");
    private static Exception UnsupportedWave() =>
        LocalSpeechRecognitionProvider.Error(ProviderErrorCodes.UnsupportedFormat, "Use PCM16 WAV with one or two channels and 8–48 kHz sampling.");
    private static Exception SizeLimit() =>
        LocalSpeechRecognitionProvider.Error(ProviderErrorCodes.InputLimitExceeded, "The WAV exceeds the configured byte limit.");

    private readonly record struct Format(int SampleRate, int FrameBytes);

    private sealed class WaveReader
    {
        private readonly Stream? stream;
        private ReadOnlyMemory<byte> remaining;
        internal long? Length { get; }

        internal WaveReader(ReadOnlyMemory<byte> bytes)
        {
            remaining = bytes;
            Length = bytes.Length;
        }

        internal WaveReader(Stream stream)
        {
            this.stream = stream;
            Length = stream.CanSeek ? stream.Length - stream.Position : null;
        }

        internal ValueTask ReadExactlyAsync(Memory<byte> buffer, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (stream is not null)
                return stream.ReadExactlyAsync(buffer, ct);
            if (remaining.Length < buffer.Length)
                throw BadWave();
            remaining[..buffer.Length].CopyTo(buffer);
            remaining = remaining[buffer.Length..];
            return ValueTask.CompletedTask;
        }

        internal async ValueTask<bool> ReadExtraAsync(Memory<byte> buffer, CancellationToken ct) =>
            await stream!.ReadAsync(buffer, ct).ConfigureAwait(false) != 0;
    }
}
