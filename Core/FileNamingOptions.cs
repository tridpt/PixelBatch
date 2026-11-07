using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace PixelBatch.Core;

public enum FileNamingMode { Original, Template, Replace }

public sealed record FileNamingOptions
{
    public FileNamingMode Mode { get; init; }
    public string Template { get; init; } = "san-pham-{n:000}";
    public string Sku { get; init; } = "";
    public int StartNumber { get; init; } = 1;
    public string Find { get; init; } = "";
    public string Replacement { get; init; } = "";
    private static readonly Regex Tokens = new(@"\{(name|sku|width|height|n(?::0{1,6})?)\}", RegexOptions.CultureInvariant);

    public void Validate()
    {
        if (!Enum.IsDefined(Mode) || Template is null || Sku is null || Find is null || Replacement is null)
            throw new ArgumentException("Thiết lập tên tệp không đầy đủ.");
        if (StartNumber is < 1 or > 999999) throw new ArgumentException("Số bắt đầu cần từ 1–999.999.");
        if (Template.Length > 160 || Sku.Length > 80 || Find.Length > 160 || Replacement.Length > 160)
            throw new ArgumentException("Mẫu tên/thay chữ tối đa 160 ký tự; mã sản phẩm tối đa 80 ký tự.");
        if (Mode == FileNamingMode.Template)
        {
            string literals = Tokens.Replace(Template, "");
            if (string.IsNullOrWhiteSpace(Template) || literals.Contains('{') || literals.Contains('}'))
                throw new ArgumentException("Mẫu tên chỉ hỗ trợ {name}, {sku}, {n}, {n:000}, {width}, {height}. Số 0 để đệm tối đa 6 chữ số.");
            if (literals.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Sku.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Mẫu tên và mã sản phẩm không được chứa \\ / : * ? \" < > | hoặc ký tự điều khiển.");
            if (Template.Contains("{sku}", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(Sku))
                throw new ArgumentException("Hãy nhập mã sản phẩm để dùng {sku} trong mẫu tên.");
        }
        if (Mode == FileNamingMode.Replace && string.IsNullOrEmpty(Find))
            throw new ArgumentException("Hãy nhập phần tên cần tìm để thay thế.");
    }

    public string CreateStem(string source, ProcessingOptions options, int sourceIndex, int width, int height)
    {
        Validate();
        if (sourceIndex < 0 || sourceIndex >= 1000) throw new ArgumentException("Thứ tự ảnh nằm ngoài giới hạn 1.000 ảnh.");
        string name = Path.GetFileNameWithoutExtension(source);
        int number = StartNumber + sourceIndex;
        string stem = Mode switch
        {
            FileNamingMode.Template => Tokens.Replace(Template, match => match.Groups[1].Value switch
            {
                "name" => name, "sku" => Sku,
                "width" => width.ToString(CultureInfo.InvariantCulture), "height" => height.ToString(CultureInfo.InvariantCulture),
                "n" => number.ToString(CultureInfo.InvariantCulture),
                var token => number.ToString(token[2..], CultureInfo.InvariantCulture)
            }),
            FileNamingMode.Replace => name.Replace(Find, Replacement, StringComparison.OrdinalIgnoreCase),
            _ => name + options.Suffix
        };
        ValidateStem(stem);
        return stem;
    }

    public static void ValidateStem(string stem)
    {
        if (string.IsNullOrWhiteSpace(stem) || stem.Length > 200 || stem.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || stem is "." or ".." || stem.EndsWith(' ') || stem.EndsWith('.'))
            throw new ArgumentException("Tên ảnh xuất cần từ 1–200 ký tự, không chứa ký tự cấm và không kết thúc bằng dấu chấm/khoảng trắng.");
        string device = stem.Split('.')[0].ToUpperInvariant();
        if (device is "CON" or "PRN" or "AUX" or "NUL" || (device.Length == 4
            && (device.StartsWith("COM", StringComparison.Ordinal) || device.StartsWith("LPT", StringComparison.Ordinal)) && device[3] is >= '1' and <= '9'))
            throw new ArgumentException("Tên ảnh trùng với tên thiết bị dành riêng của Windows. Hãy chọn tên khác.");
    }
}
