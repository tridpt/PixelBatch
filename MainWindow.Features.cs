using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using PixelBatch.Core;

namespace PixelBatch;

public partial class MainWindow
{
    public ObservableCollection<OutputSize> OutputSizes { get; } = [new(800, 800), new(1600, 1600), new(2400, 2400)];
    private PresetStore? _presetStore;
    private bool _previewHasTarget;
    private bool _previewTargetMet;

    internal BatchSettings ReadBatchSettings()
    {
        var settings = new BatchSettings
        {
            Options = ReadOptions(), MultipleSizes = MultiSizeCheck.IsChecked == true,
            Sizes = OutputSizes.ToArray(), Zip = ZipCheck.IsChecked == true, Naming = ReadNamingOptions(),
            PreserveFolders = PreserveFoldersCheck.IsChecked == true
        };
        settings.Validate();
        return settings;
    }

    internal void ApplyBatchSettings(BatchSettings settings)
    {
        settings.Validate(requireWatermarkFile: false);
        _updating = true;
        try
        {
            ApplyOptions(settings.Options);
            ApplyNamingOptions(settings.Naming);
            MultiSizeCheck.IsChecked = settings.MultipleSizes;
            OutputSizes.Clear();
            foreach (var size in settings.Sizes) OutputSizes.Add(size);
            PreviewSizeCombo.SelectedIndex = OutputSizes.Count > 0 ? 0 : -1;
            ZipCheck.IsChecked = settings.Zip;
            PreserveFoldersCheck.IsChecked = settings.PreserveFolders;
        }
        finally { _updating = false; }
        RefreshSettingsVisibility(); UpdateInteractivity(); QueuePreview();
    }

    private void AddSize_Click(object sender, RoutedEventArgs e)
    {
        if (_isExporting || _isImporting) return;
        try
        {
            if (!int.TryParse(SizeWidthInput.Text.Trim(), out int width) || !int.TryParse(SizeHeightInput.Text.Trim(), out int height))
                throw new ArgumentException("Chiều rộng và cao của cỡ mới cần là số nguyên.");
            var size = new OutputSize(width, height);
            size.Validate();
            if (OutputSizes.Contains(size)) throw new ArgumentException("Cỡ ảnh này đã có trong danh sách.");
            if (OutputSizes.Count >= 6) throw new ArgumentException("Mỗi lần xuất hỗ trợ tối đa 6 cỡ ảnh.");
            OutputSizes.Add(size);
            PreviewSizeCombo.SelectedItem = size;
            UpdateInteractivity(); QueuePreview();
            ShowStatus($"Đã thêm cỡ {size.Label} · Kết quả sẽ nằm trong thư mục {size.FolderName}.");
        }
        catch (ArgumentException exception) { ShowStatus(exception.Message, true); }
    }

    private void RemoveSize_Click(object sender, RoutedEventArgs e)
    {
        if (_isExporting || _isImporting || (sender as Button)?.Tag is not OutputSize size) return;
        OutputSizes.Remove(size);
        if (PreviewSizeCombo.SelectedIndex < 0 && OutputSizes.Count > 0) PreviewSizeCombo.SelectedIndex = 0;
        UpdateInteractivity(); QueuePreview();
    }

    private void LoadPersonalPresets()
    {
        if (_presetStore is null) return;
        try { RefreshPersonalPresets(_presetStore.Load()); }
        catch (Exception exception) when (IsPresetError(exception))
        { ShowStatus("Chưa đọc được preset cá nhân · " + exception.Message, true); }
    }

    private void RefreshPersonalPresets(IReadOnlyList<SavedPreset> presets, string? selectedName = null)
    {
        SavedPresetCombo.ItemsSource = presets;
        SavedPresetCombo.SelectedItem = presets.FirstOrDefault(preset => string.Equals(preset.Name, selectedName, StringComparison.OrdinalIgnoreCase));
        ApplyPresetButton.IsEnabled = DeletePresetButton.IsEnabled = SavedPresetCombo.SelectedItem is SavedPreset;
    }

    private void SavedPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        bool selected = SavedPresetCombo.SelectedItem is SavedPreset;
        ApplyPresetButton.IsEnabled = DeletePresetButton.IsEnabled = selected;
        if (SavedPresetCombo.SelectedItem is SavedPreset preset) PresetNameInput.Text = preset.Name;
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        if (_presetStore is null || _isExporting || _isImporting) return;
        try
        {
            var presets = _presetStore.Save(PresetNameInput.Text, ReadBatchSettings());
            RefreshPersonalPresets(presets, PresetNameInput.Text.Trim());
            ShowStatus($"Đã lưu preset “{PresetNameInput.Text.Trim()}”. Nhập cùng tên để cập nhật.");
        }
        catch (Exception exception) when (IsPresetError(exception))
        { ShowStatus("Chưa lưu được preset · " + exception.Message, true); }
    }

    private void ApplySavedPreset_Click(object sender, RoutedEventArgs e)
    {
        if (_isExporting || _isImporting || SavedPresetCombo.SelectedItem is not SavedPreset preset) return;
        try
        {
            ApplyBatchSettings(preset.Settings);
            ShowStatus($"Đã áp dụng preset “{preset.Name}”.");
        }
        catch (ArgumentException exception) { ShowStatus("Chưa áp dụng được preset · " + exception.Message, true); }
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (_presetStore is null || _isExporting || _isImporting || SavedPresetCombo.SelectedItem is not SavedPreset preset) return;
        try
        {
            RefreshPersonalPresets(_presetStore.Delete(preset.Name));
            PresetNameInput.Text = "";
            ShowStatus($"Đã xóa preset “{preset.Name}”.");
        }
        catch (Exception exception) when (IsPresetError(exception))
        { ShowStatus("Chưa xóa được preset · " + exception.Message, true); }
    }

    private static bool IsPresetError(Exception exception) => exception is IOException or InvalidDataException
        or JsonException or ArgumentException or UnauthorizedAccessException;
}
