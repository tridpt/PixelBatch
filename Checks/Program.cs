using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PixelBatch.Core;
using SkiaSharp;

namespace PixelBatch.Checks;

internal static partial class Program
{
    private static readonly List<string> Passed = [];
    [STAThread]
    private static int Main(string[] args)
    {
        string directory = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "qa", "core-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(directory);
        try
        {
            Run(directory);
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(new { passed = true, count = Passed.Count, checks = Passed }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"PASS: {Passed.Count} checks. Report: {Path.Combine(directory, "report.json")}");
            return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(new { passed = false, checks = Passed, error = error.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
            Console.Error.WriteLine(error); return 1;
        }
    }

    private static void Assert(bool value, string description)
    {
        if (!value) throw new InvalidOperationException("FAIL: " + description);
        Passed.Add(description);
    }

    private static void Run(string directory)
    {
        string source = Path.Combine(directory, "transparent.png");
        using (var bitmap = new SKBitmap(500, 300, SKColorType.Rgba8888, SKAlphaType.Premul))
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Color = SKColors.Coral, IsAntialias = true };
            canvas.DrawRoundRect(80, 50, 340, 200, 25, 25, paint);
            Save(bitmap, source, SKEncodedImageFormat.Png);
        }
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(source));
        var baseOptions = new ProcessingOptions { Format = OutputFormat.Png, Resize = ResizeMode.Original };
        Assert(ImageProcessor.Inspect(source) is { Width: 500, Height: 300 }, "Source dimensions are detected");
        Assert(ImageProcessor.CalculateSize(500, 300, new ProcessingOptions { MaxWidth = 200, MaxHeight = 200 }) == (200, 120), "Fit preserves aspect ratio");
        Assert(ImageProcessor.CalculateSize(500, 300, new ProcessingOptions { MaxWidth = 1000, MaxHeight = 60 }) == (100, 60), "Height limit is respected");
        Assert(ImageProcessor.CalculateSize(500, 300, new ProcessingOptions { MaxWidth = 2000, MaxHeight = 2000 }) == (500, 300), "Small images are not enlarged by default");
        Assert(ImageProcessor.CalculateSize(500, 300, new ProcessingOptions { MaxWidth = 1000, MaxHeight = 600, AllowUpscale = true }) == (1000, 600), "Explicit upscale is available");
        Assert(ImageProcessor.CalculateSize(500, 300, new ProcessingOptions { Resize = ResizeMode.Percentage, Percentage = 40 }) == (200, 120), "Percentage resize is proportional");
        Assert(ImageProcessor.CalculateSize(500, 300, new ProcessingOptions { Resize = ResizeMode.Percentage, Percentage = 200 }) == (500, 300), "Percentage resize respects no-upscale option");
        Assert(ImageProcessor.CalculateSize(500, 300, new ProcessingOptions { Resize = ResizeMode.Percentage, Percentage = 200, AllowUpscale = true }) == (1000, 600), "Percentage upscale is explicit");
        var png = ImageProcessor.Process(source, baseOptions);
        using (var decoded = SKBitmap.Decode(png.Bytes)) Assert(decoded.GetPixel(0, 0).Alpha == 0 && decoded.GetPixel(250, 150).Alpha == 255, "PNG retains transparency");
        var whiteJpeg = ImageProcessor.Process(source, baseOptions with { Format = OutputFormat.Jpeg, Quality = 95 });
        using (var decoded = SKBitmap.Decode(whiteJpeg.Bytes)) Assert(Near(decoded.GetPixel(0, 0), SKColors.White), "JPEG replaces transparency with white");
        var blackJpeg = ImageProcessor.Process(source, baseOptions with { Format = OutputFormat.Jpeg, JpegBackground = "#000000" });
        using (var decoded = SKBitmap.Decode(blackJpeg.Bytes)) Assert(Near(decoded.GetPixel(0, 0), SKColors.Black), "JPEG background color is configurable");
        var webp = ImageProcessor.Process(source, baseOptions with { Format = OutputFormat.Webp });
        using (var decoded = SKBitmap.Decode(webp.Bytes)) Assert(decoded.GetPixel(0, 0).Alpha == 0 && decoded.Width == 500, "WebP retains alpha and dimensions");
        var resized = ImageProcessor.Process(source, new ProcessingOptions { Format = OutputFormat.Jpeg, MaxWidth = 200, MaxHeight = 200 });
        using (var decoded = SKBitmap.Decode(resized.Bytes)) Assert(decoded.Width == 200 && decoded.Height == 120, "Encoded dimensions match resize settings");
        var watermarked = ImageProcessor.Process(source, baseOptions with { Watermark = WatermarkKind.Text, WatermarkText = "Bản quyền tiếng Việt", WatermarkSize = 7, Opacity = 100 });
        using (var original = SKBitmap.Decode(png.Bytes))
        using (var marked = SKBitmap.Decode(watermarked.Bytes))
        {
            int changedBottom = 0, changedTop = 0;
            for (int y = 0; y < marked.Height; y++) for (int x = 0; x < marked.Width; x++)
                if (original.GetPixel(x, y) != marked.GetPixel(x, y)) { if (y > 150) changedBottom++; else changedTop++; }
            Assert(changedBottom > 30 && changedTop == 0, "Vietnamese text watermark appears only in the chosen corner");
        }
        var invisible = ImageProcessor.Process(source, baseOptions with { Watermark = WatermarkKind.Text, WatermarkText = "Hidden", Opacity = 0 });
        Assert(png.Bytes.SequenceEqual(invisible.Bytes), "Zero opacity leaves pixels unchanged");
        var logo = ImageProcessor.Process(source, baseOptions with { Watermark = WatermarkKind.Image, WatermarkImagePath = source, Position = WatermarkPosition.Center, WatermarkSize = 6, Opacity = 75 });
        Assert(!png.Bytes.SequenceEqual(logo.Bytes), "Image watermark changes the output");
        TestJpegOrientation(directory);
        string noisy = Path.Combine(directory, "noise.png");
        using (var bitmap = new SKBitmap(600, 360))
        {
            var random = new Random(13);
            for (int y = 0; y < 360; y++) for (int x = 0; x < 600; x++) bitmap.SetPixel(x, y, new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
            Save(bitmap, noisy, SKEncodedImageFormat.Png);
        }
        var low = ImageProcessor.Process(noisy, baseOptions with { Format = OutputFormat.Jpeg, Quality = 20 });
        var high = ImageProcessor.Process(noisy, baseOptions with { Format = OutputFormat.Jpeg, Quality = 95 });
        Assert(low.Bytes.Length < high.Bytes.Length * 0.7, "JPEG quality has a measurable effect on size");
        var lowWebp = ImageProcessor.Process(noisy, baseOptions with { Format = OutputFormat.Webp, Quality = 20 });
        var highWebp = ImageProcessor.Process(noisy, baseOptions with { Format = OutputFormat.Webp, Quality = 95 });
        Assert(lowWebp.Bytes.Length < highWebp.Bytes.Length, "WebP quality controls file size");
        TestTargetSize(directory, noisy, baseOptions, low, high, lowWebp, highWebp);
        TestMultipleSizes(directory, source);
        TestPresets(directory, source);
        TestCropping(directory, source);
        TestFileNaming(directory, source);
        TestTransforms(directory, source);
        TestRepeatedWatermark(directory);
        TestFolders(directory);
        var first = BatchExporter.ExportOne(source, directory, baseOptions with { Suffix = "" });
        var second = BatchExporter.ExportOne(source, directory, baseOptions with { Suffix = "" });
        Assert(first.Error is null && second.Error is null && first.Destination != source && second.Destination != first.Destination, "Duplicate names are numbered without overwriting");
        Assert(originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "Original file remains byte-for-byte unchanged");
        Assert(!Directory.GetFiles(directory, ".pixelbatch-*.tmp").Any(), "Successful exports leave no temporary files");
        string corrupt = Path.Combine(directory, "broken.jpg"); File.WriteAllText(corrupt, "invalid");
        Assert(BatchExporter.ExportOne(corrupt, directory, baseOptions).Error is not null, "Corrupt files produce a per-file error");
        string blocker = Path.Combine(directory, "not-a-directory"); File.WriteAllText(blocker, "blocked");
        Assert(BatchExporter.ExportOne(source, blocker, baseOptions).Error is not null, "Unwritable output location produces an actionable error");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            bool stopped = false;
            try { BatchExporter.ExportOne(source, directory, baseOptions, cancelled.Token); } catch (OperationCanceledException) { stopped = true; }
            Assert(stopped, "Cancellation is observed before processing");
            try { BatchExporter.WriteUnique(directory, "cancelled", ".png", png.Bytes, cancelled.Token); } catch (OperationCanceledException) { }
            Assert(!Directory.GetFiles(directory, ".pixelbatch-*.tmp").Any() && !File.Exists(Path.Combine(directory, "cancelled.png")), "Cancellation cleans temporary output");
        }
        string zip = BatchExporter.CreateZip(directory, [first.Destination!, second.Destination!]);
        using (var archive = ZipFile.OpenRead(zip)) Assert(archive.Entries.Count == 2 && archive.Entries.All(e => e.Length > 0), "ZIP contains only requested nonempty outputs");
        AssertThrows(() => (baseOptions with { Suffix = "..\\escape" }).Validate(), "Output suffix cannot escape the destination folder");
        AssertThrows(() => new ProcessingOptions { MaxWidth = 0 }.Validate(), "Zero dimensions are rejected");
        AssertThrows(() => (baseOptions with { JpegBackground = "invalid" }).Validate(), "Invalid color values are rejected");
        AssertThrows(() => ImageProcessor.CalculateSize(10000, 10000, baseOptions), "Oversized output is rejected before allocation");
    }

    private static void TestTargetSize(string directory, string source, ProcessingOptions baseOptions,
        ProcessedImage lowJpeg, ProcessedImage highJpeg, ProcessedImage lowWebp, ProcessedImage highWebp)
    {
        foreach (var (format, low, high) in new[]
        {
            (OutputFormat.Jpeg, lowJpeg, highJpeg), (OutputFormat.Webp, lowWebp, highWebp)
        })
        {
            long target = (low.Bytes.LongLength + high.Bytes.LongLength) / 2;
            var options = baseOptions with { Format = format, Quality = 95, MinimumQuality = 20, TargetBytes = target };
            var processed = ImageProcessor.Process(source, options);
            Assert(processed.TargetMet && processed.Bytes.LongLength <= target, $"{format} target compression respects the actual byte limit");
            Assert(processed.Quality is >= 20 and < 95 && processed.Width == 600 && processed.Height == 360,
                $"{format} target compression stays within the quality range and keeps dimensions");
            var sameQuality = ImageProcessor.Process(source, options with { TargetBytes = 0, Quality = processed.Quality!.Value });
            Assert(sameQuality.Bytes.SequenceEqual(processed.Bytes), $"{format} target result reports its effective quality correctly");
            var generous = ImageProcessor.Process(source, options with { TargetBytes = 100 * 1024 * 1024 });
            Assert(generous.Quality == 95 && generous.Bytes.SequenceEqual(high.Bytes), $"{format} keeps maximum quality when the target already fits");
            var impossible = options with { TargetBytes = 1024, MinimumQuality = 90 };
            var unmet = ImageProcessor.Process(source, impossible);
            Assert(!unmet.TargetMet && unmet.Quality == 90, $"{format} reports an unattainable target at the chosen quality floor");
            string output = Path.Combine(directory, "unmet-" + format);
            var failed = BatchExporter.ExportOne(source, output, impossible);
            Assert(failed.Error is not null && failed.Destination is null && !Directory.Exists(output),
                $"{format} does not write an oversized result when a target cannot be met");
        }
        var png = ImageProcessor.Process(source, baseOptions);
        var limitedPng = ImageProcessor.Process(source, baseOptions with { TargetBytes = 1024 });
        Assert(!limitedPng.TargetMet && limitedPng.Quality is null && limitedPng.Bytes.SequenceEqual(png.Bytes),
            "PNG target checking keeps lossless pixels and reports an unmet limit");
        var equalQuality = ImageProcessor.Process(source, baseOptions with
            { Format = OutputFormat.Jpeg, TargetBytes = 1024, Quality = 95, MinimumQuality = 95 });
        Assert(!equalQuality.TargetMet && equalQuality.Quality == 95, "An equal quality floor and ceiling cannot silently lower quality");
        AssertThrows(() => (baseOptions with { TargetBytes = -1 }).Validate(), "Negative target size is rejected");
        AssertThrows(() => (baseOptions with { TargetBytes = 1023 }).Validate(), "A target below one KB is rejected");
        AssertThrows(() => (baseOptions with { TargetBytes = 104857601 }).Validate(), "A target over 100 MB is rejected");
        AssertThrows(() => (baseOptions with { Format = OutputFormat.Jpeg, TargetBytes = 2048, MinimumQuality = 90, Quality = 80 }).Validate(),
            "A quality floor above the ceiling is rejected before output");
    }

    private static void TestMultipleSizes(string directory, string source)
    {
        string output = Path.Combine(directory, "sizes");
        var settings = new BatchSettings
        {
            Options = new ProcessingOptions { Format = OutputFormat.Png, Resize = ResizeMode.Original },
            MultipleSizes = true, Sizes = [new(120, 120), new(300, 200)], Zip = true
        };
        var variants = settings.GetVariants();
        Assert(variants.Count == 2 && variants[0].Options.Resize == ResizeMode.Fit && variants[0].Folder == "120x120",
            "Multiple sizes produce separate fit-bounded variants");
        var results = variants.SelectMany(variant => Enumerable.Range(0, 2).Select(_ =>
            BatchExporter.ExportOne(source, Path.Combine(output, variant.Folder), variant.Options))).ToArray();
        Assert(results.All(result => result.Error is null) && results.Select(result => result.Destination).Distinct().Count() == 4,
            "Multiple sizes preserve collision safety for repeated exports");
        Assert(results[0] is { Width: 120, Height: 72 } && results[2] is { Width: 300, Height: 180 },
            "Every size respects its own bounding box and the source aspect ratio");
        string zip = BatchExporter.CreateZip(output, results.Select(result => result.Destination!).ToArray());
        using (var archive = ZipFile.OpenRead(zip))
            Assert(archive.Entries.Count == 4 && archive.Entries.Select(entry => entry.FullName).Distinct().Count() == 4
                && archive.Entries.Count(entry => entry.FullName.StartsWith("120x120/")) == 2
                && archive.Entries.Count(entry => entry.FullName.StartsWith("300x200/")) == 2,
                "ZIP preserves size folders and unique names instead of flattening the batch");
        AssertThrows(() => BatchExporter.CreateZip(output, [source]), "ZIP rejects files outside the export directory");
        Assert(!Directory.GetFiles(output, ".pixelbatch-*.tmp").Any(), "Failed ZIP creation cleans up its temporary archive");
        AssertThrows(() => (settings with { Sizes = [] }).Validate(), "Multiple-size export requires at least one size");
        AssertThrows(() => (settings with { Sizes = [new(100, 100), new(100, 100)] }).Validate(), "Duplicate output sizes are rejected");
        AssertThrows(() => (settings with { Sizes = [new(0, 100)] }).Validate(), "Invalid output size dimensions are rejected");
        AssertThrows(() => (settings with { Sizes = Enumerable.Range(1, 7).Select(index => new OutputSize(index * 100, 100)).ToArray() }).Validate(),
            "More than six simultaneous sizes are rejected");
        var single = (settings with { MultipleSizes = false }).GetVariants();
        Assert(single.Count == 1 && single[0].Folder == "" && single[0].Options.Resize == ResizeMode.Original,
            "Disabling multiple sizes restores the ordinary single-output settings");
    }

    private static void TestPresets(string directory, string source)
    {
        string path = Path.Combine(directory, "preferences", "presets.json");
        var store = new PresetStore(path);
        Assert(store.Load().Count == 0, "A new preset library starts empty");
        var settings = new BatchSettings
        {
            Options = new ProcessingOptions { TargetBytes = 200 * 1024, MinimumQuality = 35,
                Watermark = WatermarkKind.Text, WatermarkText = "© Cửa hàng của tôi", Suffix = "-shop" },
            MultipleSizes = true, Sizes = [new(800, 800), new(1600, 1200)], Zip = true
        };
        store.Save("  Ảnh cửa hàng  ", settings);
        var restored = new PresetStore(path).Load().Single();
        Assert(restored.Name == "Ảnh cửa hàng" && restored.Settings.Options == settings.Options
            && restored.Settings.Sizes.SequenceEqual(settings.Sizes) && restored.Settings.MultipleSizes && restored.Settings.Zip,
            "Named presets survive a fresh load with target, watermark, sizes and ZIP settings");
        store.Save("ảnh cửa hàng", settings with { Options = settings.Options with { TargetBytes = 100 * 1024 } });
        Assert(store.Load() is { Count: 1 } updated && updated[0].Settings.Options.TargetBytes == 100 * 1024,
            "Saving the same preset name updates it without a case-sensitive duplicate");
        store.Save("Khác", new BatchSettings());
        Assert(store.Delete("ảnh cửa hàng") is { Count: 1 } remaining && remaining[0].Name == "Khác",
            "Deleting a preset preserves the rest of the library");
        AssertThrows(() => store.Save("   ", settings), "Empty preset names are rejected");
        AssertThrows(() => store.Save(new string('a', 49), settings), "Overlong preset names are rejected");
        string logo = Path.Combine(directory, "preset-logo.png");
        File.Copy(source, logo);
        store.Save("Logo", settings with { Options = settings.Options with { Watermark = WatermarkKind.Image, WatermarkImagePath = logo } });
        File.Delete(logo);
        Assert(new PresetStore(path).Load().Any(preset => preset.Name == "Logo"),
            "A moved watermark logo does not erase the user's saved preset");
        File.WriteAllText(path, "{broken json");
        bool rejected = false;
        try { store.Save("New", settings); } catch (JsonException) { rejected = true; }
        Assert(rejected && File.ReadAllText(path) == "{broken json", "A corrupt preset library is reported and never silently overwritten");
        Assert(!Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Any(), "Preset writes leave no temporary files");
    }

    private static void TestJpegOrientation(string directory)
    {
        using var bitmap = new SKBitmap(80, 40);
        using (var canvas = new SKCanvas(bitmap))
        {
            SKColor[] colors = [SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Yellow];
            for (int index = 0; index < 4; index++)
            {
                using var paint = new SKPaint { Color = colors[index] };
                canvas.DrawRect(index % 2 * 40, index / 2 * 20, 40, 20, paint);
            }
        }
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        byte[] jpeg = data.ToArray();
        SKColor[] topLeft = [SKColors.Red, SKColors.Lime, SKColors.Yellow, SKColors.Blue, SKColors.Red, SKColors.Blue, SKColors.Yellow, SKColors.Lime];
        for (ushort orientation = 1; orientation <= 8; orientation++)
        {
            using var exif = new MemoryStream();
            using (var writer = new BinaryWriter(exif, Encoding.ASCII, true))
            {
                writer.Write(new byte[] { 69, 120, 105, 102, 0, 0, 73, 73 });
                writer.Write((ushort)42); writer.Write((uint)8); writer.Write((ushort)1);
                writer.Write((ushort)0x0112); writer.Write((ushort)3); writer.Write((uint)1);
                writer.Write(orientation); writer.Write((ushort)0); writer.Write((uint)0);
            }
            byte[] payload = exif.ToArray();
            using var output = new MemoryStream();
            output.Write(jpeg, 0, 2); output.WriteByte(0xFF); output.WriteByte(0xE1);
            int length = payload.Length + 2; output.WriteByte((byte)(length >> 8)); output.WriteByte((byte)length);
            output.Write(payload); output.Write(jpeg, 2, jpeg.Length - 2);
            string path = Path.Combine(directory, $"orientation-{orientation}.jpg"); File.WriteAllBytes(path, output.ToArray());
            using var decoded = ImageProcessor.DecodeOriented(path);
            Assert(decoded.Width == (orientation >= 5 ? 40 : 80) && decoded.Height == (orientation >= 5 ? 80 : 40)
                && Near(decoded.GetPixel(8, 8), topLeft[orientation - 1]), $"EXIF orientation {orientation} is normalized correctly");
            var normalized = ImageProcessor.Process(path, new ProcessingOptions { Format = OutputFormat.Jpeg, Resize = ResizeMode.Original });
            using var codec = SKCodec.Create(new SKMemoryStream(normalized.Bytes));
            Assert(codec.EncodedOrigin == SKEncodedOrigin.TopLeft, $"Output does not retain stale EXIF orientation {orientation}");
        }
    }

    private static bool Near(SKColor a, SKColor b) => Math.Abs(a.Red - b.Red) < 25 && Math.Abs(a.Green - b.Green) < 25 && Math.Abs(a.Blue - b.Blue) < 25;
    private static void Save(SKBitmap bitmap, string path, SKEncodedImageFormat format)
    {
        using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(format, 100); File.WriteAllBytes(path, encoded.ToArray());
    }
    private static void AssertThrows(Action action, string description)
    {
        bool failed = false; try { action(); } catch (ArgumentException) { failed = true; }
        Assert(failed, description);
    }
}
