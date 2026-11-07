namespace PixelBatch.Core;

// One output of one source. Keep its original sequence and size when retrying a partial batch.
public sealed class ExportSlot(int sourceIndex, OutputSize? size)
{
    public int SourceIndex { get; } = sourceIndex;
    public OutputSize? Size { get; } = size;
    public ExportResult? Result { get; set; }
    public bool Failed => Result?.Error is not null;

    public OutputVariant GetVariant(ProcessingOptions options) => Size is { } size
        ? new OutputVariant(size.FolderName, options with { Resize = ResizeMode.Fit, MaxWidth = size.Width, MaxHeight = size.Height })
        : new OutputVariant("", options);
}
