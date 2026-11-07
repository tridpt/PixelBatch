using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Threading;
using PixelBatch.Core;
using SkiaSharp;

namespace PixelBatch;

public partial class MainWindow
{
    private async Task RunFolderUiChecksAsync(string directory, Action<bool, string> Check)
    {
        var originalItems = Files.ToArray();
        var previousSettings = ReadBatchSettings();
        string previousOutput = OutputDirectoryInput.Text;
        bool previousRecursive = IncludeSubfoldersCheck.IsChecked == true;
        double previousWidth = Width, previousHeight = Height;
        string root = Path.Combine(directory, "folder-inputs", "Kho A", "Sản phẩm");
        string other = Path.Combine(directory, "folder-inputs", "Kho B", "Sản phẩm");
        string output = Path.Combine(root, "Ket-qua");
        string[] paths = [Path.Combine(root, "bia.png"), Path.Combine(root, "Áo", "Xanh", "anh.png"),
            Path.Combine(root, "Quần", "anh.png"), Path.Combine(other, "anh.png")];
        foreach (string path in paths) Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var bitmap = new SKBitmap(120, 80))
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColor.Parse("#206B60"));
            using var paint = new SKPaint { Color = SKColor.Parse("#F2D58B") };
            canvas.DrawCircle(45, 35, 24, paint);
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            foreach (string path in paths) File.WriteAllBytes(path, encoded.ToArray());
        }
        Directory.CreateDirectory(output);
        string oldOutput = Path.Combine(output, "anh-cu.png");
        File.Copy(paths[0], oldOutput);
        string badFile = Path.Combine(root, "Áo", "loi.jpg");
        File.WriteAllText(badFile, "This image is deliberately corrupt.");
        var hashes = paths.Append(badFile).Append(oldOutput).ToDictionary(path => path, path => SHA256.HashData(File.ReadAllBytes(path)));
        var settings = new BatchSettings { Options = new ProcessingOptions { Format = OutputFormat.Png, Suffix = "", MaxWidth = 80, MaxHeight = 80 },
            PreserveFolders = true, Zip = true };
        try
        {
            Clear_Click(this, new RoutedEventArgs());
            ApplyBatchSettings(settings); OutputDirectoryInput.Text = output;
            IncludeSubfoldersCheck.IsChecked = false;
            await ImportAsync([root]); await _previewTask;
            Check(Files.Count == 1 && Files[0].RelativeDirectory == "Sản phẩm", "Folder import without recursion queues direct images and records their source root");
            IncludeSubfoldersCheck.IsChecked = true;
            await ImportAsync([root, other, paths[1]]); await _previewTask;
            Check(Files.Count == 5 && Files.Count(item => item.CanProcess) == 4 && Files.Count(item => !item.CanProcess) == 1,
                "Recursive folder import adds descendants and another root while retaining corrupt-file feedback and skipping duplicates");
            Check(Files.All(item => item.FilePath != oldOutput) && StatusLabel.Text.Contains("Bỏ qua"),
                "Folder import skips old outputs inside the selected source and reports skipped entries");
            var selected = Files.Single(item => item.FilePath == paths[1]);
            FileList.SelectedItem = selected;
            await _previewTask;
            Check(selected.DisplayPath == Path.Combine("Sản phẩm", "Áo", "Xanh", "anh.png")
                && Files.Single(item => item.FilePath == paths[3]).RelativeDirectory == "Sản phẩm (2)",
                "Queue paths distinguish nested same-named images and separate same-named input roots");
            Check(OutputNameLabel.Text == Path.Combine("Sản phẩm", "Áo", "Xanh", "anh.png"),
                "The output-name preview includes the preserved relative source folders");
            await ExportAsync(); await _previewTask;
            Check(Files.Where(item => item.CanProcess).All(item => item.LastOutput == Path.Combine(output, item.RelativeDirectory, item.Name)
                && File.Exists(item.LastOutput)), "Single-size UI export preserves the selected root and every nested input branch");
            using (var archive = ZipFile.OpenRead(Directory.GetFiles(output, "*.zip").Single()))
                Check(archive.Entries.Count == 4 && archive.Entries.Any(entry => entry.FullName == "Sản phẩm/Áo/Xanh/anh.png")
                    && archive.Entries.Any(entry => entry.FullName == "Sản phẩm (2)/anh.png") && archive.Entries.All(entry => !entry.FullName.Contains("anh-cu")),
                    "ZIP preserves nested Unicode folders and contains only this run's outputs");
            PresetNameInput.Text = "Cả cây thư mục";
            SavePreset_Click(this, new RoutedEventArgs());
            ApplyBatchSettings(new BatchSettings()); LoadPersonalPresets();
            SavedPresetCombo.SelectedItem = SavedPresetCombo.Items.Cast<SavedPreset>().Single(preset => preset.Name == "Cả cây thư mục");
            ApplySavedPreset_Click(this, new RoutedEventArgs()); await _previewTask;
            Check(ReadBatchSettings().PreserveFolders && PreserveFoldersCheck.IsChecked == true,
                "A saved preset restores folder preservation through the settings UI");
            NamingExpander.IsExpanded = true;
            FileList.ScrollIntoView(selected);
            PreserveFoldersCheck.BringIntoView();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            App.RenderElement(RootGrid, Path.Combine(directory, "source-folders.png"));
            Width = 1100; Height = 760;
            PreserveFoldersCheck.BringIntoView();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var headerPoint = IncludeSubfoldersCheck.TranslatePoint(new Point(0, 0), RootGrid);
            Check(IncludeSubfoldersCheck.IsVisible && PreserveFoldersCheck.IsVisible && headerPoint.Y >= 48
                && headerPoint.Y + IncludeSubfoldersCheck.ActualHeight <= 148 && ExportButton.IsVisible,
                "Folder import and export options remain visible and fit the compact window header");
            App.RenderElement(RootGrid, Path.Combine(directory, "source-folders-compact.png"));
            Width = previousWidth; Height = previousHeight;

            ApplyBatchSettings(settings with { PreserveFolders = false, Zip = false });
            OutputDirectoryInput.Text = Path.Combine(directory, "folders-flat");
            await _previewTask;
            Check(OutputNameLabel.Text == "anh.png", "Turning preservation off returns the output-name preview to a flat filename");
            await ExportAsync(); await _previewTask;
            Check(Directory.GetFiles(OutputDirectoryInput.Text, "*.png").Length == 4 && !Directory.GetDirectories(OutputDirectoryInput.Text).Any()
                && Files.Where(item => item.CanProcess).Select(item => item.LastOutput).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 4,
                "Flat folder export safely numbers collisions between same-named images from different branches");

            var multi = settings with { MultipleSizes = true, Sizes = [new(32, 32), new(64, 64)] };
            ApplyBatchSettings(multi);
            string multiOutput = Path.Combine(directory, "folders-multiple");
            OutputDirectoryInput.Text = multiOutput;
            string blocker = Path.Combine(multiOutput, "64x64", selected.RelativeDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(blocker)!);
            File.WriteAllText(blocker, "This blocks exactly one size of one nested source.");
            PreviewSizeCombo.SelectedIndex = 1; await _previewTask;
            Check(OutputNameLabel.Text == Path.Combine("64x64", selected.RelativeDirectory, "anh.png"),
                "Multiple-size filename preview includes the size folder before the preserved source tree");
            await ExportAsync(); await _previewTask;
            Check(Files.Sum(item => item.ExportSlots.Count(slot => slot.Failed)) == 1 && selected.Status == "Đã xuất 1/2 cỡ"
                && RetryButton.IsEnabled, "A blocked nested output marks only the affected source-size slot as failed");
            var successful = Files.SelectMany(item => item.ExportSlots).Where(slot => slot.Result is { Error: null })
                .Select(slot => slot.Result!.Destination!).ToDictionary(path => path, path => SHA256.HashData(File.ReadAllBytes(path)));
            var zipBefore = Directory.GetFiles(multiOutput, "*.zip");
            File.Delete(blocker);
            await ExportAsync(true); await _previewTask;
            Check(!RetryButton.IsEnabled && Files.Where(item => item.CanProcess).All(item => item.Status == "Đã xuất 2/2 cỡ")
                && selected.ExportSlots.Single(slot => slot.Size == new OutputSize(64, 64)).Result!.Destination == Path.Combine(blocker, "anh.png"),
                "Retry restores the failed nested variant to the same source branch and size folder");
            Check(successful.Count == 7 && successful.All(pair => pair.Value.SequenceEqual(SHA256.HashData(File.ReadAllBytes(pair.Key))))
                && Directory.GetFiles(multiOutput, "*.png", SearchOption.AllDirectories).Length == 8,
                "Nested retries preserve every successful variant without creating duplicate files");
            string retryZip = Directory.GetFiles(multiOutput, "*.zip").Except(zipBefore).Single();
            using (var archive = ZipFile.OpenRead(retryZip))
                Check(archive.Entries.Count == 1 && archive.Entries[0].FullName == "64x64/Sản phẩm/Áo/Xanh/anh.png",
                    "Retry ZIP contains only the recovered nested image with its complete relative layout");
            int countBeforeCancel = Files.Count;
            var import = ImportAsync([root]);
            Check(_isImporting && !IncludeSubfoldersCheck.IsEnabled && CancelButton.Visibility == Visibility.Visible
                && !ExportButton.IsEnabled, "Folder discovery exposes Stop and disables competing import and export actions");
            Cancel_Click(this, new RoutedEventArgs());
            await import; await _previewTask;
            Check(!_isImporting && IncludeSubfoldersCheck.IsEnabled && ExportButton.IsEnabled && !BatchProgress.IsIndeterminate
                && Files.Count == countBeforeCancel && StatusLabel.Text.Contains("Đã dừng thêm ảnh"),
                "Cancelling folder discovery retains queued images and restores interactivity without leaving indeterminate progress");
            Check(hashes.All(pair => pair.Value.SequenceEqual(SHA256.HashData(File.ReadAllBytes(pair.Key))))
                && !Directory.GetFiles(multiOutput, ".pixelbatch-*.tmp", SearchOption.AllDirectories).Any(),
                "Folder import, export, ZIP and retry preserve original images and leave no temporary files");
            FileList.SelectedItem = selected;
            FileList.ScrollIntoView(selected);
            NamingExpander.IsExpanded = true; PreserveFoldersCheck.BringIntoView();
            await _previewTask;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            App.RenderElement(RootGrid, Path.Combine(directory, "folders-multiple.png"));
        }
        finally
        {
            Clear_Click(this, new RoutedEventArgs());
            foreach (var item in originalItems) Files.Add(item);
            FileList.SelectedItem = Files.FirstOrDefault();
            Width = previousWidth; Height = previousHeight;
            IncludeSubfoldersCheck.IsChecked = previousRecursive;
            ApplyBatchSettings(previousSettings); OutputDirectoryInput.Text = previousOutput;
            NamingExpander.IsExpanded = false; SettingsScroll.ScrollToTop();
            UpdateCounters(); await _previewTask;
        }
    }
}
