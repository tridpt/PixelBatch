using System.Text.Json.Serialization;

namespace PixelBatch.Core;

public sealed record OutputSize(int Width, int Height)
{
    [JsonIgnore] public string FolderName => $"{Width}x{Height}";
    [JsonIgnore] public string Label => $"{Width:N0} × {Height:N0} px";

    public void Validate()
    {
        if (Width is < 1 or > 16000 || Height is < 1 or > 16000 || (long)Width * Height > ImageProcessor.MaximumPixels)
            throw new ArgumentException("Mỗi cỡ ảnh cần từ 1–16.000 px mỗi cạnh, tối đa 80 megapixel.");
    }
}

public sealed record OutputVariant(string Folder, ProcessingOptions Options);

public sealed record BatchSettings
{
    public ProcessingOptions Options { get; init; } = new();
    public bool MultipleSizes { get; init; }
    public OutputSize[] Sizes { get; init; } = [new(800, 800), new(1600, 1600), new(2400, 2400)];
    public bool Zip { get; init; }
    public bool PreserveFolders { get; init; }
    public FileNamingOptions Naming { get; init; } = new();

    public void Validate(bool requireWatermarkFile = true)
    {
        if (Options is null || Sizes is null || Naming is null) throw new ArgumentException("Thiết lập xuất ảnh không đầy đủ.");
        Options.Validate(requireWatermarkFile);
        Naming.Validate();
        if (Sizes.Length > 6 || (MultipleSizes && Sizes.Length == 0))
            throw new ArgumentException("Hãy chọn từ 1 đến 6 kích thước để xuất.");
        foreach (var size in Sizes)
        {
            if (size is null) throw new ArgumentException("Cỡ ảnh không hợp lệ.");
            size.Validate();
        }
        if (Sizes.Distinct().Count() != Sizes.Length) throw new ArgumentException("Các kích thước xuất không được trùng nhau.");
    }

    public IReadOnlyList<OutputVariant> GetVariants()
    {
        Validate();
        return MultipleSizes
            ? Sizes.Select(size => new OutputVariant(size.FolderName, Options with
                { Resize = ResizeMode.Fit, MaxWidth = size.Width, MaxHeight = size.Height })).ToArray()
            : [new OutputVariant("", Options)];
    }
}
