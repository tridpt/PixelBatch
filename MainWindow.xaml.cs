using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PixelBatch.Core;
using SkiaSharp;
using ImageResizeMode = PixelBatch.Core.ResizeMode;

namespace PixelBatch;

public partial class MainWindow : Window
{
    public ObservableCollection<ImageItem> Files { get; } = [];
    public ICommand AddFilesCommand { get; }
    public ICommand ExportCommand { get; }
    private readonly SemaphoreSlim _previewGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _previewCancellation;
    private CancellationTokenSource? _exportCancellation;
    private CancellationTokenSource? _importCancellation;
    private bool _ready;
    private bool _updating;
    private bool _isImporting;
    private bool _isExporting;
    private bool _closeAfterWork;
    private string _logoPath = "";
    private ImageItem? _selected;
    private BitmapSource? _originalPreview;
    private BitmapSource? _resultPreview;
    private string _previewSummary = "";
    private long _previewBytes;
    private Task _previewTask = Task.CompletedTask;
    private static readonly string PreferencesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelBatch", "settings.json");
    private const string ImageFilter = "Ảnh được hỗ trợ|*.jpg;*.jpeg;*.png;*.webp;*.bmp|Tất cả tệp|*.*";
    private readonly bool _diagnosticMode;

    public MainWindow(bool diagnosticMode = false)
    {
        _diagnosticMode = diagnosticMode;
        AddFilesCommand = new ActionCommand(AddFiles, () => !_isExporting && !_isImporting && !_guideOpen);
        ExportCommand = new ActionCommand(() => _ = ExportAsync(), () => !_isExporting && !_isImporting && !_guideOpen && Files.Any(x => x.CanProcess));
        ShowGuideCommand = new ActionCommand(ShowGuide, () => !_isExporting && !_isImporting);
        InitializeComponent();
        DataContext = this;
        OutputDirectoryInput.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PixelBatch");
        if (!diagnosticMode) RestorePreferences();
        _ready = true;
        if (!diagnosticMode)
        {
            _gettingStartedStore = new GettingStartedStore(Path.Combine(Path.GetDirectoryName(PreferencesPath)!, "getting-started.json"));
            _presetStore = new PresetStore(Path.Combine(Path.GetDirectoryName(PreferencesPath)!, "presets.json"));
            LoadPersonalPresets();
        }
        RefreshSettingsVisibility();
        UpdateCounters();
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e) => AddFiles();
    private void AddFiles()
    {
        if (_isImporting || _isExporting || _guideOpen) return;
        var dialog = new OpenFileDialog { Title = "Chọn ảnh để xử lý", Filter = ImageFilter, Multiselect = true };
        if (dialog.ShowDialog(this) == true) _ = ImportAsync(dialog.FileNames);
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_isImporting || _isExporting) return;
        var dialog = new OpenFolderDialog { Title = "Chọn thư mục ảnh", Multiselect = false };
        if (dialog.ShowDialog(this) == true) _ = ImportAsync([dialog.FolderName]);
    }

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        _selected = FileList.SelectedItem as ImageItem;
        _originalPreview = null; _resultPreview = null;
        PreviewImage.Source = null;
        QueuePreview();
    }

    private void Settings_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready || _updating) return;
        RefreshSettingsVisibility();
        UpdateInteractivity();
        QueuePreview();
    }
    private void Input_Changed(object sender, TextChangedEventArgs e) { if (_ready && !_updating) QueuePreview(); }
    private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || _updating) return;
        RefreshSettingsVisibility(); QueuePreview();
    }

    private void RefreshSettingsVisibility()
    {
        QualityValueLabel.Text = $"{QualitySlider.Value:0}%";
        OpacityValueLabel.Text = $"{OpacitySlider.Value:0}%";
        bool png = PngOption.IsChecked == true;
        bool target = TargetSizeCheck.IsChecked == true;
        bool multiple = MultiSizeCheck.IsChecked == true;
        CropOptionsPanel.Visibility = CropCombo.SelectedIndex == 0 ? Visibility.Collapsed : Visibility.Visible;
        CustomCropPanel.Visibility = CropCombo.SelectedIndex == (int)CropAspect.Custom ? Visibility.Visible : Visibility.Collapsed;
        CropHorizontalLabel.Text = $"{CropHorizontalSlider.Value:0}%";
        CropVerticalLabel.Text = $"{CropVerticalSlider.Value:0}%";
        RefreshNamingVisibility();
        QualitySlider.IsEnabled = !png;
        QualityFieldLabel.Text = target && !png ? "Chất lượng tối đa" : "Chất lượng";
        QualityValueLabel.Text = png ? "Không mất dữ liệu" : $"{QualitySlider.Value:0}%";
        QualityHintLabel.Text = png ? "PNG nén không mất dữ liệu; dung lượng có thể lớn hơn ảnh gốc." : "Chất lượng thấp hơn cho tệp nhỏ hơn. Xem trước để chọn mức phù hợp.";
        JpegBackgroundPanel.Visibility = JpegOption.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        TargetSizePanel.Visibility = target ? Visibility.Visible : Visibility.Collapsed;
        MinimumQualityPanel.Visibility = png ? Visibility.Collapsed : Visibility.Visible;
        TargetHintLabel.Text = png
            ? "PNG giữ chất lượng; app chỉ kiểm tra dung lượng. Giảm kích thước hoặc đổi sang JPG/WebP nếu chưa đạt. 1 KB = 1.024 byte."
            : "Tự hạ chất lượng trong khoảng đã chọn, giữ kích thước. Nếu chưa đạt, ảnh đó sẽ không được lưu. 1 KB = 1.024 byte.";
        SingleSizePanel.Visibility = multiple ? Visibility.Collapsed : Visibility.Visible;
        MultiSizePanel.Visibility = multiple ? Visibility.Visible : Visibility.Collapsed;
        FitPanel.Visibility = ResizeCombo.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        PercentagePanel.Visibility = ResizeCombo.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        AspectHintLabel.Text = multiple ? "Mỗi cỡ vào một thư mục riêng. Các cỡ là khung tối đa; luôn giữ tỉ lệ ảnh."
            : CropCombo.SelectedIndex != 0 ? "Cắt theo tỉ lệ đã chọn, sau đó đổi kích thước. Không kéo méo ảnh."
            : ResizeCombo.SelectedIndex == 1 ? "Giữ số điểm ảnh gốc, tự xoay đúng chiều ảnh." : "Luôn giữ tỉ lệ ảnh, không kéo méo.";
        AllowUpscaleCheck.Visibility = !multiple && ResizeCombo.SelectedIndex == 1 ? Visibility.Collapsed : Visibility.Visible;
        WatermarkOptionsPanel.Visibility = WatermarkCombo.SelectedIndex == 0 ? Visibility.Collapsed : Visibility.Visible;
        bool textWatermark = WatermarkCombo.SelectedIndex == (int)WatermarkKind.Text;
        RepeatWatermarkCheck.Visibility = textWatermark ? Visibility.Visible : Visibility.Collapsed;
        bool repeat = textWatermark && RepeatWatermarkCheck.IsChecked == true;
        RepeatWatermarkPanel.Visibility = repeat ? Visibility.Visible : Visibility.Collapsed;
        PositionCombo.Visibility = repeat ? Visibility.Collapsed : Visibility.Visible;
        WatermarkAngleLabel.Text = $"{WatermarkAngleSlider.Value:0}°";
        WatermarkSpacingLabel.Text = $"{WatermarkSpacingSlider.Value:0}%";
        WatermarkTextInput.Visibility = WatermarkCombo.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        LogoButton.Visibility = WatermarkCombo.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        WatermarkColorCombo.Visibility = WatermarkCombo.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        WatermarkSizeLabel.Text = WatermarkCombo.SelectedIndex == 2 ? $"{Math.Min(90, WatermarkSizeSlider.Value * 5):0}% chiều rộng" : $"{WatermarkSizeSlider.Value:0}% cạnh ngắn";
        PreviewFormatLabel.Text = JpegOption.IsChecked == true ? "JPG" : png ? "PNG" : "WEBP";
    }

    internal ProcessingOptions ReadOptions()
    {
        int Parse(TextBox field, string label, int fallback, bool needed)
        {
            if (!needed) return fallback;
            if (!int.TryParse(field.Text.Trim(), out int value)) throw new ArgumentException($"{label} cần là số nguyên.");
            return value;
        }
        var options = new ProcessingOptions
        {
            Format = JpegOption.IsChecked == true ? OutputFormat.Jpeg : PngOption.IsChecked == true ? OutputFormat.Png : OutputFormat.Webp,
            Quality = (int)QualitySlider.Value,
            TargetBytes = TargetSizeCheck.IsChecked == true ? (long)Parse(TargetKbInput, "Dung lượng mục tiêu", 200, true) * 1024 : 0,
            MinimumQuality = Parse(MinimumQualityInput, "Chất lượng tối thiểu", 40, TargetSizeCheck.IsChecked == true && PngOption.IsChecked != true),
            Resize = (ImageResizeMode)ResizeCombo.SelectedIndex,
            MaxWidth = Parse(WidthInput, "Chiều rộng", 1920, ResizeCombo.SelectedIndex == 0 && MultiSizeCheck.IsChecked != true),
            MaxHeight = Parse(HeightInput, "Chiều cao", 1920, ResizeCombo.SelectedIndex == 0 && MultiSizeCheck.IsChecked != true),
            Percentage = Parse(PercentageInput, "Tỉ lệ", 100, ResizeCombo.SelectedIndex == 2 && MultiSizeCheck.IsChecked != true),
            AllowUpscale = AllowUpscaleCheck.IsChecked == true,
            Rotation = (ImageRotation)RotationCombo.SelectedIndex,
            FlipHorizontal = FlipHorizontalCheck.IsChecked == true,
            FlipVertical = FlipVerticalCheck.IsChecked == true,
            Crop = (CropAspect)CropCombo.SelectedIndex,
            CropWidth = Parse(CropWidthInput, "Tỉ lệ cắt chiều rộng", 1, CropCombo.SelectedIndex == (int)CropAspect.Custom),
            CropHeight = Parse(CropHeightInput, "Tỉ lệ cắt chiều cao", 1, CropCombo.SelectedIndex == (int)CropAspect.Custom),
            CropHorizontal = (int)CropHorizontalSlider.Value, CropVertical = (int)CropVerticalSlider.Value,
            JpegBackground = JpegBackgroundCombo.SelectedIndex == 1 ? "#000000" : "#FFFFFF",
            Watermark = (WatermarkKind)WatermarkCombo.SelectedIndex,
            WatermarkText = WatermarkTextInput.Text,
            WatermarkImagePath = _logoPath,
            Position = (WatermarkPosition)PositionCombo.SelectedIndex,
            Opacity = (int)OpacitySlider.Value,
            WatermarkSize = (int)WatermarkSizeSlider.Value,
            WatermarkColor = WatermarkColorCombo.SelectedIndex == 1 ? "#000000" : "#FFFFFF",
            RepeatWatermark = RepeatWatermarkCheck.IsChecked == true,
            WatermarkAngle = (int)WatermarkAngleSlider.Value,
            WatermarkSpacing = (int)WatermarkSpacingSlider.Value,
            Suffix = SuffixInput.Text
        };
        options.Validate();
        return options;
    }

    private void QueuePreview()
    {
        if (!_ready || _updating || _isImporting || _isExporting || _closeAfterWork) return;
        _previewCancellation?.Cancel();
        _previewCancellation = null;
        SavingsBadge.Visibility = Visibility.Collapsed;
        PreviewTargetBadge.Visibility = Visibility.Collapsed;
        if (_selected is not { CanProcess: true })
        {
            PreviewImage.Source = null;
            _comparisonPreview = null; _resultPreview = null;
            OutputNameLabel.Text = "Thêm ảnh để xem tên kết quả";
            UpdateComparisonView(true);
            EmptyPreview.Visibility = Files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Checkerboard.Visibility = Visibility.Collapsed;
            PreviewLoading.Visibility = Visibility.Collapsed;
            PreviewInfoLabel.Text = _selected?.StatusDetail ?? "Chọn một ảnh để xem thay đổi ngay tại đây";
            return;
        }
        EmptyPreview.Visibility = Visibility.Collapsed;
        Checkerboard.Visibility = Visibility.Visible;
        _resultPreview = null;
        _comparisonPreview = null; ComparisonBeforeImage.Source = null;
        ComparisonBeforeLayer.Visibility = CompareControls.Visibility = ComparisonDivider.Visibility = Visibility.Collapsed;
        _previewHasTarget = false;
        PreviewImage.Source = null;
        OutputVariant variant;
        try
        {
            var settings = ReadBatchSettings();
            var variants = settings.GetVariants();
            variant = variants[Math.Clamp(PreviewSizeCombo.SelectedIndex, 0, variants.Count - 1)];
            UpdateOutputNamePreview(variant, settings.Naming, settings.PreserveFolders);
        }
        catch (ArgumentException exception)
        {
            _resultPreview = null; PreviewImage.Source = null;
            OutputNameLabel.Text = exception.Message;
            PreviewInfoLabel.Text = exception.Message; PreviewLoading.Visibility = Visibility.Collapsed; return;
        }
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _previewCancellation = cancellation;
        PreviewLoading.Visibility = Visibility.Visible;
        bool native = NativeZoomOption.IsChecked == true;
        _previewTask = RunPreviewAsync(_selected, variant, native, CompareTab.IsChecked == true,
            native && OriginalTab.IsChecked == true, cancellation);
    }

    private async Task RunPreviewAsync(ImageItem selected, OutputVariant variant, bool native, bool compare, bool originalNative,
        CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        bool entered = false;
        try
        {
            await Task.Delay(250, token);
            await _previewGate.WaitAsync(token); entered = true;
            var original = _originalNative == originalNative ? _originalPreview : null;
            if (original is null) _originalPreview = null;
            var data = await Task.Run(() =>
            {
                var processed = ImageProcessor.Process(selected.FilePath, variant.Options, token);
                token.ThrowIfCancellationRequested();
                var result = BitmapFromBytes(processed.Bytes, native ? 0 : 1400);
                if (original is null)
                {
                    using var bitmap = ImageProcessor.DecodeOriented(selected.FilePath, token);
                    original = BitmapFromBitmap(bitmap, originalNative ? 0 : 1400);
                }
                BitmapSource? reference = null;
                if (compare)
                {
                    token.ThrowIfCancellationRequested();
                    using var bitmap = ImageProcessor.RenderReference(selected.FilePath, variant.Options, token);
                    reference = BitmapFromBitmap(bitmap, native ? 0 : 1400);
                }
                return (Processed: processed, Preview: result, Original: original, Reference: reference);
            }, token);
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_selected, selected)) return;
            _originalPreview = data.Original;
            _resultPreview = data.Preview;
            _comparisonPreview = data.Reference;
            _previewNative = native; _originalNative = originalNative;
            _previewSummary = (variant.Folder.Length > 0 ? variant.Folder + " · " : "") +
                $"{data.Processed.Width:N0} × {data.Processed.Height:N0} px · {ImageItem.FormatBytes(data.Processed.Bytes.LongLength)}" +
                (data.Processed.Quality is int quality ? $" · CL {quality}%" : "");
            _previewBytes = data.Processed.Bytes.LongLength;
            _previewHasTarget = variant.Options.TargetBytes > 0;
            _previewTargetMet = data.Processed.TargetMet;
            PreviewTargetLabel.Text = _previewTargetMet
                ? $"Đạt mục tiêu ≤ {ImageItem.FormatBytes(variant.Options.TargetBytes)}"
                : $"Chưa đạt {ImageItem.FormatBytes(variant.Options.TargetBytes)} · Hãy giảm kích thước hoặc đổi mức chất lượng. App sẽ bỏ qua ảnh này khi xuất.";
            UpdateDisplayedPreview();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested) { PreviewInfoLabel.Text = exception.Message; ShowStatus("Chưa thể xem trước · " + exception.Message, true); }
        }
        finally
        {
            if (entered) _previewGate.Release();
            if (!token.IsCancellationRequested) PreviewLoading.Visibility = Visibility.Collapsed;
            if (ReferenceEquals(_previewCancellation, cancellation)) _previewCancellation = null;
            cancellation.Dispose();
        }
    }

    private void PreviewTab_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        bool native = NativeZoomOption.IsChecked == true;
        if (_previewCancellation is not null || _previewNative != native ||
            (CompareTab.IsChecked == true && _comparisonPreview is null) || (OriginalTab.IsChecked == true && native && !_originalNative))
            QueuePreview();
        else UpdateDisplayedPreview();
    }
    private void UpdateDisplayedPreview()
    {
        bool original = OriginalTab.IsChecked == true;
        PreviewImage.Source = original ? _originalPreview : _resultPreview;
        UpdateComparisonView(original);
        var info = _selected?.Info;
        if (info is null) return;
        PreviewInfoLabel.Text = original ? $"Ảnh gốc · {info.Width:N0} × {info.Height:N0} px · {ImageItem.FormatBytes(info.Bytes)}" : _previewSummary;
        SavingsBadge.Visibility = !original && _resultPreview is not null ? Visibility.Visible : Visibility.Collapsed;
        PreviewTargetBadge.Visibility = !original && _resultPreview is not null && _previewHasTarget ? Visibility.Visible : Visibility.Collapsed;
        PreviewTargetBadge.Background = new SolidColorBrush(_previewTargetMet ? Color.FromRgb(237, 247, 238) : Color.FromRgb(255, 242, 224));
        PreviewTargetLabel.Foreground = new SolidColorBrush(_previewTargetMet ? Color.FromRgb(32, 124, 85) : Color.FromRgb(158, 88, 35));
        if (_previewBytes <= info.Bytes)
        {
            SavingsLabel.Text = $"Nhẹ hơn {(1 - (double)_previewBytes / Math.Max(1, info.Bytes)) * 100:0.#}%";
            SavingsLabel.Foreground = new SolidColorBrush(Color.FromRgb(32, 124, 85));
            SavingsBadge.Background = new SolidColorBrush(Color.FromRgb(237, 247, 238));
        }
        else
        {
            SavingsLabel.Text = $"Lớn hơn {((double)_previewBytes / Math.Max(1, info.Bytes) - 1) * 100:0.#}%";
            SavingsLabel.Foreground = new SolidColorBrush(Color.FromRgb(149, 104, 36));
            SavingsBadge.Background = new SolidColorBrush(Color.FromRgb(252, 245, 230));
        }
    }

    internal static BitmapSource BitmapFromBytes(byte[] bytes, int maximumSide = 1400)
    {
        using var decoded = SKBitmap.Decode(bytes) ?? throw new InvalidDataException("Không thể tạo ảnh xem trước.");
        return BitmapFromBitmap(decoded, maximumSide);
    }

    private static BitmapSource BitmapFromBitmap(SKBitmap decoded, int maximumSide)
    {
        float ratio = maximumSide > 0 ? Math.Min(1f, (float)maximumSide / Math.Max(decoded.Width, decoded.Height)) : 1f;
        var sampling = ratio < 1f ? new SKSamplingOptions(SKCubicResampler.Mitchell) : new SKSamplingOptions(SKFilterMode.Nearest);
        using var bitmap = decoded.Resize(new SKImageInfo(Math.Max(1, (int)(decoded.Width * ratio)),
            Math.Max(1, (int)(decoded.Height * ratio)), SKColorType.Bgra8888, SKAlphaType.Premul), sampling)
            ?? throw new InvalidDataException("Không đủ bộ nhớ để tạo ảnh xem trước.");
        var source = BitmapSource.Create(bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Pbgra32, null, bitmap.GetPixels(), bitmap.ByteCount, bitmap.RowBytes);
        source.Freeze();
        return source;
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        string? preset = (sender as Button)?.Tag as string;
        var options = ReadOptionsSafe() with
        {
            Format = preset == "detail" ? OutputFormat.Png : OutputFormat.Webp,
            Quality = preset == "small" ? 70 : 82,
            TargetBytes = 0,
            Resize = preset == "detail" ? ImageResizeMode.Original : ImageResizeMode.Fit,
            MaxWidth = preset == "small" ? 1280 : 1920,
            MaxHeight = preset == "small" ? 1280 : 1920,
            AllowUpscale = false
        };
        ApplyBatchSettings(new BatchSettings { Options = options, Zip = ZipCheck.IsChecked == true });
        ShowStatus(preset switch { "small" => "Đã chọn Gọn nhẹ · WebP 70%, tối đa 1.280 px", "detail" => "Đã chọn Chi tiết · PNG, giữ kích thước gốc", _ => "Đã chọn Cân bằng · WebP 82%, tối đa 1.920 px" });
    }
    private ProcessingOptions ReadOptionsSafe() { try { return ReadOptions(); } catch (ArgumentException) { return new ProcessingOptions(); } }
    private void Reset_Click(object sender, RoutedEventArgs e) { ApplyBatchSettings(new BatchSettings()); ShowStatus("Đã khôi phục thiết lập mặc định."); }
    internal void ApplyOptions(ProcessingOptions options)
    {
        bool wasUpdating = _updating;
        _updating = true;
        WebpOption.IsChecked = options.Format == OutputFormat.Webp;
        JpegOption.IsChecked = options.Format == OutputFormat.Jpeg;
        PngOption.IsChecked = options.Format == OutputFormat.Png;
        QualitySlider.Value = options.Quality;
        TargetSizeCheck.IsChecked = options.TargetBytes > 0;
        TargetKbInput.Text = (options.TargetBytes > 0 ? options.TargetBytes / 1024 : 200).ToString();
        MinimumQualityInput.Text = options.MinimumQuality.ToString();
        ResizeCombo.SelectedIndex = (int)options.Resize;
        WidthInput.Text = options.MaxWidth.ToString(); HeightInput.Text = options.MaxHeight.ToString();
        PercentageInput.Text = options.Percentage.ToString();
        AllowUpscaleCheck.IsChecked = options.AllowUpscale;
        RotationCombo.SelectedIndex = (int)options.Rotation;
        FlipHorizontalCheck.IsChecked = options.FlipHorizontal; FlipVerticalCheck.IsChecked = options.FlipVertical;
        CropCombo.SelectedIndex = (int)options.Crop;
        CropWidthInput.Text = options.CropWidth.ToString(); CropHeightInput.Text = options.CropHeight.ToString();
        CropHorizontalSlider.Value = options.CropHorizontal; CropVerticalSlider.Value = options.CropVertical;
        JpegBackgroundCombo.SelectedIndex = options.JpegBackground == "#000000" ? 1 : 0;
        WatermarkCombo.SelectedIndex = (int)options.Watermark;
        WatermarkTextInput.Text = options.WatermarkText;
        _logoPath = options.WatermarkImagePath;
        LogoButton.Content = string.IsNullOrEmpty(_logoPath) ? "Chọn ảnh logo…" : Path.GetFileName(_logoPath);
        LogoButton.ToolTip = _logoPath;
        PositionCombo.SelectedIndex = (int)options.Position;
        OpacitySlider.Value = options.Opacity; WatermarkSizeSlider.Value = options.WatermarkSize;
        WatermarkColorCombo.SelectedIndex = options.WatermarkColor == "#000000" ? 1 : 0;
        RepeatWatermarkCheck.IsChecked = options.RepeatWatermark;
        WatermarkAngleSlider.Value = options.WatermarkAngle; WatermarkSpacingSlider.Value = options.WatermarkSpacing;
        SuffixInput.Text = options.Suffix;
        _updating = wasUpdating;
        if (!_updating) { RefreshSettingsVisibility(); QueuePreview(); }
    }

    private void ChooseLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Chọn ảnh logo", Filter = ImageFilter };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            ImageProcessor.Inspect(dialog.FileName);
            _logoPath = dialog.FileName;
            LogoButton.Content = Path.GetFileName(_logoPath); LogoButton.ToolTip = _logoPath;
            QueuePreview();
        }
        catch (Exception ex) { ShowStatus("Không thể dùng logo · " + ex.Message, true); }
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Chọn thư mục lưu ảnh kết quả", Multiselect = false };
        if (Directory.Exists(OutputDirectoryInput.Text)) dialog.InitialDirectory = OutputDirectoryInput.Text;
        if (dialog.ShowDialog(this) == true) { OutputDirectoryInput.Text = dialog.FolderName; ShowStatus("Đã đổi thư mục lưu. Ảnh gốc luôn được giữ nguyên."); }
    }
    private void OpenOutput_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!Directory.Exists(OutputDirectoryInput.Text)) { ShowStatus("Thư mục kết quả sẽ được tạo khi bạn xuất ảnh."); return; }
            Process.Start(new ProcessStartInfo { FileName = OutputDirectoryInput.Text, UseShellExecute = true });
        }
        catch (Exception ex) { ShowStatus("Không mở được thư mục · " + ex.Message, true); }
    }

    private async void Export_Click(object sender, RoutedEventArgs e) => await ExportAsync();
    private async void Retry_Click(object sender, RoutedEventArgs e) => await ExportAsync(true);
    internal async Task ExportAsync(bool retryFailed = false)
    {
        if (_isExporting || _isImporting || _guideOpen || !Files.Any(x => x.CanProcess)) return;
        var allItems = Files.Where(x => x.CanProcess).ToArray();
        var items = allItems.Where(item => !retryFailed || item.ExportSlots.Any(slot => slot.Failed)).ToArray();
        if (items.Length == 0) return;
        BatchSettings settings;
        var work = new List<(ImageItem Item, ExportSlot Slot, OutputVariant Variant, string Destination)>();
        string directory;
        try
        {
            settings = ReadBatchSettings();
            directory = Path.GetFullPath(OutputDirectoryInput.Text);
            foreach (var item in items)
            {
                int index = Array.IndexOf(allItems, item);
                ExportSlot[] slots = retryFailed ? item.ExportSlots.Where(slot => slot.Failed).ToArray()
                    : settings.MultipleSizes ? settings.Sizes.Select(size => new ExportSlot(index, size)).ToArray()
                    : [new ExportSlot(index, null)];
                foreach (var slot in slots)
                {
                    var variant = slot.GetVariant(settings.Options);
                    var info = item.Info!;
                    var (width, height) = ImageProcessor.CalculateSize(info.Width, info.Height, variant.Options);
                    try { settings.Naming.CreateStem(item.FilePath, variant.Options, slot.SourceIndex, width, height); }
                    catch (ArgumentException error) { throw new ArgumentException(item.Name + ": " + error.Message, error); }
                    string folder = SourceLayout.OutputFolder(variant.Folder, item.RelativeDirectory, settings.PreserveFolders);
                    work.Add((item, slot, variant, SourceLayout.ResolveOutput(directory, folder)));
                }
            }
        }
        catch (Exception exception) { ShowStatus("Chưa thể xuất · " + exception.Message, true); return; }
        if (!retryFailed)
            foreach (var item in items)
            {
                item.ExportSlots.Clear();
                item.ExportSlots.AddRange(work.Where(job => ReferenceEquals(job.Item, item)).Select(job => job.Slot));
                item.LastOutput = null;
            }
        bool makeZip = settings.Zip;
        int totalOutputs = work.Count;
        _previewCancellation?.Cancel(); PreviewLoading.Visibility = Visibility.Collapsed;
        _exportCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _exportCancellation.Token;
        _isExporting = true;
        UpdateInteractivity(); BatchProgress.Value = 0;
        var written = new List<string>();
        var successfulSources = new HashSet<ImageItem>();
        int failures = 0, completed = 0;
        long sourceBytes = 0, outputBytes = 0;
        bool cancelled = false, createdZip = false;
        string? fatalError = null;
        foreach (var item in items) item.SetStatus("Chờ xuất");
        try
        {
            await _previewTask;
            string? directoryError = null;
            try { await Task.Run(() => Directory.CreateDirectory(directory), token); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            { directoryError = error.Message; }
            foreach (var (item, slot, variant, destination) in work)
            {
                token.ThrowIfCancellationRequested();
                item.SetStatus("Đang xử lý…");
                ShowStatus($"{(retryFailed ? "Đang thử lại" : "Đang xuất")} {completed + 1}/{totalOutputs} tệp · {item.DisplayPath}" +
                    (variant.Folder.Length > 0 ? " · " + variant.Folder : ""));
                var result = directoryError is null
                    ? await Task.Run(() => BatchExporter.ExportOne(item.FilePath, destination, variant.Options, token, settings.Naming, slot.SourceIndex), token)
                    : new ExportResult(item.FilePath, null, 0, 0, 0, directoryError);
                slot.Result = result;
                if (result.Error is not null) failures++;
                else
                {
                    written.Add(result.Destination!);
                    if (successfulSources.Add(item)) sourceBytes += item.Info!.Bytes;
                    outputBytes += result.Bytes;
                }
                RefreshExportStatus(item);
                completed++;
                BatchProgress.Value = (double)completed / totalOutputs * (makeZip ? 95 : 100);
            }
            if (makeZip && written.Count > 0)
            {
                ShowStatus("Đang tạo tệp ZIP…");
                await Task.Run(() => BatchExporter.CreateZip(directory, written, token), token);
                createdZip = true;
            }
            BatchProgress.Value = 100;
        }
        catch (OperationCanceledException) { cancelled = true; }
        catch (Exception exception) { fatalError = exception.Message; }
        finally
        {
            foreach (var item in items) RefreshExportStatus(item);
            _isExporting = false;
            UpdateInteractivity();
            string summary = $"{(cancelled ? "Đã dừng" : fatalError is not null ? "Đã dừng vì lỗi" : retryFailed ? "Đã thử lại" : "Hoàn tất")} · Đã xuất {written.Count}/{totalOutputs} tệp";
            if (written.Count > 0) summary += totalOutputs == items.Length ? $" · {ImageItem.FormatBytes(sourceBytes)} → {ImageItem.FormatBytes(outputBytes)}"
                : $" · {items.Length} ảnh · {ImageItem.FormatBytes(outputBytes)}";
            if (failures > 0) summary += $" · {failures} lỗi (rê chuột vào trạng thái để xem)";
            if (createdZip) summary += " · Đã tạo ZIP";
            if (fatalError is not null) summary += " · Lỗi: " + fatalError;
            ShowStatus(summary, failures > 0 || fatalError is not null);
            _exportCancellation?.Dispose(); _exportCancellation = null;
            SavePreferences(); QueuePreview();
            if (_closeAfterWork) Close();
        }
    }

    private static void RefreshExportStatus(ImageItem item)
    {
        int successes = item.ExportSlots.Count(slot => slot.Result is { Error: null });
        int failures = item.ExportSlots.Count(slot => slot.Failed);
        item.LastOutput = item.ExportSlots.LastOrDefault(slot => slot.Result is { Error: null })?.Result?.Destination;
        string status = successes == 0 ? failures > 0 ? "Lỗi xuất" : "Chưa xuất"
            : item.ExportSlots.Count == 1 ? "Đã xuất" : $"Đã xuất {successes}/{item.ExportSlots.Count} cỡ";
        var details = item.ExportSlots.Select(slot =>
        {
            string prefix = slot.Size is { } size ? size.FolderName + ": " : "";
            var result = slot.Result;
            return prefix + (result is null ? "Chưa xuất (đã dừng)" : result.Error ??
                $"{result.Width:N0} × {result.Height:N0} · {ImageItem.FormatBytes(result.Bytes)}" +
                (result.Quality is int quality ? $" · CL {quality}%" : "") + $"\n{result.Destination}");
        });
        item.SetStatus(status, string.Join("\n\n", details), failures > 0);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _exportCancellation?.Cancel(); _importCancellation?.Cancel(); CancelButton.IsEnabled = false;
        ShowStatus(_isImporting ? "Đang dừng… Các ảnh đã thêm được giữ lại." : "Đang dừng… Các ảnh đã xuất được giữ lại.");
    }
    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_isExporting || _isImporting) return;
        _previewCancellation?.Cancel(); Files.Clear(); _selected = null;
        _originalPreview = null; _resultPreview = null;
        PreviewImage.Source = null; BatchProgress.Value = 0;
        UpdateCounters(); QueuePreview(); ShowStatus("Đã xóa danh sách. Các tệp trên máy vẫn được giữ nguyên.");
    }
    private void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if (_isExporting || _isImporting || (sender as Button)?.Tag is not ImageItem item) return;
        int index = Files.IndexOf(item); Files.Remove(item);
        if (FileList.SelectedItem is null && Files.Count > 0) FileList.SelectedIndex = Math.Min(index, Files.Count - 1);
        UpdateCounters(); QueuePreview();
        e.Handled = true;
    }

    private void UpdateCounters()
    {
        CountLabel.Text = $"{Files.Count} ảnh";
        TotalLabel.Text = $"{Files.Count(x => x.CanProcess)} ảnh · {ImageItem.FormatBytes(Files.Sum(x => x.Info?.Bytes ?? 0))}";
        EmptyQueueLabel.Visibility = Files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateInteractivity();
    }
    private void UpdateInteractivity()
    {
        bool busy = _isExporting || _isImporting;
        HeaderPanel.IsEnabled = WorkspacePanel.IsEnabled = FooterPanel.IsEnabled = !_guideOpen;
        GuideButton.IsEnabled = !busy;
        AddButton.IsEnabled = !busy; AddFolderButton.IsEnabled = !busy;
        IncludeSubfoldersCheck.IsEnabled = !busy;
        ClearButton.IsEnabled = !busy && Files.Count > 0;
        int failures = Files.Sum(item => item.ExportSlots.Count(slot => slot.Failed));
        RetryButton.IsEnabled = !busy && failures > 0;
        RetryButton.Content = $"Thử lại lỗi ({failures})";
        FileList.IsEnabled = !busy;
        SettingsPanel.IsEnabled = !busy; ResetButton.IsEnabled = !busy;
        ResultTab.IsEnabled = OriginalTab.IsEnabled = CompareTab.IsEnabled = FitZoomOption.IsEnabled = NativeZoomOption.IsEnabled
            = !busy && Files.Any(item => item.CanProcess);
        BrowseButton.IsEnabled = !busy;
        ExportButton.IsEnabled = !busy && Files.Any(x => x.CanProcess);
        int count = Files.Count(x => x.CanProcess);
        ExportButtonLabel.Text = _isExporting ? "Đang xuất…" : MultiSizeCheck.IsChecked == true
            ? $"Xuất {count * OutputSizes.Count} tệp" : $"Xuất {count} ảnh";
        CancelButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.IsEnabled = busy;
        CommandManager.InvalidateRequerySuggested();
    }
    private void ShowStatus(string message, bool error = false)
    {
        StatusLabel.Text = message;
        StatusLabel.Foreground = new SolidColorBrush(error ? Color.FromRgb(175, 84, 60) : Color.FromRgb(115, 128, 105));
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        bool accept = !_isExporting && !_isImporting && !_guideOpen && e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = accept ? DragDropEffects.Copy : DragDropEffects.None;
        DropOverlay.Visibility = accept ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }
    private void Window_DragLeave(object sender, DragEventArgs e) { if (!IsMouseOver) DropOverlay.Visibility = Visibility.Collapsed; }
    private async void Window_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (!_guideOpen && e.Data.GetData(DataFormats.FileDrop) is string[] paths) await ImportAsync(paths);
        e.Handled = true;
    }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExporting || _isImporting)
        {
            e.Cancel = true; _closeAfterWork = true;
            _exportCancellation?.Cancel(); _lifetime.Cancel();
            ShowStatus("Đang dừng và đóng…"); return;
        }
        SavePreferences();
        _lifetime.Cancel(); _previewCancellation?.Cancel();
    }
    private sealed record Preferences(ProcessingOptions Options, string Directory, bool Zip, bool MultipleSizes = false,
        OutputSize[]? Sizes = null, FileNamingOptions? Naming = null, bool PreserveFolders = false, bool IncludeSubfolders = false);
    private void SavePreferences()
    {
        if (_diagnosticMode) return;
        try
        {
            var settings = ReadBatchSettings();
            var preferences = new Preferences(settings.Options, OutputDirectoryInput.Text, settings.Zip, settings.MultipleSizes, settings.Sizes,
                settings.Naming, settings.PreserveFolders, IncludeSubfoldersCheck.IsChecked == true);
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
            string temporary = PreferencesPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(preferences));
                File.Move(temporary, PreferencesPath, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException) { }
    }
    private void RestorePreferences()
    {
        try
        {
            if (!File.Exists(PreferencesPath)) return;
            var preferences = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PreferencesPath));
            if (preferences?.Options is null) return;
            ApplyBatchSettings(new BatchSettings { Options = preferences.Options, Zip = preferences.Zip,
                MultipleSizes = preferences.MultipleSizes, Sizes = preferences.Sizes ?? new BatchSettings().Sizes,
                Naming = preferences.Naming ?? new FileNamingOptions(), PreserveFolders = preferences.PreserveFolders });
            IncludeSubfoldersCheck.IsChecked = preferences.IncludeSubfolders;
            if (!string.IsNullOrWhiteSpace(preferences.Directory)) OutputDirectoryInput.Text = preferences.Directory;
            ZipCheck.IsChecked = preferences.Zip;
        }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or UnauthorizedAccessException) { }
    }

    private sealed class ActionCommand(Action action, Func<bool> enabled) : ICommand
    {
        public bool CanExecute(object? parameter) => enabled();
        public void Execute(object? parameter) => action();
        public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    }
}
