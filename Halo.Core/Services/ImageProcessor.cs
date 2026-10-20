using System;
using System.IO;
using SkiaSharp;

namespace Halo.Core.Services;

public static class ImageProcessor
{
    private static readonly float[] GrayscaleColorMatrix =
    {
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0,       0,       0,       1, 0
    };

    public static string ConvertFormat(string inputPath, SKEncodedImageFormat format, int quality = 90, string? customOutputName = null)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        using var bitmap = SKBitmap.Decode(inputPath);
        if (bitmap == null)
            throw new InvalidOperationException("Неможливо декодувати зображення.");

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = format switch
        {
            SKEncodedImageFormat.Webp => ".webp",
            SKEncodedImageFormat.Png => ".png",
            SKEncodedImageFormat.Jpeg => ".jpg",
            _ => ".png"
        };

        var finalName = customOutputName ?? $"{nameWithoutExt}_converted{ext}";
        var outputPath = GetUniqueFilePath(Path.Combine(dir, finalName));

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    public static string ResizeImage(string inputPath, double scaleFactor)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        using var bitmap = SKBitmap.Decode(inputPath);
        if (bitmap == null)
            throw new InvalidOperationException("Неможливо декодувати зображення.");

        int newWidth = Math.Max(1, (int)(bitmap.Width * scaleFactor));
        int newHeight = Math.Max(1, (int)(bitmap.Height * scaleFactor));

        var info = new SKImageInfo(newWidth, newHeight, bitmap.ColorType, bitmap.AlphaType);
        using var resized = bitmap.Resize(info, SKSamplingOptions.Default);
        if (resized == null)
            throw new InvalidOperationException("Помилка зміни розміру зображення.");

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);

        var percent = (int)(scaleFactor * 100);
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}_{percent}pct{ext}"));

        var format = ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".webp" => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Png
        };

        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(format, 90);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    public static string ApplyGrayscale(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        using var original = SKBitmap.Decode(inputPath);
        if (original == null)
            throw new InvalidOperationException("Неможливо декодувати зображення.");

        var grayBitmap = new SKBitmap(original.Width, original.Height, original.ColorType, original.AlphaType);
        using (var canvas = new SKCanvas(grayBitmap))
        using (var paint = new SKPaint())
        using (var filter = SKColorFilter.CreateColorMatrix(GrayscaleColorMatrix))
        using (var origImage = SKImage.FromBitmap(original))
        {
            paint.ColorFilter = filter;
            canvas.DrawImage(origImage, 0, 0, SKSamplingOptions.Default, paint);
        }

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);

        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}_gray{ext}"));

        var format = ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".webp" => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Png
        };

        using var image = SKImage.FromBitmap(grayBitmap);
        using var data = image.Encode(format, 92);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    public static string CompressImage(string inputPath, int quality = 75)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        using var bitmap = SKBitmap.Decode(inputPath);
        if (bitmap == null)
            throw new InvalidOperationException("Неможливо декодувати зображення.");

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);

        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}_compressed{ext}"));

        var format = ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".webp" => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Png
        };

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    private static string GetUniqueFilePath(string candidate)
    {
        if (!File.Exists(candidate))
            return candidate;

        var dir = Path.GetDirectoryName(candidate) ?? "";
        var name = Path.GetFileNameWithoutExtension(candidate);
        var ext = Path.GetExtension(candidate);
        int counter = 1;

        while (true)
        {
            var testPath = Path.Combine(dir, $"{name} ({counter}){ext}");
            if (!File.Exists(testPath))
                return testPath;
            counter++;
        }
    }
}
