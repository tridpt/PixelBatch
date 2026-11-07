using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using PixelBatch.Core;

namespace PixelBatch;

public partial class MainWindow
{
    private async Task RunGettingStartedUiChecksAsync(string directory, Action<bool, string> Check)
    {
        string statePath = Path.Combine(directory, "getting-started", "state.json");
        string previousSamples = _sampleDirectory;
        var previousStore = _gettingStartedStore;
        var previousSettings = ReadBatchSettings();
        string previousOutput = OutputDirectoryInput.Text;
        double previousWidth = Width, previousHeight = Height;
        _sampleDirectory = Path.Combine(directory, "getting-started", "samples");
        _gettingStartedStore = new GettingStartedStore(statePath);
        try
        {
            ShowGettingStartedIfNeeded(launchedWithFiles: true);
            Check(!_guideOpen && !File.Exists(statePath), "File-argument startup skips the first-use guide without marking it seen");
            ShowGettingStartedIfNeeded(launchedWithFiles: false);
            Check(_guideOpen && GuideOverlay.Visibility == Visibility.Visible, "First startup displays the three-step guide");
            Check(!HeaderPanel.IsEnabled && !WorkspacePanel.IsEnabled && !FooterPanel.IsEnabled
                && !AddFilesCommand.CanExecute(null) && !ExportCommand.CanExecute(null), "Guide blocks workspace actions and keyboard shortcuts");
            Check(KeyboardNavigation.GetTabNavigation(GuideOverlay) == KeyboardNavigationMode.Cycle, "Guide keeps keyboard tab navigation inside the card");
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            App.RenderElement(RootGrid, Path.Combine(directory, "getting-started.png"));
            Width = 1100; Height = 760;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            App.RenderElement(RootGrid, Path.Combine(directory, "getting-started-compact.png"));
            var buttonPosition = GuideSampleButton.TransformToAncestor(RootGrid).Transform(new Point());
            Check(buttonPosition.Y >= 48 && buttonPosition.Y + GuideSampleButton.ActualHeight <= RootGrid.ActualHeight,
                "Compact window keeps the sample action visible");
            GuideCloseButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Check(!_guideOpen && HeaderPanel.IsEnabled && WorkspacePanel.IsEnabled && FooterPanel.IsEnabled,
                "Closing the guide restores the workspace");
            Check(new GettingStartedStore(statePath).HasSeenGuide(), "Guide completion persists independently of processing settings");
            ShowGettingStartedIfNeeded(launchedWithFiles: false);
            Check(!_guideOpen, "Later startups skip a completed guide");
            ShowGuideCommand.Execute(null);
            Check(_guideOpen, "F1 guide command reopens completed onboarding");
            var escape = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), 0, Key.Escape)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            GuideOverlay.RaiseEvent(escape);
            Check(escape.Handled && !_guideOpen, "Escape dismisses the guide");
            ShowGuide_Click(this, new RoutedEventArgs());
            await AddSamplePhotosAsync(); await _previewTask;
            Check(!_guideOpen && Files.Count == 3 && Files.All(item => item.CanProcess), "Sample action imports three valid formats and closes the guide");
            Check(_resultPreview is not null && ExportButton.IsEnabled, "Sample images provide a real preview and can be exported");
            Check(JsonSerializer.Serialize(previousSettings) == JsonSerializer.Serialize(ReadBatchSettings())
                && OutputDirectoryInput.Text == previousOutput, "Sample import preserves processing settings and output location");
            var originalItems = Files.ToArray();
            var hashes = Files.ToDictionary(item => item.FilePath, item => SHA256.HashData(File.ReadAllBytes(item.FilePath)));
            await AddSamplePhotosAsync(); await _previewTask;
            Check(Files.SequenceEqual(originalItems), "Repeated sample actions keep existing queue entries without duplicates");
            OutputDirectoryInput.Text = Path.Combine(directory, "getting-started", "output");
            await ExportAsync(); await _previewTask;
            Check(Files.All(item => item.LastOutput is not null && File.Exists(item.LastOutput)), "First-use sample workflow exports all three images");
            Check(hashes.All(pair => pair.Value.SequenceEqual(SHA256.HashData(File.ReadAllBytes(pair.Key)))), "Trying and exporting samples leaves their source files unchanged");
            Check(Directory.GetFiles(_sampleDirectory, "*.tmp").Length == 0, "Sample generation leaves no temporary files");
            string ownImage = Path.Combine(directory, "getting-started", "own-image.png");
            File.Copy(Files.Single(item => item.FilePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).FilePath, ownImage);
            await ImportAsync([ownImage]); await _previewTask;
            var mixedItems = Files.ToArray();
            ShowGuide(); await AddSamplePhotosAsync(); await _previewTask;
            Check(Files.Count == 4 && Files.SequenceEqual(mixedItems), "Trying samples with a personal image queued preserves every existing row");
            _isImporting = true; UpdateInteractivity();
            ShowGuide();
            Check(!_guideOpen && !GuideButton.IsEnabled && !ShowGuideCommand.CanExecute(null), "Busy import blocks onboarding until work finishes");
            _isImporting = false; UpdateInteractivity();
            File.WriteAllText(statePath, "invalid state");
            Check(!new GettingStartedStore(statePath).HasSeenGuide(), "Corrupt onboarding state safely offers the guide again");
            _gettingStartedStore.MarkSeen();
            Check(new GettingStartedStore(statePath).HasSeenGuide(), "Onboarding state can recover after corruption");
        }
        finally
        {
            _isImporting = false;
            if (_guideOpen) DismissGuide();
            Clear_Click(this, new RoutedEventArgs());
            _gettingStartedStore = previousStore;
            _sampleDirectory = previousSamples;
            OutputDirectoryInput.Text = previousOutput;
            Width = previousWidth; Height = previousHeight;
            ApplyBatchSettings(previousSettings); await _previewTask;
        }
    }
}
