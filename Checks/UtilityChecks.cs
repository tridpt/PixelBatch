using System.IO;
using System.Text.Json;
using PixelBatch.Core;
using SkiaSharp;

namespace PixelBatch.Checks;

internal static partial class Program
{
    private static void TestTransforms(string directory, string transparentSource)
    {
        string source = Path.Combine(directory, "transform-quadrants.png");
        SKColor[] colors = [SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Gold];
        using (var bitmap = new SKBitmap(120, 80))
        using (var canvas = new SKCanvas(bitmap))
        {
            for (int index = 0; index < 4; index++)
            {
                using var paint = new SKPaint { Color = colors[index] };
                canvas.DrawRect(index % 2 * 60, index / 2 * 40, 60, 40, paint);
            }
            Save(bitmap, source, SKEncodedImageFormat.Png);
        }
        int[][] order = [[0, 1, 2, 3], [2, 0, 3, 1], [3, 2, 1, 0], [1, 3, 0, 2]];
        foreach (var rotation in Enum.GetValues<ImageRotation>())
            foreach (bool horizontal in new[] { false, true })
                foreach (bool vertical in new[] { false, true })
                {
                    var options = new ProcessingOptions { Format = OutputFormat.Png, Resize = ResizeMode.Original,
                        Rotation = rotation, FlipHorizontal = horizontal, FlipVertical = vertical };
                    var processed = ImageProcessor.Process(source, options);
                    using var decoded = SKBitmap.Decode(processed.Bytes);
                    bool swap = rotation is ImageRotation.Clockwise90 or ImageRotation.CounterClockwise90;
                    bool correct = decoded.Width == (swap ? 80 : 120) && decoded.Height == (swap ? 120 : 80);
                    for (int corner = 0; corner < 4; corner++)
                    {
                        int expectedCorner = corner ^ (horizontal ? 1 : 0) ^ (vertical ? 2 : 0);
                        correct &= Near(decoded.GetPixel(corner % 2 == 0 ? 10 : decoded.Width - 10,
                            corner / 2 == 0 ? 10 : decoded.Height - 10), colors[order[(int)rotation][expectedCorner]]);
                    }
                    Assert(correct, $"Rotation {rotation}, horizontal flip {horizontal}, vertical flip {vertical} map all four source corners correctly");
                }
        var rotated = new ProcessingOptions { Format = OutputFormat.Png, Resize = ResizeMode.Original, Rotation = ImageRotation.Clockwise90 };
        Assert(ImageProcessor.CalculateSize(120, 80, rotated) == (80, 120), "Size and filename preflight use the rotated dimensions");
        Assert(ImageProcessor.CalculateSize(120, 80, rotated with { Resize = ResizeMode.Percentage, Percentage = 50 }) == (40, 60),
            "Percentage resizing uses the dimensions after rotation");
        Assert(ImageProcessor.CalculateSize(120, 80, rotated with { Resize = ResizeMode.Fit, MaxWidth = 100, MaxHeight = 60 }) == (40, 60),
            "Fit applies both output bounds after rotation");
        var crop = rotated with { Crop = CropAspect.Square, CropVertical = 0, CropHorizontal = 0, MaxWidth = 40, MaxHeight = 40, Resize = ResizeMode.Fit };
        using (var decoded = SKBitmap.Decode(ImageProcessor.Process(source, crop).Bytes))
            Assert(decoded.Width == 40 && decoded.Height == 40 && Near(decoded.GetPixel(5, 5), SKColors.Blue)
                && Near(decoded.GetPixel(35, 5), SKColors.Red), "Crop keeps the chosen region in rotated coordinates before scaling");
        using (var decoded = SKBitmap.Decode(ImageProcessor.Process(source, crop with { FlipHorizontal = true }).Bytes))
            Assert(Near(decoded.GetPixel(5, 5), SKColors.Red), "Explicit flips happen after rotation and before crop");
        using (var reference = ImageProcessor.RenderReference(source, crop with { FlipVertical = true, Watermark = WatermarkKind.Text, RepeatWatermark = true }))
        using (var expected = SKBitmap.Decode(ImageProcessor.Process(source, crop with { FlipVertical = true }).Bytes))
            Assert(reference.Pixels.SequenceEqual(expected.Pixels), "Comparison reference shares transform and crop geometry while excluding watermark pixels");
        using (var transformed = SKBitmap.Decode(ImageProcessor.Process(transparentSource, rotated with { FlipVertical = true }).Bytes))
            Assert(transformed.GetPixel(0, 0).Alpha == 0 && transformed.GetPixel(transformed.Width / 2, transformed.Height / 2).Alpha == 255,
                "Rotation and flipping preserve transparent and opaque pixels");
        for (int orientation = 1; orientation <= 8; orientation++)
        {
            string orientedPath = Path.Combine(directory, $"orientation-{orientation}.jpg");
            using var oriented = ImageProcessor.DecodeOriented(orientedPath);
            using var transformed = SKBitmap.Decode(ImageProcessor.Process(orientedPath, rotated with { FlipHorizontal = true }).Bytes);
            Assert(transformed.Width == oriented.Height && transformed.Height == oriented.Width
                && Near(transformed.GetPixel(8, 8), oriented.GetPixel(8, 8))
                && Near(transformed.GetPixel(transformed.Width - 8, 8), oriented.GetPixel(8, oriented.Height - 8)),
                $"User rotation and flip are applied after EXIF orientation {orientation}");
        }
        AssertThrows(() => (rotated with { Rotation = (ImageRotation)9 }).Validate(), "Unknown rotation values are rejected");
    }

    private static void TestRepeatedWatermark(string directory)
    {
        string source = Path.Combine(directory, "watermark-blank.png");
        using (var bitmap = new SKBitmap(600, 400, SKColorType.Rgba8888, SKAlphaType.Premul))
        {
            bitmap.Erase(SKColors.Transparent);
            Save(bitmap, source, SKEncodedImageFormat.Png);
        }
        var options = new ProcessingOptions { Format = OutputFormat.Png, Resize = ResizeMode.Original, Watermark = WatermarkKind.Text,
            WatermarkText = "© Ảnh sản phẩm", RepeatWatermark = true, WatermarkSize = 8, Opacity = 25 };
        var marked = ImageProcessor.Process(source, options);
        using (var decoded = SKBitmap.Decode(marked.Bytes))
        {
            int[] coverage = new int[4];
            int transparent = 0, maxAlpha = 0;
            for (int y = 0; y < decoded.Height; y++) for (int x = 0; x < decoded.Width; x++)
            {
                byte alpha = decoded.GetPixel(x, y).Alpha;
                if (alpha > 0) coverage[(y >= 200 ? 2 : 0) + (x >= 300 ? 1 : 0)]++;
                else transparent++;
                maxAlpha = Math.Max(maxAlpha, alpha);
            }
            Assert(coverage.All(count => count > 300), "Repeated Vietnamese watermark covers all four image quadrants");
            Assert(transparent > 120000 && maxAlpha is >= 50 and < 120, "Repeated watermark retains unmarked transparency and honors low opacity");
        }
        var unmarked = ImageProcessor.Process(source, options with { Watermark = WatermarkKind.None });
        Assert(unmarked.Bytes.SequenceEqual(ImageProcessor.Process(source, options with { Opacity = 0 }).Bytes),
            "Repeated watermark at zero opacity leaves the encoded PNG unchanged");
        Assert(!marked.Bytes.SequenceEqual(ImageProcessor.Process(source, options with { WatermarkAngle = 0 }).Bytes)
            && !marked.Bytes.SequenceEqual(ImageProcessor.Process(source, options with { WatermarkAngle = 45 }).Bytes),
            "Changing watermark angle changes the rendered text pattern");
        using (var sparse = SKBitmap.Decode(ImageProcessor.Process(source, options with { WatermarkSpacing = 300 }).Bytes))
        using (var dense = SKBitmap.Decode(ImageProcessor.Process(source, options with { WatermarkSpacing = 25 }).Bytes))
            Assert(sparse.Pixels.Count(pixel => pixel.Alpha > 0) < dense.Pixels.Count(pixel => pixel.Alpha > 0),
                "The spacing control changes repeated text density");
        foreach (int angle in new[] { -90, 90 })
            Assert(ImageProcessor.Process(source, options with { WatermarkAngle = angle, WatermarkText = new string('W', 160) }).Bytes.Length > 0,
                $"Repeated watermark supports long text at angle {angle} without invalid geometry");
        var logo = options with { Watermark = WatermarkKind.Image, WatermarkImagePath = source };
        Assert(ImageProcessor.Process(source, logo).Bytes.SequenceEqual(ImageProcessor.Process(source, logo with { RepeatWatermark = false }).Bytes),
            "The repeated text option does not change logo watermark behavior");
        using (var reference = ImageProcessor.RenderReference(source, options))
            Assert(reference.Pixels.All(pixel => pixel.Alpha == 0), "Repeated text is omitted from the comparison reference");
        string thin = Path.Combine(directory, "watermark-thin.png");
        using (var bitmap = new SKBitmap(16000, 1)) { bitmap.Erase(SKColors.Transparent); Save(bitmap, thin, SKEncodedImageFormat.Png); }
        Assert(ImageProcessor.Process(thin, options with { WatermarkAngle = 45 }).Width == 16000, "Extreme aspect ratio watermark rendering completes with bounded tile work");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            bool stopped = false;
            try { ImageProcessor.Process(source, options, cancelled.Token); } catch (OperationCanceledException) { stopped = true; }
            Assert(stopped, "Repeated watermark processing respects cancellation");
        }
        var settings = new BatchSettings { Options = options with { Rotation = ImageRotation.CounterClockwise90, FlipHorizontal = true, WatermarkAngle = 45, WatermarkSpacing = 175 },
            MultipleSizes = true, Sizes = [new(200, 300), new(400, 600)] };
        var store = new PresetStore(Path.Combine(directory, "utility-presets.json"));
        store.Save("Ảnh có watermark chéo", settings);
        Assert(store.Load().Single().Settings.Options == settings.Options && store.Load().Single().Settings.Sizes.SequenceEqual(settings.Sizes),
            "Personal presets retain rotation, flips, repeated watermark angle, spacing and output sizes");
        Assert(settings.GetVariants().All(variant => variant.Options.Rotation == ImageRotation.CounterClockwise90
            && variant.Options.FlipHorizontal && variant.Options.RepeatWatermark), "Every output size inherits image transforms and repeated watermark settings");
        var legacy = JsonSerializer.Deserialize<ProcessingOptions>("{\"Quality\":78}")!;
        Assert(legacy.Rotation == ImageRotation.None && !legacy.FlipHorizontal && !legacy.FlipVertical && !legacy.RepeatWatermark,
            "Older saved settings load without introducing transforms or repeated watermarks");
        AssertThrows(() => (options with { WatermarkAngle = 91 }).Validate(), "Out-of-range watermark angles are rejected");
        AssertThrows(() => (options with { WatermarkSpacing = 0 }).Validate(), "Zero watermark spacing is rejected");
        AssertThrows(() => (options with { WatermarkText = "   " }).Validate(), "Blank repeated watermark text is rejected");
    }
}
