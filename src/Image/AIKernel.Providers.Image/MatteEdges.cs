using OpenCvSharp;

namespace AIKernel.Providers.Image;

internal static class MatteEdges
{
    internal static void Refine(Mat bgr, Mat matte, int radius, CancellationToken cancellationToken)
    {
        var window = new Size(2 * radius + 1, 2 * radius + 1);
        using var guide = new Mat();
        using var alpha = new Mat();
        using var meanGuide = new Mat();
        using var meanAlpha = new Mat();
        using var a = new Mat();
        using var b = new Mat();
        using var temporary = new Mat();
        Cv2.CvtColor(bgr, guide, ColorConversionCodes.BGR2GRAY);
        guide.ConvertTo(guide, MatType.CV_32FC1, 1d / 255d);
        matte.ConvertTo(alpha, MatType.CV_32FC1, 1d / 255d);
        Cv2.Blur(guide, meanGuide, window);
        Cv2.Blur(alpha, meanAlpha, window);

        Cv2.Multiply(guide, alpha, a);
        Cv2.Blur(a, a, window);
        Cv2.Multiply(meanGuide, meanAlpha, temporary);
        Cv2.Subtract(a, temporary, a);
        Cv2.Multiply(guide, guide, b);
        Cv2.Blur(b, b, window);
        Cv2.Multiply(meanGuide, meanGuide, temporary);
        Cv2.Subtract(b, temporary, b);
        using var epsilon = new Mat(1, 1, MatType.CV_64FC1, Scalar.All(.001));
        Cv2.Add(b, epsilon, b);
        Cv2.Divide(a, b, a);
        Cv2.Multiply(a, meanGuide, b);
        Cv2.Subtract(meanAlpha, b, b);
        Cv2.Blur(a, a, window);
        Cv2.Blur(b, b, window);
        Cv2.Multiply(a, guide, a);
        Cv2.Add(a, b, a);
        a.ConvertTo(temporary, MatType.CV_8UC1, 255);
        cancellationToken.ThrowIfCancellationRequested();

        // Keep the original matte wherever a full window contains certain foreground or background.
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, window);
        using var opaque = new Mat();
        using var transparent = new Mat();
        using var band = new Mat();
        Cv2.Erode(matte, opaque, kernel);
        Cv2.Dilate(matte, transparent, kernel);
        Cv2.Threshold(opaque, opaque, 253, 255, ThresholdTypes.BinaryInv);
        Cv2.Threshold(transparent, transparent, 1, 255, ThresholdTypes.Binary);
        Cv2.BitwiseAnd(opaque, transparent, band);
        temporary.CopyTo(matte, band);
    }

    internal static void Erode(Mat matte, int pixels)
    {
        var side = 2 * pixels + 1;
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(side, side));
        Cv2.Erode(matte, matte, kernel);
    }

    internal static void Decontaminate(Mat bgr, Mat matte, Mat sourceAlpha, CancellationToken cancellationToken)
    {
        var window = new Size(17, 17);
        using var background = new Mat();
        Cv2.Threshold(matte, background, 12, 1, ThresholdTypes.BinaryInv);
        if (!sourceAlpha.Empty())
        {
            using var visible = new Mat();
            Cv2.Threshold(sourceAlpha, visible, 245, 1, ThresholdTypes.Binary);
            Cv2.BitwiseAnd(background, visible, background);
        }
        background.ConvertTo(background, MatType.CV_32FC1);
        using var support = new Mat();
        using var weighted = new Mat();
        using var channel = new Mat();
        Cv2.Blur(background, support, window);
        if (!support.GetArray(out float[] weight) || !matte.GetArray(out byte[] opacity))
            throw new InvalidDataException("Cannot read the background samples or the edge mask.");

        for (int index = 0; index < 3; index++)
        {
            Cv2.ExtractChannel(bgr, channel, index);
            if (!channel.GetArray(out byte[] original)) throw new InvalidDataException("Cannot read the source colors.");
            channel.ConvertTo(weighted, MatType.CV_32FC1);
            Cv2.Multiply(weighted, background, weighted);
            Cv2.Blur(weighted, weighted, window);
            if (!weighted.GetArray(out float[] values)) throw new InvalidDataException("Cannot read the estimated background.");
            for (int pixel = 0; pixel < opacity.Length; pixel++)
            {
                if (opacity[pixel] is < 26 or > 249 || weight[pixel] < .03f) continue;
                var alpha = opacity[pixel] / 255f;
                var oldBackground = values[pixel] / weight[pixel];
                var foreground = (original[pixel] - (1 - alpha) * oldBackground) / alpha;
                original[pixel] = (byte)Math.Clamp(MathF.Round(foreground), 0, 255);
            }
            cancellationToken.ThrowIfCancellationRequested();
            channel.SetArray(original);
            Cv2.InsertChannel(channel, bgr, index);
        }
    }
}
