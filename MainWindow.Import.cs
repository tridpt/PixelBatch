using System.Windows;
using PixelBatch.Core;

namespace PixelBatch;

public partial class MainWindow
{
    internal async Task ImportAsync(IEnumerable<string> sources)
    {
        if (_isImporting || _isExporting) return;
        string[] input = sources.ToArray();
        var existing = Files.Select(item => new SourceFile(item.FilePath, item.ImportRoot, item.RelativeDirectory)).ToArray();
        bool recursive = IncludeSubfoldersCheck.IsChecked == true;
        string output = OutputDirectoryInput.Text;
        _previewCancellation?.Cancel();
        _importCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _importCancellation.Token;
        _isImporting = true;
        UpdateInteractivity();
        BatchProgress.IsIndeterminate = true;
        int added = 0, invalid = 0;
        try
        {
            ShowStatus(recursive ? "Đang tìm ảnh trong thư mục và các thư mục con…" : "Đang tìm ảnh…");
            var scan = await Task.Run(() => SourceScanner.Scan(input, existing, recursive, output, token), token);
            foreach (var candidate in scan.Files)
            {
                token.ThrowIfCancellationRequested();
                ShowStatus($"Đang đọc ảnh {added + invalid + 1}/{scan.Files.Count} · " +
                    (candidate.RelativeDirectory.Length > 0 ? candidate.RelativeDirectory + " · " : "") + System.IO.Path.GetFileName(candidate.FilePath));
                ImageItem item;
                try
                {
                    var data = await Task.Run(() =>
                    {
                        var info = ImageProcessor.Inspect(candidate.FilePath);
                        var thumbnail = BitmapFromBytes(ImageProcessor.Thumbnail(candidate.FilePath, 300, token));
                        return (Info: info, Thumbnail: thumbnail);
                    }, token);
                    item = new ImageItem { FilePath = candidate.FilePath, ImportRoot = candidate.ImportRoot,
                        RelativeDirectory = candidate.RelativeDirectory, Info = data.Info, Thumbnail = data.Thumbnail };
                    added++;
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    item = new ImageItem { FilePath = candidate.FilePath, ImportRoot = candidate.ImportRoot, RelativeDirectory = candidate.RelativeDirectory };
                    item.SetStatus("Không đọc được", error.Message, true); invalid++;
                }
                Files.Add(item);
            }
            ShowStatus($"Đã thêm {added} ảnh" + (invalid > 0 ? $" · {invalid} tệp lỗi (rê chuột vào trạng thái để xem)" : "") +
                (scan.SkippedFiles > 0 ? $" · Bỏ qua {scan.SkippedFiles} tệp trùng, không hỗ trợ hoặc không đọc được" : "") +
                (scan.SkippedDirectories > 0 ? $" · Bỏ qua {scan.SkippedDirectories} thư mục kết quả, liên kết hoặc không đọc được" : "") +
                (scan.LimitReached ? " · Đã chạm giới hạn 1.000 ảnh; hãy xóa bớt để thêm tiếp" : "") + " · Sẵn sàng xuất");
        }
        catch (OperationCanceledException) { ShowStatus($"Đã dừng thêm ảnh · Đã thêm {added} ảnh" + (invalid > 0 ? $" · {invalid} tệp lỗi" : "")); }
        catch (Exception error) { ShowStatus("Chưa thêm được ảnh · " + error.Message, true); }
        finally
        {
            FileList.SelectedItem ??= Files.FirstOrDefault(item => item.CanProcess) ?? Files.FirstOrDefault();
            BatchProgress.IsIndeterminate = false;
            _isImporting = false;
            _importCancellation?.Dispose(); _importCancellation = null;
            UpdateCounters(); QueuePreview();
            if (_closeAfterWork) Close();
        }
    }
}
