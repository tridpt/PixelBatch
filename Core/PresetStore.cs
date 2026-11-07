using System.IO;
using System.Text.Json;

namespace PixelBatch.Core;

public sealed record SavedPreset(string Name, BatchSettings Settings);

public sealed class PresetStore(string path)
{
    public IReadOnlyList<SavedPreset> Load()
    {
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > 1_048_576) throw new InvalidDataException("Tệp preset quá lớn. Hãy kiểm tra presets.json.");
        var presets = JsonSerializer.Deserialize<SavedPreset[]>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Không đọc được danh sách preset.");
        if (presets.Length > 50) throw new InvalidDataException("Chỉ hỗ trợ tối đa 50 preset cá nhân.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var preset in presets)
        {
            if (preset?.Settings is null) throw new InvalidDataException("Preset thiếu thiết lập xuất ảnh.");
            ValidateName(preset.Name);
            if (!names.Add(preset.Name.Trim())) throw new InvalidDataException("Tệp preset có tên trùng nhau.");
            preset.Settings.Validate(requireWatermarkFile: false);
        }
        return Sort(presets);
    }

    public IReadOnlyList<SavedPreset> Save(string name, BatchSettings settings)
    {
        ValidateName(name);
        settings.Validate();
        var presets = Load().ToList();
        name = name.Trim();
        int index = presets.FindIndex(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
        var saved = new SavedPreset(name, settings);
        if (index >= 0) presets[index] = saved;
        else
        {
            if (presets.Count >= 50) throw new ArgumentException("Bạn đã lưu 50 preset. Hãy cập nhật hoặc xóa một preset cũ.");
            presets.Add(saved);
        }
        var sorted = Sort(presets);
        Write(sorted);
        return sorted;
    }

    public IReadOnlyList<SavedPreset> Delete(string name)
    {
        var presets = Sort(Load().Where(preset => !string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase)));
        Write(presets);
        return presets;
    }

    private static void ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 48 || name.Any(char.IsControl))
            throw new ArgumentException("Tên preset cần từ 1–48 ký tự và không có ký tự xuống dòng.");
    }

    private static SavedPreset[] Sort(IEnumerable<SavedPreset> presets) => presets.OrderBy(preset => preset.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();

    private void Write(IReadOnlyList<SavedPreset> presets)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(presets, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, fullPath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
