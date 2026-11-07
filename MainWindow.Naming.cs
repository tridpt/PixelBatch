using System.Windows;
using System.IO;
using PixelBatch.Core;

namespace PixelBatch;

public partial class MainWindow
{
    private FileNamingOptions ReadNamingOptions()
    {
        bool template = NamingCombo.SelectedIndex == 1;
        int start = 1;
        if (template && !int.TryParse(StartNumberInput.Text.Trim(), out start))
            throw new ArgumentException("Số thứ tự bắt đầu cần là số nguyên.");
        return new FileNamingOptions
        {
            Mode = (FileNamingMode)NamingCombo.SelectedIndex,
            Template = NameTemplateInput.Text, Sku = SkuInput.Text, StartNumber = start,
            Find = FindNameInput.Text, Replacement = ReplaceNameInput.Text
        };
    }

    private void ApplyNamingOptions(FileNamingOptions naming)
    {
        NamingCombo.SelectedIndex = (int)naming.Mode;
        NameTemplateInput.Text = naming.Template; SkuInput.Text = naming.Sku;
        StartNumberInput.Text = naming.StartNumber.ToString();
        FindNameInput.Text = naming.Find; ReplaceNameInput.Text = naming.Replacement;
    }

    private void RefreshNamingVisibility()
    {
        SuffixPanel.Visibility = NamingCombo.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        TemplateNamingPanel.Visibility = NamingCombo.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        ReplaceNamingPanel.Visibility = NamingCombo.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateOutputNamePreview(OutputVariant variant, FileNamingOptions naming, bool preserveFolders)
    {
        if (_selected?.Info is not { } info) return;
        int index = Files.Where(item => item.CanProcess).TakeWhile(item => !ReferenceEquals(item, _selected)).Count();
        var (width, height) = ImageProcessor.CalculateSize(info.Width, info.Height, variant.Options);
        string stem = naming.CreateStem(_selected.FilePath, variant.Options, index, width, height);
        string extension = variant.Options.Format switch { OutputFormat.Jpeg => ".jpg", OutputFormat.Png => ".png", _ => ".webp" };
        OutputNameLabel.Text = preserveFolders
            ? Path.Combine(SourceLayout.OutputFolder(variant.Folder, _selected.RelativeDirectory, true), stem + extension)
            : stem + extension;
    }
}
