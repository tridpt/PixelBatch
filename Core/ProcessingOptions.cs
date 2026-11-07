using System.IO;

namespace PixelBatch.Core;

public enum OutputFormat { Webp, Jpeg, Png }
public enum ResizeMode { Fit, Original, Percentage }
public enum CropAspect { None, Square, Portrait4x5, Landscape16x9, Portrait9x16, Custom }
public enum ImageRotation { None, Clockwise90, HalfTurn, CounterClockwise90 }
public enum WatermarkKind { None, Text, Image }
public enum WatermarkPosition { BottomRight, BottomLeft, TopRight, TopLeft, Center }

public sealed record ProcessingOptions
{
    public OutputFormat Format { get; init; } = OutputFormat.Webp;
    public int Quality { get; init; } = 82;
    public long TargetBytes { get; init; }
    public int MinimumQuality { get; init; } = 40;
    public ResizeMode Resize { get; init; } = ResizeMode.Fit;
    public int MaxWidth { get; init; } = 1920;
    public int MaxHeight { get; init; } = 1920;
    public int Percentage { get; init; } = 100;
    public bool AllowUpscale { get; init; }
    public ImageRotation Rotation { get; init; }
    public bool FlipHorizontal { get; init; }
    public bool FlipVertical { get; init; }
    public CropAspect Crop { get; init; }
    public int CropWidth { get; init; } = 1;
    public int CropHeight { get; init; } = 1;
    public int CropHorizontal { get; init; } = 50;
    public int CropVertical { get; init; } = 50;
    public string JpegBackground { get; init; } = "#FFFFFF";
    public WatermarkKind Watermark { get; init; }
    public string WatermarkText { get; init; } = "© PixelBatch";
    public string WatermarkImagePath { get; init; } = "";
    public WatermarkPosition Position { get; init; } = WatermarkPosition.BottomRight;
    public int Opacity { get; init; } = 65;
    public int WatermarkSize { get; init; } = 5;
    public string WatermarkColor { get; init; } = "#FFFFFF";
    public bool RepeatWatermark { get; init; }
    public int WatermarkAngle { get; init; } = -30;
    public int WatermarkSpacing { get; init; } = 100;
    public string Suffix { get; init; } = "-pixelbatch";

    public void Validate(bool requireWatermarkFile = true)
    {
        if (WatermarkText is null || WatermarkImagePath is null || Suffix is null)
            throw new ArgumentException("Thiết lập ảnh không đầy đủ.");
        if (!Enum.IsDefined(Format) || !Enum.IsDefined(Resize) || !Enum.IsDefined(Rotation) || !Enum.IsDefined(Crop) || !Enum.IsDefined(Watermark) || !Enum.IsDefined(Position))
            throw new ArgumentException("Thiết lập ảnh không hợp lệ.");
        if (Quality is < 1 or > 100) throw new ArgumentException("Chất lượng cần nằm trong khoảng 1–100.");
        if (TargetBytes != 0 && TargetBytes is < 1024 or > 104857600)
            throw new ArgumentException("Dung lượng mục tiêu cần nằm trong khoảng 1–102.400 KB (1 KB = 1.024 byte).");
        if (MinimumQuality is < 1 or > 100 || (TargetBytes > 0 && Format != OutputFormat.Png && MinimumQuality > Quality))
            throw new ArgumentException("Chất lượng tối thiểu cần từ 1 đến mức chất lượng tối đa đã chọn.");
        if (Resize == ResizeMode.Fit && (MaxWidth is < 1 or > 16000 || MaxHeight is < 1 or > 16000))
            throw new ArgumentException("Chiều rộng và chiều cao cần nằm trong khoảng 1–16.000 px.");
        if (Resize == ResizeMode.Percentage && Percentage is < 1 or > 400)
            throw new ArgumentException("Tỉ lệ cần nằm trong khoảng 1–400%.");
        if (Crop == CropAspect.Custom && (CropWidth is < 1 or > 1000 || CropHeight is < 1 or > 1000))
            throw new ArgumentException("Tỉ lệ cắt tùy chỉnh cần hai số nguyên từ 1–1.000.");
        if (CropHorizontal is < 0 or > 100 || CropVertical is < 0 or > 100)
            throw new ArgumentException("Vị trí vùng giữ cần nằm trong khoảng 0–100%.");
        if (Opacity is < 0 or > 100 || WatermarkSize is < 1 or > 20)
            throw new ArgumentException("Kích thước hoặc độ mờ watermark không hợp lệ.");
        if (WatermarkAngle is < -90 or > 90 || WatermarkSpacing is < 25 or > 300)
            throw new ArgumentException("Góc watermark cần từ -90 đến 90°, khoảng cách từ 25 đến 300%.");
        if (Watermark == WatermarkKind.Text && string.IsNullOrWhiteSpace(WatermarkText))
            throw new ArgumentException("Hãy nhập nội dung watermark.");
        if (WatermarkText.Length > 160) throw new ArgumentException("Watermark tối đa 160 ký tự.");
        if (requireWatermarkFile && Watermark == WatermarkKind.Image && !File.Exists(WatermarkImagePath))
            throw new ArgumentException("Hãy chọn ảnh logo cho watermark.");
        if (Suffix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Suffix.Length > 60)
            throw new ArgumentException("Hậu tố tên tệp không được chứa ký tự đặc biệt và tối đa 60 ký tự.");
        if (!IsHexColor(JpegBackground) || !IsHexColor(WatermarkColor))
            throw new ArgumentException("Màu cần có dạng #RRGGBB, ví dụ #FFFFFF.");
    }

    private static bool IsHexColor(string? value) => value is { Length: 7 } && value[0] == '#' &&
        value.Skip(1).All(Uri.IsHexDigit);
}

public sealed record ImageInfo(int Width, int Height, long Bytes, string Format);
public sealed record ImageGeometry(double Left, double Top, double SourceWidth, double SourceHeight, int Width, int Height);
public sealed record ProcessedImage(byte[] Bytes, int Width, int Height, string Extension, int? Quality = null, long TargetBytes = 0)
{
    public bool TargetMet => TargetBytes == 0 || Bytes.LongLength <= TargetBytes;
}
public sealed record ExportResult(string Source, string? Destination, int Width, int Height, long Bytes, string? Error, int? Quality = null);
