using System.IO;
using SkiaSharp;

namespace PixelBatch.Core;

public static class SampleArtwork
{
    public static string[] Create(string directory, bool includeLogo = false, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        string[] names = ["Vung-cao.jpg", "Bien-xanh.png", "Chieu-vang.webp"];
        for (int index = 0; index < names.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string destination = Path.Combine(directory, names[index]);
            if (File.Exists(destination)) continue;
            using var bitmap = new SKBitmap(2400, 1600, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            string[] skyColors = ["#E9C4A3", "#CAE1D3", "#E5BB8C"];
            string[] hillColors = ["#7A9E91", "#669E9A", "#B98461"];
            string[] foregroundColors = ["#2E6258", "#326A6A", "#595E48"];
            using var sky = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, 1500),
                [SKColor.Parse(skyColors[index]), SKColor.Parse("#FAEFDB")], SKShaderTileMode.Clamp) };
            canvas.DrawRect(0, 0, 2400, 1600, sky);
            using var sun = new SKPaint { Color = SKColor.Parse("#F9E5B5"), IsAntialias = true };
            canvas.DrawCircle(1740, 450, 185, sun);
            using var ridge = new SKPathBuilder(); ridge.MoveTo(0, 1070); ridge.CubicTo(350, 1100, 600, 360, 1100, 720); ridge.CubicTo(1400, 960, 1790, 630, 2400, 880); ridge.LineTo(2400, 1600); ridge.LineTo(0, 1600); ridge.Close();
            using var ridgePath = ridge.Detach();
            using var hill = new SKPaint { Color = SKColor.Parse(hillColors[index]), IsAntialias = true }; canvas.DrawPath(ridgePath, hill);
            using var second = new SKPathBuilder(); second.MoveTo(0, 1380); second.CubicTo(490, 720, 720, 1300, 1350, 1090); second.CubicTo(1940, 890, 2200, 1150, 2400, 1090); second.LineTo(2400, 1600); second.LineTo(0, 1600); second.Close();
            using var secondPath = second.Detach();
            using var foreground = new SKPaint { Color = SKColor.Parse(foregroundColors[index]), IsAntialias = true }; canvas.DrawPath(secondPath, foreground);
            using var river = new SKPaint { Color = SKColor.Parse("#E0D7AF"), IsAntialias = true };
            using var path = new SKPathBuilder(); path.MoveTo(2400, 1180); path.CubicTo(1400, 1100, 1130, 1370, 1620, 1600); path.LineTo(1960, 1600); path.CubicTo(1280, 1370, 1580, 1190, 2400, 1240); path.Close(); using var riverPath = path.Detach(); canvas.DrawPath(riverPath, river);
            var random = new Random(42 + index);
            using var texture = new SKPaint { IsAntialias = true };
            for (int dot = 0; dot < 14000; dot++)
            {
                if (dot % 1000 == 0) cancellationToken.ThrowIfCancellationRequested();
                texture.Color = (dot % 2 == 0 ? SKColors.White : SKColors.Black).WithAlpha((byte)random.Next(3, 20));
                canvas.DrawCircle(random.Next(2400), random.Next(1600), random.Next(1, 4), texture);
            }
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(index == 0 ? SKEncodedImageFormat.Jpeg : index == 1 ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Webp, index == 2 ? 96 : 100);
            WriteNewFile(destination, data.ToArray(), cancellationToken);
        }
        if (includeLogo && !File.Exists(Path.Combine(directory, "logo.png")))
        using (var bitmap = new SKBitmap(360, 160, SKColorType.Rgba8888, SKAlphaType.Premul))
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var ink = new SKPaint { Color = SKColor.Parse("#207C55"), IsAntialias = true };
            canvas.DrawRoundRect(0, 0, 360, 160, 25, 25, ink);
            using var white = new SKPaint { Color = SKColors.White, IsAntialias = true };
            using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 50);
            canvas.DrawText("STUDIO", 180, 98, SKTextAlign.Center, font, white);
            using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            WriteNewFile(Path.Combine(directory, "logo.png"), data.ToArray(), cancellationToken);
        }
        return names.Select(name => Path.Combine(directory, name)).ToArray();
    }

    private static void WriteNewFile(string path, byte[] bytes, CancellationToken token)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            token.ThrowIfCancellationRequested();
            File.WriteAllBytes(temporary, bytes);
            token.ThrowIfCancellationRequested();
            try { File.Move(temporary, path); }
            catch (IOException) when (File.Exists(path)) { }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
