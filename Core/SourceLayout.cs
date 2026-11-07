using System.IO;

namespace PixelBatch.Core;

public static class SourceLayout
{
    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    public static bool IsWithin(string path, string parent)
    {
        path = Normalize(path); parent = Normalize(parent);
        string prefix = Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar;
        return path.Equals(parent, StringComparison.OrdinalIgnoreCase) || path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static string OutputFolder(string sizeFolder, string sourceFolder, bool preserveFolders)
    {
        ValidateRelative(sizeFolder);
        if (preserveFolders) ValidateRelative(sourceFolder);
        return preserveFolders ? Path.Combine(sizeFolder, sourceFolder) : sizeFolder;
    }

    public static string ResolveOutput(string outputRoot, string relativeFolder)
    {
        ValidateRelative(relativeFolder);
        string root = Normalize(outputRoot);
        string result = Normalize(Path.Combine(root, relativeFolder));
        if (!IsWithin(result, root)) throw new ArgumentException("Thư mục ảnh xuất phải nằm trong thư mục lưu đã chọn.");
        return result;
    }

    public static void ValidateRelative(string path)
    {
        if (path is null || Path.IsPathRooted(path)) throw new ArgumentException("Đường dẫn thư mục ảnh xuất không hợp lệ.");
        if (path.Length == 0) return;
        foreach (string part in path.Split(['\\', '/']))
            if (string.IsNullOrWhiteSpace(part) || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ')
                || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Tên thư mục ảnh xuất không hợp lệ.");
    }
}
