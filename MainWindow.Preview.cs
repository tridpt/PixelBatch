using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PixelBatch;

public partial class MainWindow
{
    private BitmapSource? _comparisonPreview;
    private bool _previewNative;
    private bool _originalNative;
    private bool _draggingComparison;

    private void Zoom_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready || _updating) return;
        PreviewViewport.ScrollToHorizontalOffset(0); PreviewViewport.ScrollToVerticalOffset(0);
        QueuePreview();
    }

    private void UpdateComparisonView(bool original)
    {
        bool comparing = !original && CompareTab.IsChecked == true && _comparisonPreview is not null && _resultPreview is not null;
        ComparisonBeforeImage.Source = comparing ? _comparisonPreview : null;
        if (!comparing) ComparisonAfterLayer.Clip = null;
        ComparisonBeforeLayer.Visibility = CompareControls.Visibility = ComparisonDivider.Visibility = comparing ? Visibility.Visible : Visibility.Collapsed;
        PreviewCanvas.Cursor = comparing ? Cursors.SizeWE : Cursors.Arrow;
        UpdatePreviewLayout();
    }

    private void UpdatePreviewLayout()
    {
        if (!_ready) return;
        bool native = NativeZoomOption.IsChecked == true && PreviewImage.Source is BitmapSource;
        PreviewViewport.HorizontalScrollBarVisibility = PreviewViewport.VerticalScrollBarVisibility = native ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        if (native && PreviewImage.Source is BitmapSource bitmap)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            // At 100%, one image pixel occupies one physical screen pixel, including high-DPI monitors.
            PreviewCanvas.Width = bitmap.PixelWidth / dpi.DpiScaleX + 30;
            PreviewCanvas.Height = bitmap.PixelHeight / dpi.DpiScaleY + 30;
        }
        else
        {
            double width = PreviewViewport.ViewportWidth, height = PreviewViewport.ViewportHeight;
            PreviewCanvas.Width = Math.Max(1, double.IsFinite(width) && width > 0 ? width : PreviewViewport.ActualWidth);
            PreviewCanvas.Height = Math.Max(1, double.IsFinite(height) && height > 0 ? height : PreviewViewport.ActualHeight);
        }
        PreviewImage.Stretch = ComparisonBeforeImage.Stretch = native ? Stretch.Fill : Stretch.Uniform;
        var scaling = native ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality;
        RenderOptions.SetBitmapScalingMode(PreviewImage, scaling); RenderOptions.SetBitmapScalingMode(ComparisonBeforeImage, scaling);
        UpdateComparisonClip();
    }

    private Rect DisplayedImageBounds()
    {
        if (PreviewImage.Source is not BitmapSource bitmap) return Rect.Empty;
        double width = Math.Max(1, PreviewCanvas.ActualWidth - 30), height = Math.Max(1, PreviewCanvas.ActualHeight - 30);
        if (NativeZoomOption.IsChecked == true) return new Rect(15, 15, width, height);
        double scale = Math.Min(width / bitmap.PixelWidth, height / bitmap.PixelHeight);
        double imageWidth = bitmap.PixelWidth * scale, imageHeight = bitmap.PixelHeight * scale;
        return new Rect((PreviewCanvas.ActualWidth - imageWidth) / 2, (PreviewCanvas.ActualHeight - imageHeight) / 2, imageWidth, imageHeight);
    }

    private void UpdateComparisonClip()
    {
        if (!_ready || ComparisonBeforeLayer.Visibility != Visibility.Visible) return;
        var bounds = DisplayedImageBounds();
        if (bounds.IsEmpty) return;
        double split = bounds.Left + bounds.Width * CompareSlider.Value / 100d;
        ComparisonBeforeLayer.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, split), Math.Max(0, PreviewCanvas.ActualHeight)));
        // Clip both layers so transparent source pixels cannot reveal the processed image underneath.
        ComparisonAfterLayer.Clip = new RectangleGeometry(new Rect(Math.Max(0, split), 0,
            Math.Max(0, PreviewCanvas.ActualWidth - split), Math.Max(0, PreviewCanvas.ActualHeight)));
        Canvas.SetLeft(ComparisonDivider, split - 1); Canvas.SetTop(ComparisonDivider, bounds.Top);
        ComparisonDivider.Height = bounds.Height;
    }

    private void CompareSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_ready) UpdateComparisonClip(); }
    private void PreviewViewport_SizeChanged(object sender, SizeChangedEventArgs e) { if (_ready) UpdatePreviewLayout(); }
    private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e) { if (_ready) UpdateComparisonClip(); }
    private void PreviewViewport_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_ready && (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)) UpdatePreviewLayout();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        if (_ready) UpdatePreviewLayout();
    }

    private void PreviewCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (CompareTab.IsChecked != true || _comparisonPreview is null) return;
        _draggingComparison = true; PreviewCanvas.CaptureMouse();
        MoveComparison(e.GetPosition(PreviewCanvas).X); e.Handled = true;
    }

    private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingComparison && e.LeftButton == MouseButtonState.Pressed) MoveComparison(e.GetPosition(PreviewCanvas).X);
    }

    private void MoveComparison(double x)
    {
        var bounds = DisplayedImageBounds();
        if (!bounds.IsEmpty && bounds.Width > 0) CompareSlider.Value = Math.Clamp((x - bounds.Left) / bounds.Width * 100, 0, 100);
    }

    private void PreviewCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_draggingComparison) return;
        MoveComparison(e.GetPosition(PreviewCanvas).X);
        _draggingComparison = false; PreviewCanvas.ReleaseMouseCapture(); e.Handled = true;
    }

    private void PreviewCanvas_LostMouseCapture(object sender, MouseEventArgs e) => _draggingComparison = false;
}
