using System.Buffers.Binary;

namespace AIKernel.Providers.Tts;

internal readonly record struct PcmWave(int SampleRate, int Channels, int Frames)
{
    internal static bool TryRead(ReadOnlySpan<byte> wave, out PcmWave format)
    {
        format = default;
        if (wave.Length < 44 || !wave[..4].SequenceEqual("RIFF"u8) || !wave[8..12].SequenceEqual("WAVE"u8)
            || BinaryPrimitives.ReadUInt32LittleEndian(wave[4..8]) != wave.Length - 8)
            return false;

        var offset = 12;
        int channels = 0, rate = 0, dataSize = 0;
        while (offset <= wave.Length - 8)
        {
            var id = wave.Slice(offset, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(wave[(offset + 4)..]);
            offset += 8;
            if (size > wave.Length - offset)
                return false;
            var chunk = wave.Slice(offset, (int)size);
            if (id.SequenceEqual("fmt "u8))
            {
                if (channels != 0 || size < 16 || BinaryPrimitives.ReadUInt16LittleEndian(chunk) != 1)
                    return false;
                channels = BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]);
                rate = BinaryPrimitives.ReadInt32LittleEndian(chunk[4..]);
                if (channels is not (1 or 2) || rate is < 8000 or > 192000
                    || BinaryPrimitives.ReadUInt32LittleEndian(chunk[8..]) != rate * channels * 2
                    || BinaryPrimitives.ReadUInt16LittleEndian(chunk[12..]) != channels * 2
                    || BinaryPrimitives.ReadUInt16LittleEndian(chunk[14..]) != 16)
                    return false;
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (dataSize != 0 || size == 0 || channels == 0 || size % (channels * 2) != 0)
                    return false;
                dataSize = (int)size;
            }
            offset += (int)(size + (size & 1));
        }

        if (offset != wave.Length || channels == 0 || dataSize == 0)
            return false;
        format = new PcmWave(rate, channels, dataSize / (channels * 2));
        return true;
    }
}
