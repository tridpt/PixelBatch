using System.IO;
using System.IO.Compression;

namespace PixelBatch.Core;

public static class BatchExporter
{
    public static ExportResult ExportOne(string source, string directory, ProcessingOptions options, CancellationToken token = default,
        FileNamingOptions? naming = null, int sourceIndex = 0)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            var result = ImageProcessor.Process(source, options, token);
            if (!result.TargetMet)
            {
                string advice = options.Format == OutputFormat.Png ? "Hãy giảm kích thước hoặc chuyển sang JPG/WebP."
                    : "Hãy giảm kích thước, hạ chất lượng tối thiểu hoặc tăng dung lượng mục tiêu.";
                return new ExportResult(source, null, result.Width, result.Height, result.Bytes.LongLength,
                    $"Chưa đạt mục tiêu {options.TargetBytes:N0} byte; kết quả ở mức đã thử là {result.Bytes.LongLength:N0} byte. {advice} Ảnh này chưa được lưu.", result.Quality);
            }
            Directory.CreateDirectory(directory);
            string stem = (naming ?? new FileNamingOptions()).CreateStem(source, options, sourceIndex, result.Width, result.Height);
            string output = WriteUnique(directory, stem, result.Extension, result.Bytes, token);
            return new ExportResult(source, output, result.Width, result.Height, result.Bytes.LongLength, null, result.Quality);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            return new ExportResult(source, null, 0, 0, 0, exception.Message);
        }
    }

    public static string WriteUnique(string directory, string stem, string extension, byte[] bytes, CancellationToken token = default)
    {
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".pixelbatch-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                token.ThrowIfCancellationRequested();
                stream.Write(bytes);
            }
            for (int index = 1; index < 10000; index++)
            {
                token.ThrowIfCancellationRequested();
                string candidate = Path.Combine(directory, stem + (index == 1 ? "" : $" ({index})") + extension);
                try { File.Move(temporary, candidate, false); return candidate; }
                catch (IOException) when (File.Exists(candidate)) { }
            }
            throw new IOException("Có quá nhiều tệp trùng tên trong thư mục đích.");
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static string CreateZip(string directory, IReadOnlyList<string> paths, CancellationToken token = default)
    {
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".pixelbatch-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
                foreach (string path in paths)
                {
                    token.ThrowIfCancellationRequested();
                    string relative = Path.GetRelativePath(directory, path);
                    if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                        throw new ArgumentException("Tệp ZIP chỉ chứa ảnh nằm trong thư mục kết quả.");
                    archive.CreateEntryFromFile(path, relative.Replace('\\', '/'), CompressionLevel.NoCompression);
                }
            token.ThrowIfCancellationRequested();
            string stem = "PixelBatch-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            for (int index = 1; index < 10000; index++)
            {
                string candidate = Path.Combine(directory, stem + (index == 1 ? "" : $" ({index})") + ".zip");
                try { File.Move(temporary, candidate, false); return candidate; }
                catch (IOException) when (File.Exists(candidate)) { }
            }
            throw new IOException("Không tạo được tên tệp ZIP duy nhất.");
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
