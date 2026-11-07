using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PixelBatch.Core;
using ImageResizeMode = PixelBatch.Core.ResizeMode;

namespace PixelBatch;

public partial class MainWindow
{
    private async Task RunUtilityUiChecksAsync(string directory, Action<bool, string> Check)
    {
        var options = new ProcessingOptions { Format = OutputFormat.Png, MaxWidth = 400, MaxHeight = 300,
            Rotation = ImageRotation.Clockwise90, FlipHorizontal = true, Watermark = WatermarkKind.Text,
            RepeatWatermark = true, WatermarkText = "© PixelBatch Studio", WatermarkAngle = -35, WatermarkSpacing = 125,
            WatermarkSize = 7, Opacity = 35 };
        var settings = new BatchSettings { Options = options,
            Naming = new FileNamingOptions { Mode = FileNamingMode.Template, Template = "SP-{n:000}-{width}x{height}", StartNumber = 7 } };
        FileList.SelectedItem = Files[0];
        ApplyBatchSettings(settings);
        CompareTab.IsChecked = true; FitZoomOption.IsChecked = true;
        await _previewTask;
        Check(ReadOptions() == options && RepeatWatermarkPanel.Visibility == Visibility.Visible && PositionCombo.Visibility == Visibility.Collapsed,
            "Transform and repeated watermark controls restore and read every setting correctly");
        var expected = ImageProcessor.CalculateSize(Files[0].Info!.Width, Files[0].Info!.Height, options);
        Check(_resultPreview is not null && _resultPreview.PixelWidth == expected.Width && _resultPreview.PixelHeight == expected.Height
            && OutputNameLabel.Text == $"SP-007-{expected.Width}x{expected.Height}.png", "Preview dimensions and filename tokens follow user rotation");
        Check(_comparisonPreview is not null && _comparisonPreview.PixelWidth == _resultPreview!.PixelWidth
            && _comparisonPreview.PixelHeight == _resultPreview.PixelHeight && !PreviewPixels(_comparisonPreview).SequenceEqual(PreviewPixels(_resultPreview)),
            "Transformed comparison aligns both layers and shows repeated watermark only on the result");
        byte[] before = PreviewPixels(_resultPreview!);
        WatermarkAngleSlider.Value = 25; WatermarkSpacingSlider.Value = 200;
        await _previewTask;
        Check(ReadOptions().WatermarkAngle == 25 && ReadOptions().WatermarkSpacing == 200 && !before.SequenceEqual(PreviewPixels(_resultPreview!)),
            "Angle and spacing controls update the real encoded preview");
        RepeatWatermarkCheck.IsChecked = false;
        await _previewTask;
        Check(PositionCombo.Visibility == Visibility.Visible && RepeatWatermarkPanel.Visibility == Visibility.Collapsed,
            "Single watermark mode restores corner positioning controls");
        WatermarkCombo.SelectedIndex = (int)WatermarkKind.Image;
        Check(RepeatWatermarkCheck.Visibility == Visibility.Collapsed && PositionCombo.Visibility == Visibility.Visible,
            "Logo mode hides repeated text controls and keeps logo positioning");
        ApplyBatchSettings(settings);
        PresetNameInput.Text = "Xoay và watermark lặp";
        SavePreset_Click(this, new RoutedEventArgs());
        ApplyBatchSettings(new BatchSettings());
        LoadPersonalPresets();
        SavedPresetCombo.SelectedItem = SavedPresetCombo.Items.Cast<SavedPreset>().Single(preset => preset.Name == "Xoay và watermark lặp");
        ApplySavedPreset_Click(this, new RoutedEventArgs());
        await _previewTask;
        Check(ReadOptions() == options && ReadNamingOptions() == settings.Naming, "Saved presets restore rotation, flips, repeated watermark and filename settings through the UI");
        TransformExpander.IsExpanded = true;
        RotationCombo.BringIntoView();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        App.RenderElement(RootGrid, Path.Combine(directory, "rotate-flip.png"));
        RepeatWatermarkPanel.BringIntoView();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        App.RenderElement(RootGrid, Path.Combine(directory, "repeated-watermark.png"));
        double oldWidth = Width, oldHeight = Height;
        Width = 1100; Height = 760;
        RepeatWatermarkPanel.BringIntoView();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Check(RepeatWatermarkPanel.IsVisible && PreviewViewport.ActualHeight > 80 && RetryButton.ActualWidth > 50 && ExportButton.IsVisible,
            "New controls and retry action remain usable in the compact window");
        App.RenderElement(RootGrid, Path.Combine(directory, "utilities-compact.png"));
        Width = oldWidth; Height = oldHeight;
        await RunRetryUiChecksAsync(directory, Check);
        TransformExpander.IsExpanded = false;
        ResultTab.IsChecked = true; FitZoomOption.IsChecked = true;
        ApplyBatchSettings(new BatchSettings());
        OutputDirectoryInput.Text = Path.Combine(directory, "output");
        SettingsScroll.ScrollToTop();
        await _previewTask;
    }

    private async Task RunRetryUiChecksAsync(string directory, Action<bool, string> Check)
    {
        var originalItems = Files.ToArray();
        var hashes = originalItems.ToDictionary(item => item.FilePath, item => SHA256.HashData(File.ReadAllBytes(item.FilePath)));
        string output = Path.Combine(directory, "retry-output");
        Directory.CreateDirectory(output);
        string blocker = Path.Combine(output, "240x180");
        File.WriteAllText(blocker, "A file deliberately blocks this output-size directory.");
        var settings = new BatchSettings
        {
            Options = new ProcessingOptions { Format = OutputFormat.Png },
            MultipleSizes = true, Sizes = [new(160, 120), new(240, 180)],
            Naming = new FileNamingOptions { Mode = FileNamingMode.Template, Template = "retry-{n:000}", StartNumber = 7 }
        };
        try
        {
            ApplyBatchSettings(settings);
            OutputDirectoryInput.Text = output;
            await ExportAsync();
            Check(Files.All(item => item.ExportSlots.Count == 2 && item.ExportSlots.Count(slot => slot.Failed) == 1
                && item.Status == "Đã xuất 1/2 cỡ") && RetryButton.IsEnabled && RetryButton.Content.ToString() == "Thử lại lỗi (3)",
                "Partial multi-size export records exactly the failed slots and offers the correct retry count");
            var completed = Directory.GetFiles(Path.Combine(output, "160x120")).ToDictionary(path => path, path => SHA256.HashData(File.ReadAllBytes(path)));
            await ExportAsync(true);
            Check(Files.All(item => item.ExportSlots.Count(slot => slot.Failed) == 1)
                && Directory.GetFiles(Path.Combine(output, "160x120")).Length == 3,
                "A failed retry retains failure state and does not repeat any successful size");
            NameTemplateInput.Text = "{unknown}";
            await ExportAsync(true);
            Check(StatusLabel.Text.Contains("Chưa thể xuất") && Files.Sum(item => item.ExportSlots.Count(slot => slot.Failed)) == 3,
                "Retry preflight rejects invalid settings without discarding recorded failures");
            ApplyBatchSettings(settings with { Options = settings.Options with { Rotation = ImageRotation.Clockwise90,
                Watermark = WatermarkKind.Text, RepeatWatermark = true, WatermarkText = "© Studio", WatermarkSize = 6, Opacity = 40 },
                Sizes = [new(72, 72)], Zip = true });
            RemoveFile_Click(new Button { Tag = originalItems[0] }, new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            QueuePreview();
            var retry = ExportAsync(true);
            Check(_isExporting && !RetryButton.IsEnabled && !SettingsPanel.IsEnabled, "Retry prevents a second export or setting changes while running");
            Cancel_Click(this, new RoutedEventArgs());
            await retry;
            Check(Files.Sum(item => item.ExportSlots.Count(slot => slot.Failed)) == 2 && RetryButton.IsEnabled
                && Files.All(item => item.Status is not "Đang xử lý…" and not "Chờ xuất"),
                "Cancelling a retry preserves failed jobs and restores queue interactivity");
            await _previewTask;
            App.RenderElement(RootGrid, Path.Combine(directory, "retry-failures.png"));
            File.Delete(blocker);
            await ExportAsync(true);
            await _previewTask;
            Check(Files.All(item => item.ExportSlots.All(slot => !slot.Failed) && item.Status == "Đã xuất 2/2 cỡ") && !RetryButton.IsEnabled,
                "Successful retry clears only recovered failures and retains earlier successful results");
            string[] recovered = Directory.GetFiles(Path.Combine(output, "240x180"));
            Check(recovered.Length == 2 && recovered.Select(Path.GetFileName).Order().SequenceEqual(new[] { "retry-008.png", "retry-009.png" }),
                "Retry preserves original source numbering after an earlier queue row is removed");
            Check(!Directory.Exists(Path.Combine(output, "72x72")) && recovered.All(path => ImageProcessor.Inspect(path) is { Width: <= 240, Height: <= 180 }),
                "Retry retains failed multi-size bounds even when the current output-size list changes");
            Check(completed.All(pair => pair.Value.SequenceEqual(SHA256.HashData(File.ReadAllBytes(pair.Key))))
                && Directory.GetFiles(Path.Combine(output, "160x120")).Length == 3,
                "Retry leaves successful files byte-for-byte unchanged and creates no duplicate sizes");
            var failedSlot = Files[0].ExportSlots.Single(slot => slot.Size == new OutputSize(240, 180));
            var expected = ImageProcessor.Process(Files[0].FilePath, failedSlot.GetVariant(ReadOptions()).Options);
            Check(File.ReadAllBytes(failedSlot.Result!.Destination!).SequenceEqual(expected.Bytes),
                "Recovered output uses current rotation and repeated watermark settings");
            using (var archive = ZipFile.OpenRead(Directory.GetFiles(output, "*.zip").Single()))
                Check(archive.Entries.Count == 2 && archive.Entries.All(entry => entry.FullName.StartsWith("240x180/", StringComparison.Ordinal)),
                    "Retry ZIP contains only outputs recovered in this run with their size folders");
            int count = Directory.GetFiles(output, "*", SearchOption.AllDirectories).Length;
            await ExportAsync(true);
            Check(Directory.GetFiles(output, "*", SearchOption.AllDirectories).Length == count, "Retry with no failed jobs writes nothing");
            App.RenderElement(RootGrid, Path.Combine(directory, "retry-recovered.png"));
            ApplyBatchSettings(new BatchSettings { Options = new ProcessingOptions { Format = OutputFormat.Png, MaxWidth = 120, MaxHeight = 120 } });
            OutputDirectoryInput.Text = Path.Combine(directory, "retry-new-batch");
            await ExportAsync();
            Check(Files.All(item => item.ExportSlots.Count == 1 && item.Status == "Đã xuất") && !RetryButton.IsEnabled,
                "A fresh full export starts a new tracking set instead of reusing old failed sizes");
            string badDirectory = Path.Combine(directory, "retry-root-blocker");
            File.WriteAllText(badDirectory, "This path cannot be used as an output directory.");
            OutputDirectoryInput.Text = badDirectory;
            await ExportAsync();
            Check(Files.All(item => item.ExportSlots.Single().Failed) && RetryButton.IsEnabled,
                "An output directory failure is recorded per file and can be retried");
            OutputDirectoryInput.Text = Path.Combine(directory, "retry-new-directory");
            await ExportAsync(true);
            Check(Files.All(item => item.Status == "Đã xuất" && item.LastOutput!.StartsWith(OutputDirectoryInput.Text, StringComparison.OrdinalIgnoreCase)),
                "Retry can recover a directory error using the newly chosen destination");
            Check(!Directory.GetFiles(output, ".pixelbatch-*.tmp", SearchOption.AllDirectories).Any()
                && hashes.All(pair => pair.Value.SequenceEqual(SHA256.HashData(File.ReadAllBytes(pair.Key)))),
                "Retry and cancellation leave no temporary output and never modify source files");
        }
        finally
        {
            _previewCancellation?.Cancel();
            Files.Clear();
            foreach (var item in originalItems)
            {
                item.ExportSlots.Clear(); item.LastOutput = null; item.SetStatus("Sẵn sàng"); Files.Add(item);
            }
            FileList.SelectedItem = Files[0]; UpdateCounters();
        }
    }
}
