using System.IO;
using System.Text.Json;

namespace PixelBatch.Core;

public sealed class GettingStartedStore(string path)
{
    private sealed record State(bool Seen);

    public bool HasSeenGuide()
    {
        try { return File.Exists(path) && JsonSerializer.Deserialize<State>(File.ReadAllText(path))?.Seen == true; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { return false; }
    }

    public void MarkSeen()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new State(true)));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
