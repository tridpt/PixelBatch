using System.IO;
using System.Text;
using SkiaSharp;

namespace PixelBatch.Core;

public static class ImageProcessor
{
    public const long MaximumPixels = 80_000_000;
    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };

    public static ImageInfo Inspect(string path)
    {
        using var codec = OpenCodec(path);
        bool swap = IsTransposed(codec.EncodedOrigin);
        return new ImageInfo(swap ? codec.Info.Height : codec.Info.Width,
            swap ? codec.Info.Width : codec.Info.Height, new FileInfo(path).Length,
            codec.EncodedFormat.ToString().ToUpperInvariant());
    }

    private static SKCodec OpenCodec(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy ảnh gốc.", path);
        var codec = SKCodec.Create(path) ?? throw new InvalidDataException("Tệp bị hỏng hoặc không phải định dạng ảnh được hỗ trợ.");
        if ((long)codec.Info.Width * codec.Info.Height > MaximumPixels || codec.Info.Width < 1 || codec.Info.Height < 1)
        {
            codec.Dispose();
            throw new InvalidDataException("Ảnh vượt giới hạn 80 megapixel. Hãy giảm kích thước trước khi thêm.");
        }
        if (codec.FrameCount > 1)
        {
            codec.Dispose();
            throw new InvalidDataException("Bản này hỗ trợ ảnh tĩnh. Ảnh động cần được chuyển thành ảnh tĩnh trước.");
        }
        return codec;
    }

    public static SKBitmap DecodeOriented(string path, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var codec = OpenCodec(path);
        using var srgb = SKColorSpace.CreateSrgb();
        using var decoded = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height,
            SKColorType.Rgba8888, SKAlphaType.Premul, srgb));
        var status = codec.GetPixels(decoded.Info, decoded.GetPixels());
        if (status != SKCodecResult.Success) throw new InvalidDataException("Không đọc được toàn bộ ảnh. Tệp có thể bị hỏng.");
        token.ThrowIfCancellationRequested();
        bool swap = IsTransposed(codec.EncodedOrigin);
        var result = new SKBitmap(new SKImageInfo(swap ? decoded.Height : decoded.Width,
            swap ? decoded.Width : decoded.Height, SKColorType.Rgba8888, SKAlphaType.Premul, srgb));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        var w = decoded.Width;
        var h = decoded.Height;
        var matrix = codec.EncodedOrigin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity
        };
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(decoded, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        return result;
    }

    private static bool IsTransposed(SKEncodedOrigin origin) => origin is SKEncodedOrigin.LeftTop
        or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

    public static (int Width, int Height) CalculateSize(int width, int height, ProcessingOptions options)
    {
        var geometry = CalculateGeometry(width, height, options);
        return (geometry.Width, geometry.Height);
    }

    public static ImageGeometry CalculateGeometry(int width, int height, ProcessingOptions options)
    {
        if (width < 1 || height < 1) throw new ArgumentException("Kích thước ảnh gốc không hợp lệ.");
        if (options.Rotation is ImageRotation.Clockwise90 or ImageRotation.CounterClockwise90) (width, height) = (height, width);
        double ratio = options.Crop switch
        {
            CropAspect.Square => 1d,
            CropAspect.Portrait4x5 => 4d / 5,
            CropAspect.Landscape16x9 => 16d / 9,
            CropAspect.Portrait9x16 => 9d / 16,
            CropAspect.Custom when options.CropWidth > 0 && options.CropHeight > 0 => (double)options.CropWidth / options.CropHeight,
            _ => (double)width / height
        };
        double cropWidth = width, cropHeight = height;
        if ((double)width / height > ratio) cropWidth = height * ratio;
        else cropHeight = width / ratio;
        double left = Math.Max(0, width - cropWidth) * options.CropHorizontal / 100d;
        double top = Math.Max(0, height - cropHeight) * options.CropVertical / 100d;
        double factor = options.Resize switch
        {
            ResizeMode.Fit => Math.Min(options.MaxWidth / cropWidth, options.MaxHeight / cropHeight),
            ResizeMode.Percentage => options.Percentage / 100d,
            _ => 1d
        };
        if (!options.AllowUpscale) factor = Math.Min(1d, factor);
        int targetWidth = Math.Max(1, (int)Math.Round(cropWidth * factor));
        int targetHeight = Math.Max(1, (int)Math.Round(cropHeight * factor));
        if (targetWidth > 16000 || targetHeight > 16000 || (long)targetWidth * targetHeight > MaximumPixels)
            throw new ArgumentException("Ảnh kết quả quá lớn. Hãy chọn kích thước nhỏ hơn (tối đa 16.000 px mỗi cạnh và 80 MP).");
        return new ImageGeometry(left, top, cropWidth, cropHeight, targetWidth, targetHeight);
    }

    private static SKBitmap Render(SKBitmap source, ProcessingOptions options, bool watermark, CancellationToken token)
    {
        var geometry = CalculateGeometry(source.Width, source.Height, options);
        using var srgb = SKColorSpace.CreateSrgb();
        var result = new SKBitmap(new SKImageInfo(geometry.Width, geometry.Height, SKColorType.Rgba8888, SKAlphaType.Premul, srgb));
        try
        {
            using var canvas = new SKCanvas(result);
            canvas.Clear(options.Format == OutputFormat.Jpeg ? SKColor.Parse(options.JpegBackground) : SKColors.Transparent);
            using var image = SKImage.FromBitmap(source);
            // Transform the source in canvas coordinates to avoid allocating another full-size bitmap.
            canvas.Save();
            canvas.ClipRect(new SKRect(0, 0, geometry.Width, geometry.Height));
            canvas.Scale((float)(geometry.Width / geometry.SourceWidth), (float)(geometry.Height / geometry.SourceHeight));
            canvas.Translate((float)-geometry.Left, (float)-geometry.Top);
            var transform = SourceTransform(source.Width, source.Height, options);
            canvas.Concat(transform);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell));
            canvas.Restore();
            token.ThrowIfCancellationRequested();
            if (watermark) DrawWatermark(canvas, geometry.Width, geometry.Height, options, token);
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    private static SKMatrix SourceTransform(int width, int height, ProcessingOptions options)
    {
        var matrix = options.Rotation switch
        {
            ImageRotation.Clockwise90 => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
            ImageRotation.HalfTurn => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
            ImageRotation.CounterClockwise90 => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
            _ => SKMatrix.Identity
        };
        if (options.Rotation is ImageRotation.Clockwise90 or ImageRotation.CounterClockwise90) (width, height) = (height, width);
        if (options.FlipHorizontal)
        {
            matrix.ScaleX = -matrix.ScaleX; matrix.SkewX = -matrix.SkewX; matrix.TransX = width - matrix.TransX;
        }
        if (options.FlipVertical)
        {
            matrix.SkewY = -matrix.SkewY; matrix.ScaleY = -matrix.ScaleY; matrix.TransY = height - matrix.TransY;
        }
        return matrix;
    }

    public static SKBitmap RenderReference(string path, ProcessingOptions options, CancellationToken token = default)
    {
        options.Validate();
        using var source = DecodeOriented(path, token);
        return Render(source, options, false, token);
    }

    public static ProcessedImage Process(string path, ProcessingOptions options, CancellationToken token = default)
    {
        options.Validate();
        using var source = DecodeOriented(path, token);
        using var result = Render(source, options, true, token);
        token.ThrowIfCancellationRequested();
        using var pixmap = result.PeekPixels();
        byte[] Encode(int quality)
        {
            token.ThrowIfCancellationRequested();
            using var data = options.Format switch
            {
                OutputFormat.Jpeg => pixmap.Encode(new SKJpegEncoderOptions(quality, SKJpegEncoderDownsample.Downsample420, SKJpegEncoderAlphaOption.BlendOnBlack)),
                OutputFormat.Png => pixmap.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, 9)),
                _ => pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, quality))
            };
            if (data is null) throw new InvalidOperationException("Không thể mã hóa ảnh với thiết lập đã chọn.");
            token.ThrowIfCancellationRequested();
            return data.ToArray();
        }

        int chosenQuality = options.Quality;
        byte[] bytes = Encode(chosenQuality);
        // Reuse the rendered pixels for every trial, including the watermark and alpha.
        if (options.TargetBytes > 0 && bytes.LongLength > options.TargetBytes && options.Format != OutputFormat.Png
            && options.MinimumQuality < options.Quality)
        {
            chosenQuality = options.MinimumQuality;
            bytes = Encode(chosenQuality);
            if (bytes.LongLength <= options.TargetBytes)
            {
                int low = options.MinimumQuality + 1, high = options.Quality - 1;
                while (low <= high)
                {
                    int middle = low + (high - low) / 2;
                    byte[] candidate = Encode(middle);
                    if (candidate.LongLength <= options.TargetBytes)
                    {
                        bytes = candidate; chosenQuality = middle; low = middle + 1;
                    }
                    else high = middle - 1;
                }
            }
        }
        return new ProcessedImage(bytes, result.Width, result.Height,
            options.Format switch { OutputFormat.Jpeg => ".jpg", OutputFormat.Png => ".png", _ => ".webp" },
            options.Format == OutputFormat.Png ? null : chosenQuality, options.TargetBytes);
    }

    private static readonly object WatermarkCacheLock = new();
    private static string? CachedWatermarkPath;
    private static DateTime CachedWatermarkWriteUtc;
    private static SKImage? CachedWatermarkImage;

    private static SKImage GetWatermarkImage(string path, CancellationToken token)
    {
        var fullPath = Path.GetFullPath(path);
        var writeUtc = File.GetLastWriteTimeUtc(fullPath);
        lock (WatermarkCacheLock)
        {
            if (CachedWatermarkImage is not null &&
                string.Equals(CachedWatermarkPath, fullPath, StringComparison.OrdinalIgnoreCase) &&
                CachedWatermarkWriteUtc == writeUtc)
            {
                return CachedWatermarkImage;
            }
        }

        using var logo = DecodeOriented(fullPath, token);
        var image = SKImage.FromBitmap(logo);
        lock (WatermarkCacheLock)
        {
            CachedWatermarkImage?.Dispose();
            CachedWatermarkPath = fullPath;
            CachedWatermarkWriteUtc = writeUtc;
            CachedWatermarkImage = image;
            return CachedWatermarkImage;
        }
    }

    private static void DrawWatermark(SKCanvas canvas, int width, int height, ProcessingOptions options, CancellationToken token)
    {
        if (options.Watermark == WatermarkKind.None || options.Opacity == 0) return;
        float margin = Math.Max(6f, Math.Min(width, height) * 0.035f);
        float availableWidth = Math.Max(1, width - 2 * margin);
        float availableHeight = Math.Max(1, height - 2 * margin);
        byte alpha = (byte)Math.Round(options.Opacity * 255 / 100d);

        if (options.Watermark == WatermarkKind.Image)
        {
            var image = GetWatermarkImage(options.WatermarkImagePath, token);
            float desiredWidth = width * Math.Clamp(options.WatermarkSize * 0.05f, 0.05f, 0.9f);
            float scale = Math.Min(desiredWidth / image.Width, Math.Min(availableWidth / image.Width, availableHeight / image.Height));
            float logoWidth = Math.Max(1, image.Width * scale);
            float logoHeight = Math.Max(1, image.Height * scale);
            var (x, y) = GetPosition(width, height, logoWidth, logoHeight, margin, options.Position);
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha(alpha), IsAntialias = true };
            canvas.DrawImage(image, new SKRect(x, y, x + logoWidth, y + logoHeight), new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
            return;
        }

        string text = options.WatermarkText.Normalize(NormalizationForm.FormC);
        using var typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold);
        using var font = new SKFont(typeface, Math.Max(1, Math.Min(width, height) * options.WatermarkSize / 100f));
        using var ink = new SKPaint { Color = SKColor.Parse(options.WatermarkColor).WithAlpha(alpha), IsAntialias = true };
        if (options.RepeatWatermark)
        {
            DrawRepeatedText(canvas, width, height, text, font, ink, options, token);
            return;
        }
        float textWidth = font.MeasureText(text);
        if (textWidth > availableWidth) font.Size *= availableWidth / textWidth;
        var metrics = font.Metrics;
        float textHeight = metrics.Descent - metrics.Ascent;
        if (textHeight > availableHeight) font.Size *= availableHeight / textHeight;
        metrics = font.Metrics;
        textWidth = font.MeasureText(text);
        textHeight = metrics.Descent - metrics.Ascent;
        var (tx, ty) = GetPosition(width, height, textWidth, textHeight, margin, options.Position);
        float baseline = ty - metrics.Ascent;
        using var shadow = new SKPaint { Color = SKColors.Black.WithAlpha((byte)(alpha * 0.6)), IsAntialias = true };
        canvas.DrawText(text, tx + Math.Max(0.5f, font.Size / 30f), baseline + Math.Max(0.5f, font.Size / 30f), SKTextAlign.Left, font, shadow);
        canvas.DrawText(text, tx, baseline, SKTextAlign.Left, font, ink);
    }

    private static void DrawRepeatedText(SKCanvas canvas, int width, int height, string text, SKFont font,
        SKPaint ink, ProcessingOptions options, CancellationToken token)
    {
        // Work in the rotated image's bounds; this also keeps extreme aspect ratios inexpensive.
        double radians = options.WatermarkAngle * Math.PI / 180;
        float cos = (float)Math.Abs(Math.Cos(radians)), sin = (float)Math.Abs(Math.Sin(radians));
        float halfWidth = (width * cos + height * sin) / 2;
        float halfHeight = (width * sin + height * cos) / 2;
        float textWidth = font.MeasureText(text);
        float maximumWidth = Math.Max(1, Math.Min(width, height) * 0.8f);
        if (textWidth > maximumWidth) font.Size *= maximumWidth / textWidth;
        // Whitespace-only strings are rejected by validation; other narrow strings still need a finite pitch.
        textWidth = Math.Max(1, font.MeasureText(text));
        var metrics = font.Metrics;
        float textHeight = Math.Max(1, metrics.Descent - metrics.Ascent);
        float gap = Math.Max(4, Math.Min(width, height) * 0.07f * options.WatermarkSpacing / 100);
        float stepX = Math.Max(8, textWidth + gap), stepY = Math.Max(8, textHeight + gap);
        // Keep work bounded even for a 1 × 16,000 image or a long string scaled to fit it.
        double estimated = (2 * halfWidth / stepX + 3) * (2 * halfHeight / stepY + 3);
        if (estimated > 20000)
        {
            float scale = (float)Math.Sqrt(estimated / 20000);
            stepX *= scale; stepY *= scale;
        }
        using var shadow = new SKPaint { Color = SKColors.Black.WithAlpha((byte)(ink.Color.Alpha * 0.6)), IsAntialias = true };
        float offset = Math.Max(0.5f, font.Size / 30f);
        canvas.Save();
        try
        {
            canvas.ClipRect(new SKRect(0, 0, width, height));
            canvas.Translate(width / 2f, height / 2f);
            canvas.RotateDegrees(options.WatermarkAngle);
            int rows = (int)Math.Ceiling(halfHeight / stepY) + 1;
            int columns = (int)Math.Ceiling(halfWidth / stepX) + 1;
            for (int row = -rows; row <= rows; row++)
            {
                token.ThrowIfCancellationRequested();
                float y = row * stepY - (metrics.Ascent + metrics.Descent) / 2;
                for (int column = -columns; column <= columns; column++)
                {
                    if ((column & 31) == 0) token.ThrowIfCancellationRequested();
                    float x = column * stepX - textWidth / 2 + ((row & 1) == 0 ? 0 : stepX / 2);
                    canvas.DrawText(text, x + offset, y + offset, SKTextAlign.Left, font, shadow);
                    canvas.DrawText(text, x, y, SKTextAlign.Left, font, ink);
                }
            }
        }
        finally { canvas.Restore(); }
    }

    private static (float X, float Y) GetPosition(int width, int height, float objectWidth, float objectHeight,
        float margin, WatermarkPosition position)
    {
        float left = Math.Min(margin, Math.Max(0, (width - objectWidth) / 2));
        float top = Math.Min(margin, Math.Max(0, (height - objectHeight) / 2));
        return position switch
        {
            WatermarkPosition.TopLeft => (left, top),
            WatermarkPosition.TopRight => (width - objectWidth - left, top),
            WatermarkPosition.BottomLeft => (left, height - objectHeight - top),
            WatermarkPosition.Center => ((width - objectWidth) / 2, (height - objectHeight) / 2),
            _ => (width - objectWidth - left, height - objectHeight - top)
        };
    }

    public static byte[] Thumbnail(string path, int maximumSide = 900, CancellationToken token = default)
    {
        using var source = DecodeOriented(path, token);
        float ratio = Math.Min(1f, (float)maximumSide / Math.Max(source.Width, source.Height));
        using var thumb = source.Resize(new SKImageInfo(Math.Max(1, (int)(source.Width * ratio)),
            Math.Max(1, (int)(source.Height * ratio))), new SKSamplingOptions(SKCubicResampler.Mitchell));
        using var image = SKImage.FromBitmap(thumb ?? source);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
