using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelBatch.Core;
using SkiaSharp;
using ImageResizeMode = PixelBatch.Core.ResizeMode;

namespace PixelBatch;

public partial class MainWindow
{
    private async Task RunAdvancedUiChecksAsync(string directory, Action<bool, string> Check)
    {
        var settings = new BatchSettings
        {
            Options = new ProcessingOptions { Format = OutputFormat.Jpeg, Quality = 50, Crop = CropAspect.Square,
                CropHorizontal = 75, Watermark = WatermarkKind.Text, WatermarkText = "© PixelBatch Studio", WatermarkSize = 5, Opacity = 90 },
            Naming = new FileNamingOptions { Mode = FileNamingMode.Template, Template = "{sku}-{n:000}-{width}x{height}", Sku = "AO", StartNumber = 7 },
            MultipleSizes = true, Sizes = [new(800, 800), new(1600, 1600)], Zip = true
        };
        var hashes = Files.ToDictionary(item => item.FilePath, item => SHA256.HashData(File.ReadAllBytes(item.FilePath)));
        ApplyBatchSettings(settings);
        PreviewSizeCombo.SelectedIndex = 1;
        await _previewTask;
        Check(CropOptionsPanel.Visibility == Visibility.Visible && ReadOptions().Crop == CropAspect.Square
            && _previewSummary.Contains("1.600 × 1.600"), "Crop controls produce square output in the real image preview");
        Check(OutputNameLabel.Text == "AO-007-1600x1600.jpg" && TemplateNamingPanel.Visibility == Visibility.Visible,
            "Filename preview combines SKU, sequence and the selected variant's actual dimensions");
        CropHorizontalSlider.Value = 0;
        await _previewTask;
        byte[] beforeMove = PreviewPixels(_resultPreview!);
        CropHorizontalSlider.Value = 100;
        await _previewTask;
        Check(ReadOptions().CropHorizontal == 100 && !beforeMove.SequenceEqual(PreviewPixels(_resultPreview!)),
            "Moving the crop position changes the kept region in the encoded preview");
        CropCombo.SelectedIndex = (int)CropAspect.Custom;
        CropWidthInput.Text = "3"; CropHeightInput.Text = "2";
        await _previewTask;
        Check(CustomCropPanel.Visibility == Visibility.Visible && OutputNameLabel.Text == "AO-007-1600x1067.jpg",
            "Custom crop ratios update actual dimensions in the filename preview");
        CropCombo.SelectedIndex = (int)CropAspect.Square;
        CropHorizontalSlider.Value = 75;
        await _previewTask;
        CompareTab.IsChecked = true;
        await _previewTask;
        Check(_comparisonPreview is not null && CompareControls.Visibility == Visibility.Visible
            && _comparisonPreview.PixelWidth == _resultPreview!.PixelWidth && _comparisonPreview.PixelHeight == _resultPreview.PixelHeight,
            "Before/after comparison aligns both images to the same crop and output size");
        var taskBeforeDrag = _previewTask;
        var referenceBeforeDrag = _comparisonPreview;
        CompareSlider.Value = 0;
        var bounds = DisplayedImageBounds();
        double leftClip = ((RectangleGeometry)ComparisonBeforeLayer.Clip).Rect.Width;
        CompareSlider.Value = 100;
        double rightClip = ((RectangleGeometry)ComparisonBeforeLayer.Clip).Rect.Width;
        Check(Math.Abs(leftClip - bounds.Left) < 0.1 && Math.Abs(rightClip - bounds.Right) < 0.1,
            "Comparison slider endpoints reveal the full result or the full source reference");
        MoveComparison(bounds.Left + bounds.Width * 0.25);
        Check(Math.Abs(CompareSlider.Value - 25) < 0.01 && ReferenceEquals(_previewTask, taskBeforeDrag) && ReferenceEquals(_comparisonPreview, referenceBeforeDrag),
            "Dragging across the comparison changes the split without reprocessing the image");
        CompareSlider.Value = 50;
        CropCombo.BringIntoView();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        App.RenderElement(RootGrid, Path.Combine(directory, "crop-comparison.png"));
        NativeZoomOption.IsChecked = true;
        await _previewTask;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        var dpi = VisualTreeHelper.GetDpi(this);
        Check(_resultPreview!.PixelWidth == 1600 && _comparisonPreview!.PixelWidth == 1600
            && Math.Abs((PreviewCanvas.Width - 30) * dpi.DpiScaleX - 1600) < 0.01,
            "100% comparison retains native pixels and maps one image pixel to one physical display pixel");
        App.RenderElement(RootGrid, Path.Combine(directory, "comparison-native-layout.png"));
        File.WriteAllText(Path.Combine(directory, "preview-geometry.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            dpi.DpiScaleX, dpi.DpiScaleY, CanvasWidth = PreviewCanvas.ActualWidth, CanvasHeight = PreviewCanvas.ActualHeight,
            PreviewViewport.ViewportWidth, PreviewViewport.ViewportHeight, PreviewViewport.ScrollableWidth, PreviewViewport.ScrollableHeight
        }));
        Check(PreviewViewport.ViewportWidth > 0 && PreviewViewport.ViewportHeight > 0
            && Math.Abs(PreviewViewport.ScrollableWidth - Math.Max(0, PreviewCanvas.ActualWidth - PreviewViewport.ViewportWidth)) < 1
            && Math.Abs(PreviewViewport.ScrollableHeight - Math.Max(0, PreviewCanvas.ActualHeight - PreviewViewport.ViewportHeight)) < 1
            && (PreviewViewport.ScrollableWidth > 0 || PreviewViewport.ScrollableHeight > 0),
            "100% zoom exposes the scrolling needed for the actual image size and monitor DPI");
        PreviewViewport.ScrollToHorizontalOffset(180); PreviewViewport.ScrollToVerticalOffset(120);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Check((PreviewViewport.ScrollableWidth == 0 || PreviewViewport.HorizontalOffset > 0)
            && (PreviewViewport.ScrollableHeight == 0 || PreviewViewport.VerticalOffset > 0)
            && ComparisonBeforeImage.RenderSize == PreviewImage.RenderSize,
            "Scrolling at 100% keeps source and result aligned in the same viewport");
        App.RenderElement(RootGrid, Path.Combine(directory, "comparison-100.png"));
        OriginalTab.IsChecked = true;
        await _previewTask;
        Check(_originalPreview is { PixelWidth: 2400, PixelHeight: 1600 } && ReferenceEquals(PreviewImage.Source, _originalPreview)
            && ComparisonBeforeLayer.Visibility == Visibility.Collapsed,
            "Original-image tab at 100% loads native source pixels instead of an enlarged thumbnail");
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        PreviewViewport.ScrollToHorizontalOffset(180);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Check(Math.Abs(PreviewViewport.HorizontalOffset - Math.Min(180, PreviewViewport.ScrollableWidth)) < 1,
            "Native source view supports horizontal scrolling to inspect details outside the viewport");
        App.RenderElement(RootGrid, Path.Combine(directory, "original-100.png"));
        CompareTab.IsChecked = true;
        FitZoomOption.IsChecked = true;
        await _previewTask;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Check(_resultPreview!.PixelWidth == 1400 && PreviewViewport.HorizontalScrollBarVisibility == System.Windows.Controls.ScrollBarVisibility.Disabled,
            "Fit-to-window mode releases native-size preview copies and removes scrolling");
        NamingExpander.IsExpanded = true;
        NamingExpander.BringIntoView();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        App.RenderElement(RootGrid, Path.Combine(directory, "batch-naming.png"));
        string output = Path.Combine(directory, "crop-renamed-output");
        OutputDirectoryInput.Text = output;
        await ExportAsync(); await _previewTask;
        string[] outputs = Directory.GetFiles(output, "*.jpg", SearchOption.AllDirectories);
        Check(outputs.Length == 6 && outputs.All(path => { var info = ImageProcessor.Inspect(path); return info.Width == info.Height; }),
            "Batch export applies cropping to all sources and size variants");
        Check(Files.Select((item, index) => Path.GetFileName(item.LastOutput) == $"AO-{index + 7:000}-1600x1600.jpg").All(value => value)
            && Directory.GetFiles(Path.Combine(output, "800x800"), "*.jpg").Select(Path.GetFileName).Order().SequenceEqual(
                new[] { "AO-007-800x800.jpg", "AO-008-800x800.jpg", "AO-009-800x800.jpg" }),
            "Each source keeps its sequence number across every output size");
        using (var archive = ZipFile.OpenRead(Directory.GetFiles(output, "*.zip").Single()))
            Check(archive.Entries.Count == 6 && archive.Entries.All(entry => entry.FullName.Contains("AO-")),
                "ZIP contains the cropped images with the final generated names");
        Check(hashes.All(pair => pair.Value.SequenceEqual(SHA256.HashData(File.ReadAllBytes(pair.Key)))) && hashes.Keys.All(File.Exists),
            "Cropping and renaming preserve all original filenames and file bytes");
        App.RenderElement(RootGrid, Path.Combine(directory, "batch-naming.png"));
        CropOptionsPanel.BringIntoView();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        App.RenderElement(RootGrid, Path.Combine(directory, "exported-comparison.png"));
        int countBefore = Directory.GetFiles(output, "*", SearchOption.AllDirectories).Length;
        NameTemplateInput.Text = "{unknown}";
        await ExportAsync();
        Check(StatusLabel.Text.Contains("Chưa thể xuất") && Directory.GetFiles(output, "*", SearchOption.AllDirectories).Length == countBefore,
            "Invalid template tokens block export before writing any files");
        NameTemplateInput.Text = "CON";
        await ExportAsync();
        Check(StatusLabel.Text.Contains("dành riêng") && Directory.GetFiles(output, "*", SearchOption.AllDirectories).Length == countBefore,
            "Windows-reserved output names are rejected in export preflight");
        NamingCombo.SelectedIndex = (int)FileNamingMode.Replace;
        FindNameInput.Text = Path.GetFileNameWithoutExtension(Files[1].FilePath); ReplaceNameInput.Text = "";
        await ExportAsync();
        Check(StatusLabel.Text.Contains("Chưa thể xuất") && Directory.GetFiles(output, "*", SearchOption.AllDirectories).Length == countBefore,
            "Preflight checks every source name and avoids partial output when a later replacement becomes empty");
        FindNameInput.Text = "VUNG"; ReplaceNameInput.Text = "San-pham";
        await _previewTask;
        Check(ReplaceNamingPanel.Visibility == Visibility.Visible && OutputNameLabel.Text == "San-pham-cao.jpg",
            "Find-and-replace mode previews the renamed output without the original suffix");
        ApplyBatchSettings(settings);
        PresetNameInput.Text = "Ảnh vuông có mã";
        SavePreset_Click(this, new RoutedEventArgs());
        ApplyBatchSettings(new BatchSettings());
        LoadPersonalPresets(); SavedPresetCombo.SelectedIndex = 0;
        ApplySavedPreset_Click(this, new RoutedEventArgs());
        await _previewTask;
        var restored = ReadBatchSettings();
        Check(restored.Options == settings.Options && restored.Naming == settings.Naming && restored.MultipleSizes,
            "A saved preset restores crop ratio, crop position and all advanced naming fields");
        CompareTab.IsChecked = true;
        await _previewTask;
        double previousWidth = Width, previousHeight = Height;
        Width = 1100; Height = 760;
        NamingExpander.BringIntoView();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Check(CompareControls.IsVisible && PreviewViewport.ActualHeight > 80 && ExportButton.IsVisible && SettingsScroll.ScrollableHeight > 0,
            "Comparison and advanced naming remain usable in the compact window");
        App.RenderElement(RootGrid, Path.Combine(directory, "advanced-compact.png"));
        Width = previousWidth; Height = previousHeight;
        await CheckTransparentComparisonAsync(directory, Check);
        ResultTab.IsChecked = true; FitZoomOption.IsChecked = true;
        NamingExpander.IsExpanded = false;
        ApplyBatchSettings(new BatchSettings());
        OutputDirectoryInput.Text = Path.Combine(directory, "output");
        SettingsScroll.ScrollToTop();
        await _previewTask;
    }

    private async Task CheckTransparentComparisonAsync(string directory, Action<bool, string> Check)
    {
        string source = Path.Combine(directory, "input", "compare-alpha.png");
        using (var bitmap = new SKBitmap(400, 300, SKColorType.Rgba8888, SKAlphaType.Premul))
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Color = SKColors.Coral };
            canvas.DrawRect(0, 0, 40, 40, paint);
            using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(source, encoded.ToArray());
        }
        await ImportAsync([source]);
        var temporaryItem = Files.Single(item => item.FilePath == source);
        FileList.SelectedItem = temporaryItem;
        ApplyBatchSettings(new BatchSettings { Options = new ProcessingOptions { Format = OutputFormat.Png, Resize = ImageResizeMode.Original,
            Watermark = WatermarkKind.Image, WatermarkImagePath = Path.Combine(directory, "input", "logo.png"),
            Position = WatermarkPosition.Center, WatermarkSize = 6, Opacity = 100 } });
        CompareTab.IsChecked = true; FitZoomOption.IsChecked = true;
        await _previewTask;
        CompareSlider.Value = 0;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        string afterPath = Path.Combine(directory, "alpha-after.png");
        App.RenderElement(PreviewCanvas, afterPath);
        CompareSlider.Value = 100;
        string beforePath = Path.Combine(directory, "alpha-before.png");
        App.RenderElement(PreviewCanvas, beforePath);
        using (var after = SKBitmap.Decode(afterPath))
        using (var before = SKBitmap.Decode(beforePath))
            Check(after.GetPixel(after.Width / 2, after.Height / 2).Alpha > 200 && before.GetPixel(before.Width / 2, before.Height / 2).Alpha == 0,
                "Transparent before/after comparison hides the result watermark instead of showing it through the source alpha");
        CompareSlider.Value = 50;
        App.RenderElement(RootGrid, Path.Combine(directory, "transparent-comparison.png"));
        FileList.SelectedItem = Files.First(item => !ReferenceEquals(item, temporaryItem));
        RemoveFile_Click(new System.Windows.Controls.Button { Tag = temporaryItem },
            new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        await _previewTask;
    }

    private static byte[] PreviewPixels(BitmapSource bitmap)
    {
        int stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        return SHA256.HashData(pixels);
    }
}
