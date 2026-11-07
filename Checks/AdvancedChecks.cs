using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using PixelBatch.Core;
using SkiaSharp;

namespace PixelBatch.Checks;

internal static partial class Program
{
    private static void TestCropping(string directory, string transparentSource)
    {
        string source = Path.Combine(directory, "crop-bands.png");
        using (var bitmap = new SKBitmap(600, 200))
        using (var canvas = new SKCanvas(bitmap))
        {
            SKColor[] colors = [SKColors.Red, SKColors.Lime, SKColors.Blue];
            for (int index = 0; index < 3; index++)
            {
                using var paint = new SKPaint { Color = colors[index] };
                canvas.DrawRect(index * 200, 0, 200, 200, paint);
            }
            Save(bitmap, source, SKEncodedImageFormat.Png);
        }
        byte[] hash = SHA256.HashData(File.ReadAllBytes(source));
        var options = new ProcessingOptions { Format = OutputFormat.Png, Resize = ResizeMode.Original, Crop = CropAspect.Square };
        foreach (var (position, color) in new[] { (0, SKColors.Red), (50, SKColors.Lime), (100, SKColors.Blue) })
        {
            var result = ImageProcessor.Process(source, options with { CropHorizontal = position });
            using var decoded = SKBitmap.Decode(result.Bytes);
            Assert(decoded.Width == 200 && decoded.Height == 200 && Near(decoded.GetPixel(100, 100), color),
                $"Square crop at horizontal {position}% keeps the intended part of the source");
        }
        string verticalSource = Path.Combine(directory, "crop-vertical.png");
        using (var bitmap = new SKBitmap(200, 600))
        using (var canvas = new SKCanvas(bitmap))
        {
            SKColor[] colors = [SKColors.Red, SKColors.Lime, SKColors.Blue];
            for (int index = 0; index < 3; index++)
            {
                using var paint = new SKPaint { Color = colors[index] };
                canvas.DrawRect(0, index * 200, 200, 200, paint);
            }
            Save(bitmap, verticalSource, SKEncodedImageFormat.Png);
        }
        foreach (var (position, color) in new[] { (0, SKColors.Red), (50, SKColors.Lime), (100, SKColors.Blue) })
        {
            using var decoded = SKBitmap.Decode(ImageProcessor.Process(verticalSource, options with { CropVertical = position }).Bytes);
            Assert(Near(decoded.GetPixel(100, 100), color), $"Square crop at vertical {position}% keeps the intended source region");
        }
        Assert(ImageProcessor.CalculateSize(2400, 1600, options with { Crop = CropAspect.Portrait4x5 }) == (1280, 1600), "4:5 crop produces the expected portrait dimensions");
        Assert(ImageProcessor.CalculateSize(2400, 1600, options with { Crop = CropAspect.Landscape16x9 }) == (2400, 1350), "16:9 crop trims height without distorting the source");
        Assert(ImageProcessor.CalculateSize(2400, 1600, options with { Crop = CropAspect.Portrait9x16 }) == (900, 1600), "9:16 crop produces a vertical image");
        Assert(ImageProcessor.CalculateSize(600, 200, options with { Crop = CropAspect.Custom, CropWidth = 3, CropHeight = 2 }) == (300, 200), "Custom crop ratios are applied before resize");
        Assert(ImageProcessor.CalculateSize(600, 200, options with { Resize = ResizeMode.Fit, MaxWidth = 100, MaxHeight = 100 }) == (100, 100), "Crop is fitted into the output bounds after selecting its region");
        Assert(ImageProcessor.CalculateSize(600, 200, options with { Resize = ResizeMode.Fit, MaxWidth = 400, MaxHeight = 400 }) == (200, 200), "Crop respects the existing no-upscale setting");
        Assert(ImageProcessor.CalculateSize(600, 200, options with { Resize = ResizeMode.Fit, MaxWidth = 400, MaxHeight = 400, AllowUpscale = true }) == (400, 400), "Upscaling a crop requires explicit permission");
        Assert(ImageProcessor.CalculateSize(600, 200, options with { Resize = ResizeMode.Percentage, Percentage = 50 }) == (100, 100), "Percentage resize is relative to the cropped region");
        using (var reference = ImageProcessor.RenderReference(source, options with { Watermark = WatermarkKind.Text, WatermarkText = "WATERMARK", Opacity = 100, WatermarkSize = 20 }))
        using (var expected = SKBitmap.Decode(ImageProcessor.Process(source, options).Bytes))
            Assert(reference.Width == expected.Width && reference.Height == expected.Height && Enumerable.Range(0, reference.Height)
                .All(y => Enumerable.Range(0, reference.Width).All(x => reference.GetPixel(x, y) == expected.GetPixel(x, y))),
                "Comparison reference uses the same crop and dimensions before watermark and compression");
        using (var reference = ImageProcessor.RenderReference(transparentSource, options with { Format = OutputFormat.Jpeg }))
            Assert(reference.GetPixel(0, 0).Alpha == 255 && Near(reference.GetPixel(0, 0), SKColors.White),
                "JPEG comparison reference uses the chosen transparency background");
        string rotated = Path.Combine(directory, "orientation-6.jpg");
        using (var normalized = ImageProcessor.DecodeOriented(rotated))
        using (var cropped = SKBitmap.Decode(ImageProcessor.Process(rotated, options with { CropVertical = 0 }).Bytes))
            Assert(cropped.Width == 40 && cropped.Height == 40 && Near(cropped.GetPixel(8, 8), normalized.GetPixel(8, 8)),
                "Crop coordinates follow the EXIF-normalized image orientation");
        Assert(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "Cropping preserves the original file byte-for-byte");
        AssertThrows(() => (options with { Crop = (CropAspect)99 }).Validate(), "Unknown crop modes are rejected");
        AssertThrows(() => (options with { Crop = CropAspect.Custom, CropWidth = 0 }).Validate(), "A zero custom crop ratio is rejected");
        AssertThrows(() => (options with { CropHorizontal = 101 }).Validate(), "Out-of-range crop positions are rejected");
    }

    private static void TestFileNaming(string directory, string source)
    {
        var options = new ProcessingOptions { Format = OutputFormat.Png, MaxWidth = 120, MaxHeight = 120 };
        var template = new FileNamingOptions { Mode = FileNamingMode.Template, Template = "{sku}-{n:000}-{width}x{height}", Sku = "ÁO", StartNumber = 7 };
        Assert(template.CreateStem(source, options, 0, 120, 72) == "ÁO-007-120x72", "Filename templates combine SKU, padded sequence and actual output dimensions");
        Assert(template.CreateStem(source, options, 4, 120, 72) == "ÁO-011-120x72", "Sequence numbers follow source order from the chosen starting number");
        Assert((template with { Template = "{name}-{n}" }).CreateStem("áo.hè.jpg", options, 1, 120, 72) == "áo.hè-8", "Templates preserve Unicode source basenames and literal dots");
        Assert(new FileNamingOptions().CreateStem("mẫu.jpg", options, 0, 120, 72) == "mẫu-pixelbatch", "The original naming mode keeps the existing suffix behavior");
        var replace = new FileNamingOptions { Mode = FileNamingMode.Replace, Find = "IMG_", Replacement = "SP-" };
        Assert(replace.CreateStem("img_001.jpg", options, 0, 120, 72) == "SP-001", "Find-and-replace naming is case-insensitive and does not add the original-mode suffix");
        Assert((replace with { Replacement = "" }).CreateStem("IMG_001.jpg", options, 0, 120, 72) == "001", "An empty replacement removes the matching part of the name");
        foreach (string invalid in new[] { "{unknown}", "{n:0000000}", "{name", "..\\{name}", "bad:{n}" })
            AssertThrows(() => (template with { Template = invalid }).Validate(), $"Invalid filename template is rejected: {invalid}");
        AssertThrows(() => (template with { Sku = "" }).Validate(), "SKU is required when its token is used");
        AssertThrows(() => (template with { StartNumber = 0 }).Validate(), "A zero sequence start is rejected");
        AssertThrows(() => (replace with { Find = "" }).Validate(), "Find-and-replace requires a search value");
        foreach (string invalid in new[] { "CON", "LPT1.photo", "trailing.", "trailing ", "..\\escape" })
            AssertThrows(() => (template with { Template = invalid }).CreateStem(source, options, 0, 120, 72), $"Unsafe or Windows-reserved output name is rejected: {invalid}");
        string output = Path.Combine(directory, "renamed");
        byte[] hash = SHA256.HashData(File.ReadAllBytes(source));
        var result = BatchExporter.ExportOne(source, output, options, naming: template);
        var repeated = BatchExporter.ExportOne(source, output, options, naming: template);
        Assert(result.Error is null && Path.GetFileName(result.Destination) == "ÁO-007-120x72.png"
            && repeated.Error is null && Path.GetFileName(repeated.Destination) == "ÁO-007-120x72 (2).png", "Templated exports use collision-safe filenames on disk");
        Assert(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "Batch renaming changes output names while preserving source bytes and names");
        var settings = new BatchSettings { Options = options with { Crop = CropAspect.Portrait4x5, CropHorizontal = 20, CropVertical = 80 }, Naming = template };
        var store = new PresetStore(Path.Combine(directory, "advanced-presets.json"));
        store.Save("Cắt và đặt tên", settings);
        var restored = store.Load().Single().Settings;
        Assert(restored.Options == settings.Options && restored.Naming == template, "Personal presets retain crop positions and advanced naming settings");
        var legacy = JsonSerializer.Deserialize<BatchSettings>("{\"Options\":{\"Quality\":75},\"Zip\":true}")!;
        legacy.Validate();
        Assert(legacy.Options.Crop == CropAspect.None && legacy.Naming.Mode == FileNamingMode.Original && legacy.Options.Quality == 75 && legacy.Zip,
            "Existing settings without crop or naming fields keep their previous behavior");
    }
}
