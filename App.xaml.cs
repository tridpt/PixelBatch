using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PixelBatch;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi-VN");
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--self-test")
        {
            RunDiagnostics(Path.GetFullPath(e.Args[1])); return;
        }
        var window = new MainWindow();
        MainWindow = window;
        var area = SystemParameters.WorkArea;
        window.MinWidth = Math.Min(window.MinWidth, Math.Max(900, area.Width - 30));
        window.MinHeight = Math.Min(window.MinHeight, Math.Max(650, area.Height - 30));
        window.Width = Math.Min(window.Width, area.Width - 30);
        window.Height = Math.Min(window.Height, area.Height - 30);
        window.Show();
        window.ShowGettingStartedIfNeeded(e.Args.Length > 0);
        if (e.Args.Length > 0) _ = window.ImportAsync(e.Args);
    }

    private void RunDiagnostics(string directory)
    {
        Directory.CreateDirectory(directory);
        var window = new MainWindow(true)
        {
            ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000
        };
        MainWindow = window;
        window.Loaded += async (_, _) =>
        {
            try
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                var checks = await window.RunUiChecksAsync(directory);
                File.WriteAllText(Path.Combine(directory, "ui-report.json"), JsonSerializer.Serialize(new { passed = true, checks }, new JsonSerializerOptions { WriteIndented = true }));
                Shutdown(0);
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(directory, "ui-report.json"), JsonSerializer.Serialize(new { passed = false, error = exception.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
                Shutdown(1);
            }
        };
        window.Show();
    }

    internal static void RenderElement(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        var image = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        image.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path); encoder.Save(file);
    }
}
