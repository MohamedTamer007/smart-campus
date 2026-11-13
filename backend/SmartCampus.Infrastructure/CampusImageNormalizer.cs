using SkiaSharp;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

/// <summary>Validates campus JPG/PNG/WebP uploads and stores/sends metadata-free JPEG pixels.</summary>
public static class CampusImageNormalizer
{
    public const int MaximumUploadBytes = 5 * 1024 * 1024;
    public const int MaximumPixelCount = 16_000_000;
    public const int MaximumEdge = 2048;
    public const int MaximumNormalizedBytes = 1_900_000;

    public static byte[] ToJpeg(byte[] image, int maximumBytes = MaximumNormalizedBytes)
    {
        if (image.Length is <= 0 or > MaximumUploadBytes || maximumBytes is <= 0 or > MaximumNormalizedBytes)
            throw new RuleException("invalid_upload");

        using var sourceData = SKData.CreateCopy(image);
        using var codec = SKCodec.Create(sourceData);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp) ||
            codec.Info.Width <= 0 || codec.Info.Height <= 0 ||
            (long)codec.Info.Width * codec.Info.Height > MaximumPixelCount)
            throw new RuleException("invalid_upload");

        using var decoded = SKBitmap.Decode(sourceData) ?? throw new RuleException("invalid_upload");
        SKBitmap current = decoded;
        SKBitmap? owned = null;
        try
        {
            while (true)
            {
                var longest = Math.Max(current.Width, current.Height);
                var scale = Math.Min(1d, (double)MaximumEdge / longest);
                var targetWidth = Math.Max(1, (int)Math.Round(current.Width * scale));
                var targetHeight = Math.Max(1, (int)Math.Round(current.Height * scale));
                if (targetWidth != current.Width || targetHeight != current.Height)
                {
                    var resized = current.Resize(new SKImageInfo(targetWidth, targetHeight), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
                        ?? throw new RuleException("invalid_upload");
                    owned?.Dispose();
                    owned = resized;
                    current = resized;
                }

                using var outputImage = SKImage.FromBitmap(current);
                using var encoded = outputImage.Encode(SKEncodedImageFormat.Jpeg, current.Width <= 512 && current.Height <= 512 ? 65 : 82);
                if (encoded is null) throw new RuleException("invalid_upload");
                if (encoded.Size <= maximumBytes) return encoded.ToArray();
                if (current.Width <= 384 && current.Height <= 384) throw new RuleException("invalid_upload");

                var smaller = current.Resize(new SKImageInfo(
                    Math.Max(1, (int)(current.Width * .75)),
                    Math.Max(1, (int)(current.Height * .75))), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
                    ?? throw new RuleException("invalid_upload");
                owned?.Dispose();
                owned = smaller;
                current = smaller;
            }
        }
        finally
        {
            owned?.Dispose();
        }
    }
}
