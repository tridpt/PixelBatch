using System.IO;
using System.IO.Compression;
using System.Text.Json;
using PixelBatch.Core;
using SkiaSharp;

namespace PixelBatch.Checks;

internal static partial class Program
{
    private static void TestFolders(string directory)
    {
        string root = Path.Combine(directory, "folder-checks", "Shop", "Sản phẩm");
        string output = Path.Combine(root, "Ket-qua");
        string nested = Path.Combine(root, "Áo", "Mặt trước");
        Directory.CreateDirectory(nested);
        Directory.CreateDirectory(Path.Combine(root, "Quần"));
        Directory.CreateDirectory(output);
        string direct = Path.Combine(root, "bia.PNG");
        string first = Path.Combine(nested, "anh.png");
        string second = Path.Combine(root, "Quần", "anh.png");
        string oldOutput = Path.Combine(output, "cu.png");
        using (var bitmap = new SKBitmap(30, 20))
        {
            bitmap.Erase(SKColors.Coral);
            foreach (string path in new[] { direct, first, second, oldOutput }) Save(bitmap, path, SKEncodedImageFormat.Png);
        }
        File.WriteAllText(Path.Combine(root, "ghi-chu.txt"), "This is not an image.");
        var shallow = SourceScanner.Scan([root], [], false, output);
        Assert(shallow.Files.Count == 1 && shallow.Files[0].FilePath == direct && shallow.Files[0].RelativeDirectory == "Sản phẩm",
            "Shallow folder import keeps only direct images and records the selected root name");
        var recursive = SourceScanner.Scan([root], [], true, output);
        Assert(recursive.Files.Count == 3 && recursive.Files.Select(file => file.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(new[] { direct, first, second }),
            "Recursive import finds nested images, supports mixed-case extensions and ignores non-images");
        Assert(recursive.Files.Single(file => file.FilePath == first).RelativeDirectory == Path.Combine("Sản phẩm", "Áo", "Mặt trước")
            && recursive.Files.All(file => file.ImportRoot == root), "Folder metadata preserves Unicode and multiple nested levels relative to the selected root");
        Assert(recursive.SkippedDirectories == 1 && recursive.Files.All(file => file.FilePath != oldOutput),
            "Recursive import excludes the current output directory nested inside an input root");
        var caseOutput = SourceScanner.Scan([root], [], true, output.ToUpperInvariant());
        Assert(caseOutput.Files.Count == 3, "Output-folder exclusion is case-insensitive on Windows");
        var duplicate = SourceScanner.Scan([root, first, root, root.ToUpperInvariant()], recursive.Files, true, output);
        Assert(duplicate.Files.Count == 0 && duplicate.SkippedFiles > 0, "Existing images and overlapping input selections are deduplicated case-insensitively");
        var overlapping = SourceScanner.Scan([root, Path.Combine(root, "Áo"), first], [], true, output);
        Assert(overlapping.Files.Count == 3 && overlapping.Files.Single(file => file.FilePath == first).ImportRoot == root,
            "Overlapping folders retain the first import root instead of changing an already-queued source layout");
        var explicitFile = SourceScanner.Scan([first], [], true, output);
        Assert(explicitFile.Files.Single().ImportRoot == "" && explicitFile.Files.Single().RelativeDirectory == "",
            "Individually selected files do not inherit parent folders");
        string other = Path.Combine(directory, "folder-checks", "Other shop", "Sản phẩm");
        Directory.CreateDirectory(other);
        File.Copy(first, Path.Combine(other, "anh.png"));
        var otherScan = SourceScanner.Scan([other], recursive.Files, true);
        Assert(otherScan.Files.Single().RelativeDirectory == "Sản phẩm (2)", "Distinct input roots with the same name receive separate output aliases");
        var sameNameRoots = SourceScanner.Scan([root, other], [], true, output);
        Assert(sameNameRoots.Files.Count == 4 && sameNameRoots.Files.Single(file => file.ImportRoot == other).RelativeDirectory == "Sản phẩm (2)",
            "Root aliases are unique when several same-named folders are added together");
        var secondScan = SourceScanner.Scan([root], shallow.Files, true, output);
        Assert(secondScan.Files.Count == 2 && secondScan.Files.All(file => file.RelativeDirectory.StartsWith("Sản phẩm\\", StringComparison.Ordinal)),
            "Enabling recursion for a previously added root adds only its missing descendants and reuses the root alias");
        var sourceIsOutput = SourceScanner.Scan([output], [], true, output);
        Assert(sourceIsOutput.Files.Count == 1 && sourceIsOutput.Files[0].FilePath == oldOutput,
            "An explicitly selected output folder can itself be used as an input source");
        var missing = SourceScanner.Scan([Path.Combine(root, "missing") , first], [], true);
        Assert(missing.Files.Count == 1 && missing.SkippedFiles == 1, "One missing input does not stop other selected sources");
        var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        bool stopped = false;
        try { SourceScanner.Scan([root], [], true, output, cancelled.Token); } catch (OperationCanceledException) { stopped = true; }
        Assert(stopped, "Folder discovery respects cancellation before scanning");
        cancelled.Dispose();

        var almostFull = Enumerable.Range(0, 999).Select(number => new SourceFile(Path.Combine(directory, $"existing-{number}.png"))).ToArray();
        var limited = SourceScanner.Scan([root], almostFull, true, output);
        Assert(limited.Files.Count == 1 && limited.LimitReached, "Recursive discovery stops at the total queue limit without adding extra files");
        var full = SourceScanner.Scan([root], almostFull.Append(new SourceFile(Path.Combine(directory, "last.png"))).ToArray(), true);
        Assert(full.Files.Count == 0 && full.LimitReached, "A full queue skips discovery and reports its capacity limit");

        string link = Path.Combine(root, "loop");
        bool linked = false;
        try { Directory.CreateSymbolicLink(link, root); linked = true; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        if (!linked)
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe", Arguments = $"/d /c mklink /J \"{link}\" \"{root}\"", UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            });
            if (process is not null) { process.WaitForExit(); linked = process.ExitCode == 0; }
        }
        Assert(linked, "The folder test creates a real Windows directory link for loop handling");
        var noLoop = SourceScanner.Scan([root], [], true, output);
        Assert(noLoop.Files.Count == 3 && noLoop.SkippedDirectories == 2, "Directory links are skipped so recursive import cannot loop back into its own root");
        var linkRoot = SourceScanner.Scan([link, first], [], true);
        Assert(linkRoot.Files.Count == 1 && linkRoot.SkippedDirectories == 1, "A linked input root is skipped without blocking ordinary selected files");

        string destination = Path.Combine(directory, "folder-output");
        var options = new ProcessingOptions { Format = OutputFormat.Png, Resize = ResizeMode.Original, Suffix = "" };
        var outputs = new List<string>();
        foreach (var file in recursive.Files)
        {
            string relative = SourceLayout.OutputFolder("80x80", file.RelativeDirectory, true);
            string target = SourceLayout.ResolveOutput(destination, relative);
            var result = BatchExporter.ExportOne(file.FilePath, target, options);
            Assert(result.Error is null && result.Destination is not null && File.Exists(result.Destination),
                "Nested export writes a valid image at " + relative);
            outputs.Add(result.Destination!);
        }
        Assert(outputs.Count(path => Path.GetFileName(path) == "anh.png") == 2 && outputs.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3,
            "Same-named images in different input branches keep their names in separate output folders");
        string zip = BatchExporter.CreateZip(destination, outputs);
        using (var archive = ZipFile.OpenRead(zip))
            Assert(archive.Entries.Select(entry => entry.FullName).Order().SequenceEqual(outputs.Select(path => Path.GetRelativePath(destination, path).Replace('\\', '/')).Order()),
                "ZIP preserves the size folder, selected root name and every nested input branch");
        string flattened = SourceLayout.ResolveOutput(destination, SourceLayout.OutputFolder("120x120", recursive.Files[0].RelativeDirectory, false));
        Assert(flattened == Path.Combine(destination, "120x120"), "Turning folder preservation off retains the existing flat output layout");
        Assert(SourceLayout.OutputFolder("", "", true) == "", "Individual files keep the output root even with folder preservation enabled");
        Assert(SourceLayout.IsWithin(Path.Combine(destination, "sub"), destination)
            && !SourceLayout.IsWithin(destination + "-other", destination), "Folder containment distinguishes descendants from same-prefix sibling directories");
        foreach (string invalid in new[] { "..", @"..\escape", @"80x80\..\escape", @"C:\escape", @"\\server\share", @"/escape", @"folder\", @"folder\\sub", "bad:name" })
            AssertThrows(() => SourceLayout.ResolveOutput(destination, invalid), "Output layout rejects unsafe relative path: " + invalid);
        var presets = new PresetStore(Path.Combine(directory, "folder-presets.json"));
        presets.Save("Giữ cây thư mục", new BatchSettings { PreserveFolders = true });
        Assert(presets.Load().Single().Settings.PreserveFolders, "Personal presets save and restore the folder layout option");
        Assert(!JsonSerializer.Deserialize<BatchSettings>("{}")!.PreserveFolders, "Older presets retain flat export behavior by default");
    }
}
