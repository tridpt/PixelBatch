using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelBatch.Core;

namespace PixelBatch;

public sealed class ImageItem : INotifyPropertyChanged
{
    public required string FilePath { get; init; }
    public string Name => Path.GetFileName(FilePath);
    public string ImportRoot { get; init; } = "";
    public string RelativeDirectory { get; init; } = "";
    public string DisplayPath => RelativeDirectory.Length == 0 ? Name : Path.Combine(RelativeDirectory, Name);
    public ImageInfo? Info { get; init; }
    public BitmapSource? Thumbnail { get; init; }
    public bool CanProcess => Info is not null;
    public string Details => Info is null ? "Không thể đọc ảnh" : $"{Info.Width:N0} × {Info.Height:N0} · {FormatBytes(Info.Bytes)}";
    public string? LastOutput { get; set; }
    internal List<ExportSlot> ExportSlots { get; } = [];
    private string _status = "Sẵn sàng";
    private string _statusDetail = "";
    private bool _error;
    public string Status => _status;
    public string StatusDetail => _statusDetail;
    public Brush StatusBrush => _error ? Brushes.IndianRed : _status.StartsWith("Đã xuất", StringComparison.Ordinal) ? new SolidColorBrush(Color.FromRgb(32, 124, 85)) : new SolidColorBrush(Color.FromRgb(117, 132, 113));
    public event PropertyChangedEventHandler? PropertyChanged;
    public void SetStatus(string value, string detail = "", bool error = false)
    {
        _status = value; _statusDetail = detail.Length == 0 ? value : detail; _error = error;
        OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(StatusDetail)); OnPropertyChanged(nameof(StatusBrush));
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes:N0} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.#} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024d * 1024):0.##} MB";
        return $"{bytes / (1024d * 1024 * 1024):0.##} GB";
    }
}
