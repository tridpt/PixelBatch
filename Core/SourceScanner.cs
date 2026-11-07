using System.IO;

namespace PixelBatch.Core;

public sealed record SourceFile(string FilePath, string ImportRoot = "", string RelativeDirectory = "");
public sealed record SourceScanResult(IReadOnlyList<SourceFile> Files, int SkippedFiles, int SkippedDirectories, bool LimitReached);

public static class SourceScanner
{
    public const int MaximumFiles = 1000;

    public static SourceScanResult Scan(IEnumerable<string> sources, IReadOnlyList<SourceFile> existing, bool includeSubfolders,
        string? outputDirectory = null, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (existing.Count >= MaximumFiles) return new([], 0, 0, true);
        var files = new List<SourceFile>();
        var known = new HashSet<string>(existing.Select(file => SourceLayout.Normalize(file.FilePath)), StringComparer.OrdinalIgnoreCase);
        var roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rootNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in existing.Where(file => file.ImportRoot.Length > 0))
        {
            string name = file.RelativeDirectory.Split(['\\', '/'])[0];
            roots.TryAdd(SourceLayout.Normalize(file.ImportRoot), name);
            rootNames.Add(name);
        }
        var seenRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? output = null;
        try { if (!string.IsNullOrWhiteSpace(outputDirectory)) output = SourceLayout.Normalize(outputDirectory); }
        catch (Exception error) when (IsPathError(error)) { }
        int skippedFiles = 0, skippedDirectories = 0;
        bool limit = false;

        string RootName(string root)
        {
            if (roots.TryGetValue(root, out string? existingName)) return existingName;
            string name = new DirectoryInfo(root).Name;
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || string.IsNullOrWhiteSpace(name))
                name = "O-" + root.TrimEnd('\\', '/').Replace(":", "").Replace('\\', '-').Replace('/', '-');
            name = name.TrimEnd('.', ' ');
            if (name.Length > 160) name = name[..160].TrimEnd('.', ' ');
            if (name.Length == 0) name = "Anh";
            string unique = name;
            for (int number = 2; !rootNames.Add(unique); number++) unique = $"{name} ({number})";
            roots.Add(root, unique);
            return unique;
        }

        bool AddFile(string path, string root, string rootName)
        {
            if (!ImageProcessor.SupportedExtensions.Contains(Path.GetExtension(path)) || !known.Add(path)) { skippedFiles++; return true; }
            if (existing.Count + files.Count >= MaximumFiles) { limit = true; return false; }
            string folder = root.Length == 0 ? "" : Path.GetDirectoryName(Path.GetRelativePath(root, path)) ?? "";
            string relative = root.Length == 0 ? "" : Path.Combine(rootName, folder);
            SourceLayout.ValidateRelative(relative);
            files.Add(new(path, root, relative));
            return true;
        }

        foreach (string source in sources)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                string full = SourceLayout.Normalize(source);
                var attributes = File.GetAttributes(full);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    if ((attributes & FileAttributes.Directory) != 0) skippedDirectories++; else skippedFiles++;
                    continue;
                }
                if ((attributes & FileAttributes.Directory) == 0)
                {
                    if (!AddFile(full, "", "")) break;
                    continue;
                }
                if (!seenRoots.Add(full)) { skippedFiles++; continue; }
                string name = RootName(full);
                // Exclude previous outputs only when they are a descendant of this selected input root.
                string? excluded = output is not null && !output.Equals(full, StringComparison.OrdinalIgnoreCase)
                    && SourceLayout.IsWithin(output, full) ? output : null;
                var stack = new Stack<IEnumerator<string>>();
                stack.Push(Directory.EnumerateFileSystemEntries(full).GetEnumerator());
                try
                {
                    while (stack.Count > 0 && !limit)
                    {
                        token.ThrowIfCancellationRequested();
                        var iterator = stack.Peek();
                        bool next;
                        try { next = iterator.MoveNext(); }
                        catch (Exception error) when (IsPathError(error))
                        { skippedDirectories++; stack.Pop().Dispose(); continue; }
                        if (!next) { stack.Pop().Dispose(); continue; }
                        string path = SourceLayout.Normalize(iterator.Current);
                        try
                        {
                            var entryAttributes = File.GetAttributes(path);
                            bool directory = (entryAttributes & FileAttributes.Directory) != 0;
                            if (directory)
                            {
                                if (!includeSubfolders) continue;
                                if ((entryAttributes & FileAttributes.ReparsePoint) != 0 || (excluded is not null && SourceLayout.IsWithin(path, excluded)))
                                { skippedDirectories++; continue; }
                                stack.Push(Directory.EnumerateFileSystemEntries(path).GetEnumerator());
                            }
                            else if ((entryAttributes & FileAttributes.ReparsePoint) != 0) skippedFiles++;
                            else if (!AddFile(path, full, name)) break;
                        }
                        catch (Exception error) when (IsPathError(error)) { skippedFiles++; }
                    }
                }
                finally { while (stack.TryPop(out var iterator)) iterator.Dispose(); }
                if (limit) break;
            }
            catch (Exception error) when (IsPathError(error)) { skippedFiles++; }
        }
        return new(files, skippedFiles, skippedDirectories, limit);
    }

    private static bool IsPathError(Exception error) => error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}
