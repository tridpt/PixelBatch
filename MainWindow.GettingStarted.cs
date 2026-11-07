using System.IO;
using System.Windows;
using System.Windows.Input;
using PixelBatch.Core;

namespace PixelBatch;

public partial class MainWindow
{
    public ICommand ShowGuideCommand { get; }
    private bool _guideOpen;
    private IInputElement? _focusBeforeGuide;
    private GettingStartedStore? _gettingStartedStore;
    private string _sampleDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PixelBatch", "Samples", "v1");

    internal void ShowGettingStartedIfNeeded(bool launchedWithFiles)
    {
        if (!launchedWithFiles && _gettingStartedStore?.HasSeenGuide() == false) ShowGuide();
    }

    private void ShowGuide_Click(object sender, RoutedEventArgs e) => ShowGuide();
    private void ShowGuide()
    {
        if (_isImporting || _isExporting) return;
        if (!_guideOpen) _focusBeforeGuide = Keyboard.FocusedElement;
        _guideOpen = true;
        DropOverlay.Visibility = Visibility.Collapsed;
        GuideOverlay.Visibility = Visibility.Visible;
        UpdateInteractivity();
        GuideSampleButton.Focus();
    }

    private void DismissGuide()
    {
        _guideOpen = false;
        GuideOverlay.Visibility = Visibility.Collapsed;
        try { _gettingStartedStore?.MarkSeen(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { ShowStatus("Chưa ghi nhớ được hướng dẫn · " + exception.Message, true); }
        UpdateInteractivity();
        if (_focusBeforeGuide is UIElement { IsEnabled: true, IsVisible: true } element) element.Focus();
        else GuideButton.Focus();
    }

    private void CloseGuide_Click(object sender, RoutedEventArgs e) => DismissGuide();
    private void GuideOwnPhotos_Click(object sender, RoutedEventArgs e)
    {
        DismissGuide();
        AddFiles();
    }

    private async void GuideSample_Click(object sender, RoutedEventArgs e) => await AddSamplePhotosAsync();

    private async Task AddSamplePhotosAsync()
    {
        if (_isImporting || _isExporting) return;
        if (_guideOpen) DismissGuide();
        _isImporting = true;
        _importCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _importCancellation.Token;
        UpdateInteractivity();
        BatchProgress.IsIndeterminate = true;
        string[] sources = [];
        try
        {
            ShowStatus("Đang chuẩn bị 3 ảnh mẫu…");
            sources = await Task.Run(() => SampleArtwork.Create(_sampleDirectory, cancellationToken: token), token);
        }
        catch (OperationCanceledException) { ShowStatus("Đã dừng thêm ảnh mẫu."); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        { ShowStatus("Chưa tạo được ảnh mẫu · " + exception.Message, true); }
        finally
        {
            if (token.IsCancellationRequested) sources = [];
            _isImporting = false;
            _importCancellation.Dispose(); _importCancellation = null;
            BatchProgress.IsIndeterminate = false;
            UpdateInteractivity();
            if (_closeAfterWork) Close();
        }
        if (sources.Length == 0 || _lifetime.IsCancellationRequested) return;
        await ImportAsync(sources);
        if (!_lifetime.IsCancellationRequested && sources.All(path => Files.Any(item => item.FilePath == path && item.CanProcess)))
            ShowStatus("Đã thêm ảnh mẫu · Chọn Cân bằng, xem So sánh rồi bấm Xuất ảnh. Ảnh và thiết lập hiện có được giữ nguyên.");
    }

    private void Guide_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        DismissGuide(); e.Handled = true;
    }
}
