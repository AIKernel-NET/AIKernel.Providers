using System.Buffers.Binary;
using OpenCvSharp;

namespace AIKernel.Providers.Image;

internal static class ImageInput
{
    internal const int DefaultMaximumBytes = 16 * 1024 * 1024;
    internal const int DefaultMaximumPixels = 4 * 1024 * 1024;

    internal static void ValidateLimits(int maximumBytes, int maximumPixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumBytes, 32 * 1024 * 1024);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPixels);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumPixels, 16 * 1024 * 1024);
    }

    internal static void ValidateSize(ReadOnlyMemory<byte> bytes, int maximumBytes)
    {
        if (bytes.IsEmpty || bytes.Length > maximumBytes)
            throw new InvalidDataException("The encoded image is empty or exceeds the byte limit.");
    }

    internal static Mat Decode(ReadOnlyMemory<byte> bytes, int maximumPixels)
    {
        var (width, height) = ReadDimensions(bytes.Span);
        if (width == 0 || height == 0 || width > 8192 || height > 8192 || (long)width * height > maximumPixels)
            throw new InvalidDataException("The image exceeds the decoded dimension or pixel limit.");

        Mat image;
        try
        {
            image = Cv2.ImDecode(bytes.Span, ImreadModes.Unchanged);
        }
        catch (OpenCVException exception)
        {
            throw new InvalidDataException("OpenCV could not decode the supplied image.", exception);
        }

        if (image.Empty() || image.Width != width || image.Height != height ||
            (image.Type() != MatType.CV_8UC1 && image.Type() != MatType.CV_8UC3 && image.Type() != MatType.CV_8UC4))
        {
            image.Dispose();
            throw new InvalidDataException("The image could not be decoded as an 8-bit PNG or JPEG.");
        }

        return image;
    }

    internal static Mat ToBgr(Mat decoded)
    {
        var result = new Mat();
        try
        {
            switch (decoded.Channels())
            {
                case 1:
                    Cv2.CvtColor(decoded, result, ColorConversionCodes.GRAY2BGR);
                    break;
                case 4:
                    Cv2.CvtColor(decoded, result, ColorConversionCodes.BGRA2BGR);
                    break;
                default:
                    decoded.CopyTo(result);
                    break;
            }
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static (uint Width, uint Height) ReadDimensions(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 33 && bytes.StartsWith<byte>([137, 80, 78, 71, 13, 10, 26, 10]) &&
            BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]) == 13 && bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            if (bytes[24] > 8)
                throw new InvalidDataException("PNG bit depth greater than 8 is unsupported.");

            return (BinaryPrimitives.ReadUInt32BigEndian(bytes[16..]), BinaryPrimitives.ReadUInt32BigEndian(bytes[20..]));
        }

        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8)
        {
            int offset = 2;
            while (offset < bytes.Length && bytes[offset++] == 0xff)
            {
                while (offset < bytes.Length && bytes[offset] == 0xff)
                    offset++;

                if (bytes.Length - offset < 3)
                    break;

                byte marker = bytes[offset++];
                if (marker is 0 or 0xd8 or 0xd9 or 0xda)
                    break;

                ushort length = BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..]);
                if (length < 2 || length > bytes.Length - offset)
                    break;

                if (marker is >= 0xc0 and <= 0xcf and not (0xc4 or 0xc8 or 0xcc))
                {
                    if (length < 8 || bytes[offset + 2] != 8)
                        break;

                    return (BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 5)..]),
                        BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 3)..]));
                }

                offset += length;
            }
        }

        throw new InvalidDataException("Supply a PNG or JPEG with a valid image header.");
    }
}
