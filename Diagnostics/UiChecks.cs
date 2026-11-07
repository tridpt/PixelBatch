using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Threading;
using PixelBatch.Core;

namespace PixelBatch;

public partial class MainWindow
{
    internal async Task<IReadOnlyList<string>> RunUiChecksAsync(string directory)
    {
        var checks = new List<string>();
        void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            checks.Add(description);
        }
        Check(!ExportButton.IsEnabled && EmptyPreview.Visibility == Visibility.Visible, "Empty state has no export action");
        App.RenderElement(RootGrid, Path.Combine(directory, "empty.png"));
        await RunGettingStartedUiChecksAsync(directory, Check);
        string[] sources = await Task.Run(() => SampleArtwork.Create(Path.Combine(directory, "input"), includeLogo: true));
        var hashes = sources.ToDictionary(path => path, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        await ImportAsync(sources.Concat([sources[0], Path.Combine(directory, "not-an-image.txt")]));
        await _previewTask;
        Check(Files.Count == 3, "Import handles all three formats and skips duplicate paths");
        Check(ExportButton.IsEnabled && _resultPreview is not null, "Selection produces a real encoded preview");
        OriginalTab.IsChecked = true;
        Check(ReferenceEquals(PreviewImage.Source, _originalPreview), "Original preview tab displays source image");
        ResultTab.IsChecked = true;
        ApplyOptions(new ProcessingOptions { Quality = 78, MaxWidth = 1600, MaxHeight = 1600, Watermark = WatermarkKind.Text, WatermarkText = "© PixelBatch Studio", Opacity = 80, WatermarkSize = 4 });
        await _previewTask;
        Check(ReadOptions().Watermark == WatermarkKind.Text && WatermarkOptionsPanel.Visibility == Visibility.Visible, "Text watermark settings and preview are connected");
        OutputDirectoryInput.Text = Path.Combine(directory, "output");
        ZipCheck.IsChecked = true;
        await ExportAsync();
        await _previewTask;
        Check(Files.All(x => x.LastOutput is not null && File.Exists(x.LastOutput)), "Export writes an output for every valid source");
        Check(Files.All(x => x.Status == "Đã xuất"), "Queue shows success for completed images");
        Check(Files.All(x => ImageProcessor.Inspect(x.LastOutput!).Width <= 1600), "Export honors resize settings");
        string zip = Directory.GetFiles(OutputDirectoryInput.Text, "*.zip").Single();
        using (var archive = ZipFile.OpenRead(zip)) Check(archive.Entries.Count == 3, "ZIP contains exactly this batch's outputs");
        Check(hashes.All(pair => pair.Value == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pair.Key)))), "Source files remain byte-for-byte unchanged");
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        App.RenderElement(RootGrid, Path.Combine(directory, "workspace.png"));
        long countBefore = Directory.GetFiles(OutputDirectoryInput.Text).LongLength;
        WidthInput.Text = "0";
        await ExportAsync();
        Check(Directory.GetFiles(OutputDirectoryInput.Text).LongLength == countBefore && StatusLabel.Text.Contains("Chưa thể xuất"), "Invalid dimensions block output with a visible explanation");
        ApplyOptions(new ProcessingOptions { MaxWidth = 1600, MaxHeight = 1600, Watermark = WatermarkKind.Image, WatermarkImagePath = Path.Combine(directory, "input", "logo.png") });
        await _previewTask;
        Check(_resultPreview is not null && LogoButton.Visibility == Visibility.Visible, "Logo watermark has a working preview");
        await RunFeatureUiChecksAsync(directory, Check);
        await RunAdvancedUiChecksAsync(directory, Check);
        await RunUtilityUiChecksAsync(directory, Check);
        await RunFolderUiChecksAsync(directory, Check);
        string badFile = Path.Combine(directory, "input", "broken.jpg");
        File.WriteAllText(badFile, "not an image");
        await ImportAsync([badFile]); await _previewTask;
        Check(Files.Count == 4 && !Files.Last().CanProcess && Files.Last().Status == "Không đọc được", "Corrupt input is reported without discarding good images");
        Check(!RetryButton.IsEnabled, "Unreadable imports are excluded from export retries");
        Width = 1100; Height = 760;
        ApplyOptions(new ProcessingOptions()); await _previewTask;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        App.RenderElement(RootGrid, Path.Combine(directory, "compact.png"));
        Check(ExportButton.ActualWidth >= 190 && PreviewImage.ActualWidth > 100, "Compact window retains preview and export controls");
        ZipCheck.IsChecked = false;
        var export = ExportAsync();
        for (int attempt = 0; attempt < 100 && !_isExporting && !export.IsCompleted; attempt++) await Task.Delay(5);
        if (_isExporting) Cancel_Click(this, new RoutedEventArgs());
        await export;
        Check(Files.All(x => x.Status is not "Đang xử lý…" and not "Chờ xuất"), "Cancellation leaves no queue item stuck in progress");
        Check(Directory.GetFiles(OutputDirectoryInput.Text, ".pixelbatch-*.tmp").Length == 0, "No temporary files remain after cancellation");
        Clear_Click(this, new RoutedEventArgs());
        Check(Files.Count == 0 && !ExportButton.IsEnabled && EmptyPreview.Visibility == Visibility.Visible, "Clear resets the workspace without deleting files");
        return checks;
    }
}
