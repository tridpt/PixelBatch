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
    private async Task RunFeatureUiChecksAsync(string directory, Action<bool, string> Check)
    {
        _presetStore = new PresetStore(Path.Combine(directory, "preferences", "presets.json"));
        LoadPersonalPresets();
        var settings = new BatchSettings
        {
            Options = new ProcessingOptions { Format = OutputFormat.Jpeg, Quality = 90, MinimumQuality = 30,
                TargetBytes = 200 * 1024, Watermark = WatermarkKind.Text, WatermarkText = "© Cửa hàng của tôi", WatermarkSize = 4 },
            MultipleSizes = true, Sizes = [new(800, 800), new(1600, 1600)], Zip = true
        };
        ApplyBatchSettings(settings);
        await _previewTask;
        Check(MultiSizePanel.Visibility == Visibility.Visible && SingleSizePanel.Visibility == Visibility.Collapsed
            && ExportButtonLabel.Text == "Xuất 6 tệp", "Multiple-size mode shows the size list and the correct output count");
        PreviewSizeCombo.SelectedIndex = 1;
        await _previewTask;
        Check(_previewSummary.StartsWith("1600x1600") && _previewTargetMet && PreviewTargetBadge.Visibility == Visibility.Visible,
            "Preview follows the chosen output size and reports the actual target outcome");
        PresetNameInput.Text = "Ảnh cửa hàng";
        SavePreset_Click(this, new RoutedEventArgs());
        Check(_presetStore.Load().Count == 1 && SavedPresetCombo.SelectedItem is SavedPreset,
            "Save preset writes a named configuration through the UI");
        ApplyBatchSettings(new BatchSettings());
        LoadPersonalPresets();
        SavedPresetCombo.SelectedIndex = 0;
        ApplySavedPreset_Click(this, new RoutedEventArgs());
        await _previewTask;
        var restored = ReadBatchSettings();
        Check(restored.Options == settings.Options && restored.MultipleSizes && restored.Zip && restored.Sizes.SequenceEqual(settings.Sizes),
            "A reloaded preset restores target size, quality, watermark, sizes and ZIP through the UI");
        TargetKbInput.Text = "150";
        SavePreset_Click(this, new RoutedEventArgs());
        Check(_presetStore.Load() is { Count: 1 } updated && updated[0].Settings.Options.TargetBytes == 150 * 1024,
            "Saving the selected name updates its settings without creating a duplicate");
        TargetKbInput.Text = "200";
        SizeWidthInput.Text = "1200"; SizeHeightInput.Text = "900";
        AddSize_Click(this, new RoutedEventArgs());
        await _previewTask;
        Check(OutputSizes.Count == 3 && _previewSummary.StartsWith("1200x900"), "Adding a custom size selects it for a real preview");
        AddSize_Click(this, new RoutedEventArgs());
        Check(OutputSizes.Count == 3 && StatusLabel.Text.Contains("đã có"), "Duplicate custom sizes are rejected with visible feedback");
        RemoveSize_Click(new Button { Tag = new OutputSize(1200, 900) }, new RoutedEventArgs());
        PreviewSizeCombo.SelectedIndex = 1;
        await _previewTask;
        string output = Path.Combine(directory, "multi-output");
        OutputDirectoryInput.Text = output;
        var hashes = Files.ToDictionary(item => item.FilePath, item => SHA256.HashData(File.ReadAllBytes(item.FilePath)));
        var export = ExportAsync();
        await ExportAsync();
        await export;
        await _previewTask;
        string[] outputs = Directory.GetFiles(output, "*.jpg", SearchOption.AllDirectories);
        Check(outputs.Length == 6 && outputs.All(path => new FileInfo(path).Length <= 200 * 1024),
            "One batch writes all size variants within the target and ignores a duplicate export click");
        Check(Directory.GetFiles(Path.Combine(output, "800x800"), "*.jpg").Length == 3
            && Directory.GetFiles(Path.Combine(output, "1600x1600"), "*.jpg").Length == 3,
            "Each size is exported into its own folder");
        Check(Files.All(item => item.Status == "Đã xuất 2/2 cỡ" && item.StatusDetail.Contains("800x800") && item.StatusDetail.Contains("1600x1600")),
            "Queue results include every output size instead of only the last file");
        using (var zip = ZipFile.OpenRead(Directory.GetFiles(output, "*.zip").Single()))
            Check(zip.Entries.Count == 6 && zip.Entries.All(entry => entry.FullName.Contains('/'))
                && zip.Entries.Select(entry => entry.FullName).Distinct().Count() == 6,
                "The UI export ZIP preserves both size folders and all six outputs");
        Check(hashes.All(pair => pair.Value.SequenceEqual(SHA256.HashData(File.ReadAllBytes(pair.Key)))),
            "Target compression and multiple-size export leave every source unchanged");
        SettingsScroll.ScrollToVerticalOffset(190);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        App.RenderElement(RootGrid, Path.Combine(directory, "features.png"));
        SettingsScroll.ScrollToTop(); PersonalPresetsExpander.IsExpanded = true;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        App.RenderElement(RootGrid, Path.Combine(directory, "presets.png"));
        PersonalPresetsExpander.IsExpanded = false;
        double previousWidth = Width, previousHeight = Height;
        Width = 1100; Height = 760;
        SettingsScroll.ScrollToVerticalOffset(350);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Check(SettingsScroll.ScrollableHeight > 0 && ExportButton.ActualWidth >= 190 && PreviewImage.ActualHeight > 80,
            "New settings remain scrollable while preview and export stay visible in the compact window");
        App.RenderElement(RootGrid, Path.Combine(directory, "features-compact.png"));
        Width = previousWidth; Height = previousHeight;
        ApplyBatchSettings(new BatchSettings { Options = new ProcessingOptions
            { Format = OutputFormat.Jpeg, Quality = 95, MinimumQuality = 95, TargetBytes = 1024, Resize = ImageResizeMode.Original } });
        await _previewTask;
        Check(!_previewTargetMet && PreviewTargetBadge.Visibility == Visibility.Visible && PreviewTargetLabel.Text.Contains("Chưa đạt"),
            "An unattainable target has an explicit preview warning");
        OutputDirectoryInput.Text = Path.Combine(directory, "unmet-output");
        await ExportAsync(); await _previewTask;
        Check(Directory.GetFiles(OutputDirectoryInput.Text, "*", SearchOption.AllDirectories).Length == 0
            && Files.All(item => item.Status == "Lỗi xuất" && item.LastOutput is null) && StatusLabel.Text.Contains("3 lỗi"),
            "Unmet targets create no oversized files and report a separate error for every source");
        SettingsScroll.ScrollToTop();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        App.RenderElement(RootGrid, Path.Combine(directory, "target-unmet.png"));
        PngOption.IsChecked = true;
        await _previewTask;
        Check(!QualitySlider.IsEnabled && MinimumQualityPanel.Visibility == Visibility.Collapsed && ! _previewTargetMet,
            "PNG target mode explains the limit while disabling lossy quality controls");
        JpegOption.IsChecked = true;
        MinimumQualityInput.Text = "100";
        await ExportAsync();
        Check(StatusLabel.Text.Contains("Chưa thể xuất"), "A quality floor above the ceiling blocks export in the UI");
        SavedPresetCombo.SelectedIndex = 0;
        DeletePreset_Click(this, new RoutedEventArgs());
        Check(_presetStore.Load().Count == 0 && !ApplyPresetButton.IsEnabled, "Delete preset removes the selected saved entry");
        ApplyBatchSettings(new BatchSettings());
        OutputDirectoryInput.Text = Path.Combine(directory, "output");
        SettingsScroll.ScrollToTop();
        await _previewTask;
    }
}
