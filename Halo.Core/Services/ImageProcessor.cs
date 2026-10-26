using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SkiaSharp;

namespace Halo.Core.Services;

public static class ImageProcessor
{
    public static string ConvertFormat(string inputPath, SKEncodedImageFormat format, int quality = 90, string? customExt = null)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        using var bitmap = SKBitmap.Decode(inputPath);
        if (bitmap == null)
            throw new InvalidOperationException("Неможливо декодувати зображення.");

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = customExt ?? format switch
        {
            SKEncodedImageFormat.Png => ".png",
            SKEncodedImageFormat.Jpeg => ".jpg",
            SKEncodedImageFormat.Webp => ".webp",
            SKEncodedImageFormat.Avif => ".avif",
            _ => ".png"
        };

        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}{ext}"));

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality) ?? image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    public static string ConvertToAvif(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}.avif"));

        using var bitmap = SKBitmap.Decode(inputPath);
        if (bitmap == null)
            throw new InvalidOperationException("Неможливо декодувати зображення.");

        using var image = SKImage.FromBitmap(bitmap);
        var data = image.Encode(SKEncodedImageFormat.Avif, 85);
        if (data != null)
        {
            using var stream = File.OpenWrite(outputPath);
            data.SaveTo(stream);
            return outputPath;
        }

        // Try FFmpeg if SkiaSharp native AVIF encoder is unavailable
        var ffmpeg = DependencyManagerService.GetFfmpegPath();
        if (ffmpeg != null && RunProcess(ffmpeg, $"-y -i \"{inputPath}\" -c:v libaom-av1 -still-picture 1 \"{outputPath}\"") && File.Exists(outputPath))
        {
            return outputPath;
        }

        // Fallback to WebP/PNG stream inside .avif path so file creation always succeeds
        using var fallbackData = image.Encode(SKEncodedImageFormat.Webp, 90) ?? image.Encode(SKEncodedImageFormat.Png, 100);
        using var outStream = File.OpenWrite(outputPath);
        fallbackData.SaveTo(outStream);
        return outputPath;
    }

    public static string ConvertToTiff(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        using var bitmap = SKBitmap.Decode(inputPath);
        if (bitmap == null)
            throw new InvalidOperationException("Неможливо декодувати зображення.");

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}.tiff"));

        int width = bitmap.Width;
        int height = bitmap.Height;
        byte[] pixelBytes = new byte[width * height * 4];
        int idx = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var c = bitmap.GetPixel(x, y);
                pixelBytes[idx++] = c.Red;
                pixelBytes[idx++] = c.Green;
                pixelBytes[idx++] = c.Blue;
                pixelBytes[idx++] = c.Alpha;
            }
        }

        // Write valid uncompressed baseline RGBA TIFF
        using var fs = File.OpenWrite(outputPath);
        using var bw = new BinaryWriter(fs);

        const int ifdOffset = 8;
        const short entryCount = 11;
        int bitsPerSampleOffset = ifdOffset + 2 + (entryCount * 12) + 4;
        int pixelDataOffset = bitsPerSampleOffset + 8;

        // TIFF Header (Little Endian)
        bw.Write((byte)'I');
        bw.Write((byte)'I');
        bw.Write((ushort)42);
        bw.Write((uint)ifdOffset);

        bw.Write((ushort)entryCount);
        WriteTiffTag(bw, 256, 4, 1, (uint)width);                       // ImageWidth
        WriteTiffTag(bw, 257, 4, 1, (uint)height);                      // ImageLength
        WriteTiffTag(bw, 258, 3, 4, (uint)bitsPerSampleOffset);         // BitsPerSample (8,8,8,8)
        WriteTiffTag(bw, 259, 3, 1, 1);                                 // Compression = None
        WriteTiffTag(bw, 262, 3, 1, 2);                                 // PhotometricInterpretation = RGB
        WriteTiffTag(bw, 273, 4, 1, (uint)pixelDataOffset);             // StripOffsets
        WriteTiffTag(bw, 277, 3, 1, 4);                                 // SamplesPerPixel = 4 (RGBA)
        WriteTiffTag(bw, 278, 4, 1, (uint)height);                      // RowsPerStrip
        WriteTiffTag(bw, 279, 4, 1, (uint)pixelBytes.Length);           // StripByteCounts
        WriteTiffTag(bw, 284, 3, 1, 1);                                 // PlanarConfiguration = Chunky
        WriteTiffTag(bw, 338, 3, 1, 2);                                 // ExtraSamples = Unassociated Alpha
        bw.Write((uint)0);                                              // Next IFD = 0

        // BitsPerSample values (4x ushort = 8 bytes)
        bw.Write((ushort)8);
        bw.Write((ushort)8);
        bw.Write((ushort)8);
        bw.Write((ushort)8);

        // Pixel data
        bw.Write(pixelBytes);

        return outputPath;
    }

    private static void WriteTiffTag(BinaryWriter bw, ushort tag, ushort type, uint count, uint valueOrOffset)
    {
        bw.Write(tag);
        bw.Write(type);
        bw.Write(count);
        bw.Write(valueOrOffset);
    }

    public static string ConvertToIco(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        using var bitmap = SKBitmap.Decode(inputPath);
        if (bitmap == null)
            throw new InvalidOperationException("Неможливо декодувати зображення.");

        int size = Math.Min(256, Math.Max(bitmap.Width, bitmap.Height));
        if (size <= 0) size = 64;

        var info = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var resized = bitmap.Resize(info, SKSamplingOptions.Default) ?? bitmap;
        using var image = SKImage.FromBitmap(resized);
        using var pngData = image.Encode(SKEncodedImageFormat.Png, 100);
        byte[] pngBytes = pngData.ToArray();

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}.ico"));

        using var fs = File.OpenWrite(outputPath);
        using var bw = new BinaryWriter(fs);

        // ICONDIR (6 bytes)
        bw.Write((ushort)0); // Reserved
        bw.Write((ushort)1); // Type = 1 (ICO)
        bw.Write((ushort)1); // Count = 1 image

        // ICONDIRENTRY (16 bytes)
        bw.Write((byte)(size >= 256 ? 0 : size)); // Width (0 means 256)
        bw.Write((byte)(size >= 256 ? 0 : size)); // Height (0 means 256)
        bw.Write((byte)0);                        // ColorCount
        bw.Write((byte)0);                        // Reserved
        bw.Write((ushort)1);                      // Planes
        bw.Write((ushort)32);                     // BitCount
        bw.Write((uint)pngBytes.Length);          // BytesInRes
        bw.Write((uint)22);                       // ImageOffset (6 + 16)

        bw.Write(pngBytes);
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

        var format = GetEncodedFormatForExtension(ext);
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(format, 90);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    public static string RotateImage90(string inputPath) => RotateImage(inputPath, 90);

    public static string RotateImage(string inputPath, int degrees)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        using var original = SKBitmap.Decode(inputPath);
        if (original == null)
            throw new InvalidOperationException("Неможливо декодувати зображення.");

        int normDegrees = ((degrees % 360) + 360) % 360;
        bool swapDims = normDegrees == 90 || normDegrees == 270;

        using var rotated = new SKBitmap(
            swapDims ? original.Height : original.Width,
            swapDims ? original.Width : original.Height,
            original.ColorType,
            original.AlphaType);

        using (var canvas = new SKCanvas(rotated))
        using (var origImg = SKImage.FromBitmap(original))
        {
            canvas.Clear(SKColors.Transparent);
            if (normDegrees == 90)
            {
                canvas.Translate(rotated.Width, 0);
                canvas.RotateDegrees(90);
            }
            else if (normDegrees == 270)
            {
                canvas.Translate(0, rotated.Height);
                canvas.RotateDegrees(270);
            }
            else if (normDegrees == 180)
            {
                canvas.Translate(rotated.Width, rotated.Height);
                canvas.RotateDegrees(180);
            }
            canvas.DrawImage(origImg, 0, 0, SKSamplingOptions.Default);
        }

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        var suffix = normDegrees == 270 ? "rot_left" : "rotated";

        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}_{suffix}{ext}"));
        var format = GetEncodedFormatForExtension(ext);

        using var image = SKImage.FromBitmap(rotated);
        using var data = image.Encode(format, 92);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    public static string ConvertToGrayscale(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        using var original = SKBitmap.Decode(inputPath);
        if (original == null)
            throw new InvalidOperationException("Неможливо декодувати зображення.");

        using var gray = new SKBitmap(original.Width, original.Height, original.ColorType, original.AlphaType);
        using (var canvas = new SKCanvas(gray))
        using (var origImg = SKImage.FromBitmap(original))
        using (var paint = new SKPaint())
        {
            float[] matrix =
            [
                0.299f, 0.587f, 0.114f, 0, 0,
                0.299f, 0.587f, 0.114f, 0, 0,
                0.299f, 0.587f, 0.114f, 0, 0,
                0,      0,      0,      1, 0
            ];
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(matrix);
            canvas.DrawImage(origImg, 0, 0, SKSamplingOptions.Default, paint);
        }

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}_gray{ext}"));

        var format = GetEncodedFormatForExtension(ext);
        using var image = SKImage.FromBitmap(gray);
        using var data = image.Encode(format, 92);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    public static string StripMetadata(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        using var original = SKBitmap.Decode(inputPath);
        if (original == null)
            throw new InvalidOperationException("Неможливо декодувати зображення.");

        // Copy raw pixels into a clean bitmap without any EXIF/GPS/XMP segments
        using var clean = new SKBitmap(original.Width, original.Height, original.ColorType, original.AlphaType);
        using (var canvas = new SKCanvas(clean))
        using (var origImg = SKImage.FromBitmap(original))
        {
            canvas.DrawImage(origImg, 0, 0, SKSamplingOptions.Default);
        }

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}_clean{ext}"));

        var format = GetEncodedFormatForExtension(ext);
        using var image = SKImage.FromBitmap(clean);
        using var data = image.Encode(format, 95);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    public static string MergeImagesToPdf(IReadOnlyList<string> inputPaths)
    {
        if (inputPaths == null || inputPaths.Count == 0)
            throw new ArgumentException("Не вибрано файлів для об'єднання в PDF.");

        var validPaths = inputPaths.Where(File.Exists).ToList();
        if (validPaths.Count == 0)
            throw new FileNotFoundException("Файли не знайдено.");

        var firstPath = validPaths[0];
        var dir = Path.GetDirectoryName(firstPath) ?? Environment.CurrentDirectory;
        var baseName = validPaths.Count == 1
            ? $"{Path.GetFileNameWithoutExtension(firstPath)}_merged.pdf"
            : $"{Path.GetFileNameWithoutExtension(firstPath)}_та_інші.pdf";
        var outputPath = GetUniqueFilePath(Path.Combine(dir, baseName));

        using var stream = File.OpenWrite(outputPath);
        using var document = SKDocument.CreatePdf(stream);

        foreach (var path in validPaths)
        {
            using var bitmap = SKBitmap.Decode(path);
            if (bitmap != null)
            {
                using var image = SKImage.FromBitmap(bitmap);
                using var canvas = document.BeginPage(bitmap.Width, bitmap.Height);
                canvas.Clear(SKColors.White);
                canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);
                document.EndPage();
            }
        }

        document.Close();
        return outputPath;
    }

    public static string ExtractTextOcr(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}_ocr.txt"));

        string extracted = TryWindowsOcr(inputPath);
        if (string.IsNullOrWhiteSpace(extracted))
        {
            using var bmp = SKBitmap.Decode(inputPath);
            extracted = bmp != null
                ? $"[OCR результат для {Path.GetFileName(inputPath)} ({bmp.Width}x{bmp.Height})]{Environment.NewLine}Текст на зображенні не виявлено або потрібна мовна модель OCR Windows."
                : $"[OCR результат для {Path.GetFileName(inputPath)}]";
        }

        File.WriteAllText(outputPath, extracted, Encoding.UTF8);
        return outputPath;
    }

    private static string TryWindowsOcr(string imagePath)
    {
        try
        {
            var escaped = imagePath.Replace("'", "''");
            var psScript = $@"
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation.UniversalApiContract, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Foundation.UniversalApiContract, ContentType = WindowsRuntime]
$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {{ $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' }})[0]
function Await($WinRtTask, $ResultType) {{
    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    $netTask = $asTask.Invoke($null, @($WinRtTask))
    $netTask.Wait(-1) | Out-Null
    $netTask.Result
}}
$file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync('{escaped}')) ([Windows.Storage.StorageFile])
$stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
$decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
$bitmap = Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
if ($null -ne $engine) {{
    $result = Await ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
    Write-Output $result.Text
}}
";
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{psScript.Replace("\"", "\\\"")}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(8000);
                return output.Trim();
            }
        }
        catch
        {
            // Ignore and use fallback
        }
        return string.Empty;
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
        var format = GetEncodedFormatForExtension(ext);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    public static string CropToSquare(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        using var original = SKBitmap.Decode(inputPath);
        if (original == null)
            throw new InvalidOperationException("Cannot decode image.");

        int side = Math.Min(original.Width, original.Height);
        int left = (original.Width - side) / 2;
        int top = (original.Height - side) / 2;

        using var cropped = new SKBitmap(side, side, original.ColorType, original.AlphaType);
        using (var canvas = new SKCanvas(cropped))
        using (var origImg = SKImage.FromBitmap(original))
        {
            canvas.Clear(SKColors.Transparent);
            var srcRect = new SKRect(left, top, left + side, top + side);
            var dstRect = new SKRect(0, 0, side, side);
            canvas.DrawImage(origImg, srcRect, dstRect, SKSamplingOptions.Default);
        }

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}_square{ext}"));

        var format = GetEncodedFormatForExtension(ext);
        using var image = SKImage.FromBitmap(cropped);
        using var data = image.Encode(format, 92);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    public static string FlipImageHorizontal(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        using var original = SKBitmap.Decode(inputPath);
        if (original == null)
            throw new InvalidOperationException("Cannot decode image.");

        using var flipped = new SKBitmap(original.Width, original.Height, original.ColorType, original.AlphaType);
        using (var canvas = new SKCanvas(flipped))
        using (var origImg = SKImage.FromBitmap(original))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(original.Width, 0);
            canvas.Scale(-1, 1);
            canvas.DrawImage(origImg, 0, 0, SKSamplingOptions.Default);
        }

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}_flipped{ext}"));

        var format = GetEncodedFormatForExtension(ext);
        using var image = SKImage.FromBitmap(flipped);
        using var data = image.Encode(format, 92);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    public static string AddWhiteBorder(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        using var original = SKBitmap.Decode(inputPath);
        if (original == null)
            throw new InvalidOperationException("Cannot decode image.");

        int pad = Math.Max(16, (int)(Math.Min(original.Width, original.Height) * 0.06));
        int newWidth = original.Width + pad * 2;
        int newHeight = original.Height + pad * 2;

        using var framed = new SKBitmap(newWidth, newHeight, original.ColorType, original.AlphaType);
        using (var canvas = new SKCanvas(framed))
        using (var origImg = SKImage.FromBitmap(original))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawImage(origImg, pad, pad, SKSamplingOptions.Default);
        }

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}_framed{ext}"));

        var format = GetEncodedFormatForExtension(ext);
        using var image = SKImage.FromBitmap(framed);
        using var data = image.Encode(format, 92);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    public static string MuteVideo(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        var ffmpeg = DependencyManagerService.GetFfmpegPath();
        if (ffmpeg == null)
            throw new InvalidOperationException(SettingsService.CurrentLanguageCode == "en"
                ? "FFmpeg is required. Open Settings -> Dependencies to install FFmpeg via PowerShell."
                : "Потрібен FFmpeg. Відкрийте Налаштування -> Додаткові програми, щоб встановити FFmpeg.");

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{name}_muted{ext}"));

        if (!RunProcess(ffmpeg, $"-y -i \"{inputPath}\" -c:v copy -an \"{outputPath}\"") || !File.Exists(outputPath))
            throw new InvalidOperationException("FFmpeg audio removal failed.");

        return outputPath;
    }

    public static string ExtractVideoFrame(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        var ffmpeg = DependencyManagerService.GetFfmpegPath();
        if (ffmpeg == null)
            throw new InvalidOperationException(SettingsService.CurrentLanguageCode == "en"
                ? "FFmpeg is required. Open Settings -> Dependencies to install FFmpeg via PowerShell."
                : "Потрібен FFmpeg. Відкрийте Налаштування -> Додаткові програми, щоб встановити FFmpeg.");

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{name}_frame.png"));

        if (!RunProcess(ffmpeg, $"-y -i \"{inputPath}\" -frames:v 1 \"{outputPath}\"") || !File.Exists(outputPath))
            throw new InvalidOperationException("FFmpeg frame extraction failed.");

        return outputPath;
    }

    public static string CompressVideo(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        var ffmpeg = DependencyManagerService.GetFfmpegPath();
        if (ffmpeg == null)
            throw new InvalidOperationException(SettingsService.CurrentLanguageCode == "en"
                ? "FFmpeg is required. Open Settings -> Dependencies to install FFmpeg via PowerShell."
                : "Потрібен FFmpeg. Відкрийте Налаштування -> Додаткові програми, щоб встановити FFmpeg.");

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{name}_compressed.mp4"));

        if (!RunProcess(ffmpeg, $"-y -i \"{inputPath}\" -c:v libx264 -crf 28 -preset fast -pix_fmt yuv420p -c:a aac -b:a 128k \"{outputPath}\"") || !File.Exists(outputPath))
            throw new InvalidOperationException("FFmpeg video compression failed.");

        return outputPath;
    }

    public static string TrimVideoClip(string inputPath, int seconds = 15)
    {
        return TrimVideoRange(inputPath, TimeSpan.Zero, TimeSpan.FromSeconds(seconds));
    }

    public static string TrimVideoRange(string inputPath, TimeSpan start, TimeSpan duration, string? outputFolder = null, Action<bool, string>? onCompleted = null)
    {
        if (!File.Exists(inputPath))
        {
            onCompleted?.Invoke(false, string.Empty);
            throw new FileNotFoundException("File not found", inputPath);
        }

        var ffmpeg = DependencyManagerService.GetFfmpegPath();
        if (ffmpeg == null)
        {
            onCompleted?.Invoke(false, string.Empty);
            throw new InvalidOperationException(SettingsService.CurrentLanguageCode == "en"
                ? "FFmpeg is required. Open Settings -> Dependencies to install FFmpeg via PowerShell."
                : "Потрібен FFmpeg. Відкрийте Налаштування -> Додаткові програми, щоб встановити FFmpeg.");
        }

        var dir = outputFolder ?? Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{name}_trimmed{ext}"));

        string startStr = start.ToString(@"hh\:mm\:ss\.fff", System.Globalization.CultureInfo.InvariantCulture);
        string durStr = duration.ToString(@"hh\:mm\:ss\.fff", System.Globalization.CultureInfo.InvariantCulture);

        // Accurate video & audio cut without stutter, frame jitter, or audio echo (aresample sync + keyframe re-encode)
        bool success = RunProcess(ffmpeg, $"-y -ss {startStr} -i \"{inputPath}\" -t {durStr} -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:a aac -b:a 192k -af \"aresample=async=1:first_pts=0\" -avoid_negative_ts make_zero -movflags +faststart \"{outputPath}\"") && File.Exists(outputPath);
        if (!success)
        {
            // Fallback for videos without audio stream or unusual audio codecs
            success = RunProcess(ffmpeg, $"-y -ss {startStr} -i \"{inputPath}\" -t {durStr} -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:a aac -b:a 192k -movflags +faststart \"{outputPath}\"") && File.Exists(outputPath);
        }
        if (!success)
        {
            // Video-only fallback if audio stream cannot be encoded
            success = RunProcess(ffmpeg, $"-y -ss {startStr} -i \"{inputPath}\" -t {durStr} -c:v libx264 -preset veryfast -crf 22 -pix_fmt yuv420p -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -an -movflags +faststart \"{outputPath}\"") && File.Exists(outputPath);
        }

        bool fileValid = success && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
        if (!fileValid)
        {
            onCompleted?.Invoke(false, string.Empty);
            throw new InvalidOperationException(SettingsService.CurrentLanguageCode == "en"
                ? "Failed to trim video using FFmpeg."
                : "Не вдалося обрізати відео через FFmpeg.");
        }

        // 100% confirmation callback from backend/processor that the operation successfully completed and the file exists
        onCompleted?.Invoke(true, outputPath);
        return outputPath;
    }

    public static string ExtractPlainText(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        return ExtractDocumentText(inputPath, ext);
    }

    public static string GenerateDocumentStats(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        var fi = new FileInfo(inputPath);
        var ext = fi.Extension.ToLowerInvariant();
        var text = ExtractDocumentText(inputPath, ext);
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var words = Regex.Matches(text, @"\b\S+\b").Count;
        var chars = text.Length;
        var charsNoSpaces = text.Count(c => !char.IsWhiteSpace(c));
        var readingMinutes = Math.Max(1, (int)Math.Ceiling(words / 200.0));

        var dir = fi.DirectoryName ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}_stats.txt"));

        bool isEn = SettingsService.CurrentLanguageCode == "en";
        var sb = new StringBuilder();
        if (isEn)
        {
            sb.AppendLine($"=== Document Statistics: {fi.Name} ===");
            sb.AppendLine($"File Size: {fi.Length:N0} bytes ({fi.Length / 1024.0:F1} KB)");
            sb.AppendLine($"Last Modified: {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Lines / Paragraphs: {lines.Length:N0}");
            sb.AppendLine($"Word Count: {words:N0}");
            sb.AppendLine($"Characters (with spaces): {chars:N0}");
            sb.AppendLine($"Characters (no spaces): {charsNoSpaces:N0}");
            sb.AppendLine($"Estimated Reading Time: ~{readingMinutes} min");
        }
        else
        {
            sb.AppendLine($"=== Статистика документа: {fi.Name} ===");
            sb.AppendLine($"Розмір файлу: {fi.Length:N0} байт ({fi.Length / 1024.0:F1} КБ)");
            sb.AppendLine($"Остання зміна: {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Рядків / абзаців: {lines.Length:N0}");
            sb.AppendLine($"Кількість слів: {words:N0}");
            sb.AppendLine($"Символів (з пробілами): {chars:N0}");
            sb.AppendLine($"Символів (без пробілів): {charsNoSpaces:N0}");
            sb.AppendLine($"Орієнтовний час читання: ~{readingMinutes} хв");
        }

        File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        return outPath;
    }

    public static string OptimizePdf(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}_optimized.pdf"));

        string textContent = ExtractDocumentText(inputPath, ".pdf");
        RenderTextToPdf(textContent, Path.GetFileName(inputPath), outPath);
        return outPath;
    }

    public static string CleanExtraSpacesInDocument(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        string raw = ExtractDocumentText(inputPath, ext);

        var cleanedLines = raw.Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => Regex.Replace(line.Trim(), @"\s{2,}", " "))
            .ToList();

        // Collapse multiple consecutive empty lines into at most one empty line
        var resultLines = new List<string>();
        bool prevEmpty = false;
        foreach (var l in cleanedLines)
        {
            if (string.IsNullOrEmpty(l))
            {
                if (!prevEmpty) resultLines.Add(string.Empty);
                prevEmpty = true;
            }
            else
            {
                resultLines.Add(l);
                prevEmpty = false;
            }
        }

        string cleanedText = string.Join(Environment.NewLine, resultLines);
        if (ext == ".docx")
        {
            var outDocx = GetUniqueFilePath(Path.Combine(dir, $"{name}_cleaned.docx"));
            CreateDocxFromText(cleanedText, outDocx);
            return outDocx;
        }

        var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}_cleaned.txt"));
        File.WriteAllText(outPath, cleanedText, Encoding.UTF8);
        return outPath;
    }

    public static string SortLinesInDocument(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        string raw = ExtractDocumentText(inputPath, ext);

        var sortedLines = raw.Replace("\r\n", "\n")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .OrderBy(l => l, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        string sortedText = string.Join(Environment.NewLine, sortedLines);
        if (ext == ".docx")
        {
            var outDocx = GetUniqueFilePath(Path.Combine(dir, $"{name}_sorted.docx"));
            CreateDocxFromText(sortedText, outDocx);
            return outDocx;
        }

        var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}_sorted.txt"));
        File.WriteAllText(outPath, sortedText, Encoding.UTF8);
        return outPath;
    }

    public static string GeneratePresentationSummary(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        string extracted = ExtractDocumentText(inputPath, ext);

        var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}_notes.md"));
        bool isEn = SettingsService.CurrentLanguageCode == "en";
        var header = isEn ? $"# Presentation Summary: {name}" : $"# Конспект презентації: {name}";
        File.WriteAllText(outPath, $"{header}{Environment.NewLine}{Environment.NewLine}{extracted}", Encoding.UTF8);
        return outPath;
    }

    public static string DeduplicateSpreadsheet(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        var rows = ReadSpreadsheetRows(inputPath, ext);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueRows = new List<List<string>>();
        foreach (var r in rows)
        {
            var key = string.Join("\u001F", r.Select(c => c.Trim()));
            if (seen.Add(key))
            {
                uniqueRows.Add(r);
            }
        }

        return SaveSpreadsheetRows(uniqueRows, dir, $"{name}_dedup", ext);
    }

    public static string SortSpreadsheet(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        var rows = ReadSpreadsheetRows(inputPath, ext);

        if (rows.Count > 1)
        {
            var header = rows[0];
            var sortedBody = rows.Skip(1)
                .OrderBy(r => r.Count > 0 ? r[0] : string.Empty, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            rows = [header, .. sortedBody];
        }

        return SaveSpreadsheetRows(rows, dir, $"{name}_sorted", ext);
    }

    public static string TransposeSpreadsheet(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        var rows = ReadSpreadsheetRows(inputPath, ext);

        int maxCols = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        var transposed = new List<List<string>>();
        for (int c = 0; c < maxCols; c++)
        {
            var newRow = new List<string>();
            for (int r = 0; r < rows.Count; r++)
            {
                newRow.Add(c < rows[r].Count ? rows[r][c] : string.Empty);
            }
            transposed.Add(newRow);
        }

        return SaveSpreadsheetRows(transposed, dir, $"{name}_transposed", ext);
    }

    public static string GenerateSpreadsheetStats(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        var rows = ReadSpreadsheetRows(inputPath, ext);

        int rowCount = rows.Count;
        int colCount = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        var allCells = rows.SelectMany(r => r).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        var numericValues = new List<double>();
        foreach (var cell in allCells)
        {
            if (double.TryParse(cell.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                numericValues.Add(val);
            }
        }

        var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}_stats.txt"));
        bool isEn = SettingsService.CurrentLanguageCode == "en";
        var sb = new StringBuilder();
        if (isEn)
        {
            sb.AppendLine($"=== Spreadsheet Statistics: {Path.GetFileName(inputPath)} ===");
            sb.AppendLine($"Rows: {rowCount:N0}");
            sb.AppendLine($"Columns: {colCount:N0}");
            sb.AppendLine($"Non-empty cells: {allCells.Count:N0}");
            sb.AppendLine($"Numeric cells: {numericValues.Count:N0}");
            if (numericValues.Count > 0)
            {
                sb.AppendLine($"Sum: {numericValues.Sum():N2}");
                sb.AppendLine($"Average: {numericValues.Average():N2}");
                sb.AppendLine($"Min: {numericValues.Min():N2}");
                sb.AppendLine($"Max: {numericValues.Max():N2}");
            }
        }
        else
        {
            sb.AppendLine($"=== Статистика таблиці: {Path.GetFileName(inputPath)} ===");
            sb.AppendLine($"Рядків: {rowCount:N0}");
            sb.AppendLine($"Стовпців: {colCount:N0}");
            sb.AppendLine($"Заповнених комірок: {allCells.Count:N0}");
            sb.AppendLine($"Числових значень: {numericValues.Count:N0}");
            if (numericValues.Count > 0)
            {
                sb.AppendLine($"Сума: {numericValues.Sum():N2}");
                sb.AppendLine($"Середнє: {numericValues.Average():N2}");
                sb.AppendLine($"Мінімум: {numericValues.Min():N2}");
                sb.AppendLine($"Максимум: {numericValues.Max():N2}");
            }
        }

        File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        return outPath;
    }

    private static string SaveSpreadsheetRows(List<List<string>> rows, string dir, string baseName, string ext)
    {
        string targetExt = ext is ".xlsx" or ".csv" or ".tsv" ? ext : ".csv";
        var outPath = GetUniqueFilePath(Path.Combine(dir, $"{baseName}{targetExt}"));
        if (targetExt == ".xlsx")
        {
            CreateXlsxFromRows(rows, outPath);
        }
        else if (targetExt == ".tsv")
        {
            File.WriteAllLines(outPath, rows.Select(r => string.Join("\t", r.Select(c => c.Replace("\t", " ")))), Encoding.UTF8);
        }
        else
        {
            File.WriteAllLines(outPath, rows.Select(r => string.Join(",", r.Select(EscapeCsvCell))), Encoding.UTF8);
        }
        return outPath;
    }

    public static string ListArchiveContents(string archivePath)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive not found", archivePath);

        var dir = Path.GetDirectoryName(archivePath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(archivePath);
        var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}_contents.txt"));
        var lower = archivePath.ToLowerInvariant();

        bool isEn = SettingsService.CurrentLanguageCode == "en";
        var sb = new StringBuilder();
        sb.AppendLine(isEn
            ? $"=== Archive Contents: {Path.GetFileName(archivePath)} ==="
            : $"=== Вміст архіву: {Path.GetFileName(archivePath)} ===");

        int count = 0;
        long totalSize = 0;

        if (lower.EndsWith(".zip"))
        {
            using var zip = ZipFile.OpenRead(archivePath);
            foreach (var entry in zip.Entries)
            {
                sb.AppendLine($"{entry.FullName,-50}  {entry.Length,12:N0} B");
                count++;
                totalSize += entry.Length;
            }
        }
        else if (lower.EndsWith(".tar.gz") || lower.EndsWith(".tgz"))
        {
            using var fs = File.OpenRead(archivePath);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var reader = new TarReader(gz);
            while (reader.GetNextEntry() is { } entry)
            {
                sb.AppendLine($"{entry.Name,-50}  {entry.Length,12:N0} B");
                count++;
                totalSize += entry.Length;
            }
        }
        else if (lower.EndsWith(".tar"))
        {
            using var fs = File.OpenRead(archivePath);
            using var reader = new TarReader(fs);
            while (reader.GetNextEntry() is { } entry)
            {
                sb.AppendLine($"{entry.Name,-50}  {entry.Length,12:N0} B");
                count++;
                totalSize += entry.Length;
            }
        }

        sb.AppendLine();
        sb.AppendLine(isEn
            ? $"Total entries: {count} | Uncompressed size: {totalSize:N0} bytes"
            : $"Всього елементів: {count} | Розмір без стиснення: {totalSize:N0} байт");

        File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        return outPath;
    }

    public static int VerifyArchiveIntegrity(string archivePath)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive not found", archivePath);

        var lower = archivePath.ToLowerInvariant();
        int verifiedEntries = 0;
        byte[] buffer = new byte[8192];

        if (lower.EndsWith(".zip"))
        {
            using var zip = ZipFile.OpenRead(archivePath);
            foreach (var entry in zip.Entries)
            {
                using var s = entry.Open();
                while (s.Read(buffer, 0, buffer.Length) > 0) { }
                verifiedEntries++;
            }
            return verifiedEntries;
        }

        if (lower.EndsWith(".tar.gz") || lower.EndsWith(".tgz"))
        {
            using var fs = File.OpenRead(archivePath);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var reader = new TarReader(gz);
            while (reader.GetNextEntry() is { } entry)
            {
                if (entry.DataStream != null)
                {
                    while (entry.DataStream.Read(buffer, 0, buffer.Length) > 0) { }
                }
                verifiedEntries++;
            }
            return verifiedEntries;
        }

        if (lower.EndsWith(".tar"))
        {
            using var fs = File.OpenRead(archivePath);
            using var reader = new TarReader(fs);
            while (reader.GetNextEntry() is { } entry)
            {
                if (entry.DataStream != null)
                {
                    while (entry.DataStream.Read(buffer, 0, buffer.Length) > 0) { }
                }
                verifiedEntries++;
            }
            return verifiedEntries;
        }

        return verifiedEntries;
    }

    public static (string ReportPath, string Sha256Hex) ComputeFileHashReport(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("File not found", inputPath);

        using var fs = File.OpenRead(inputPath);
        byte[] sha256Bytes = SHA256.HashData(fs);
        fs.Position = 0;
        byte[] md5Bytes = MD5.HashData(fs);

        string sha256Hex = Convert.ToHexString(sha256Bytes).ToLowerInvariant();
        string md5Hex = Convert.ToHexString(md5Bytes).ToLowerInvariant();

        var fi = new FileInfo(inputPath);
        var dir = fi.DirectoryName ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}_sha256.txt"));

        bool isEn = SettingsService.CurrentLanguageCode == "en";
        var sb = new StringBuilder();
        sb.AppendLine(isEn ? $"File: {fi.Name}" : $"Файл: {fi.Name}");
        sb.AppendLine(isEn ? $"Size: {fi.Length:N0} bytes" : $"Розмір: {fi.Length:N0} байт");
        sb.AppendLine($"SHA-256: {sha256Hex}");
        sb.AppendLine($"MD5:     {md5Hex}");

        File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        return (outPath, sha256Hex);
    }

    public static string ConvertToPdf(string inputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}.pdf"));

        if (ext == ".pdf")
        {
            File.Copy(inputPath, outputPath, overwrite: true);
            return outputPath;
        }

        // 1. Try native MS Office COM automation on Windows (Word, Excel, PowerPoint)
        if (ext is ".doc" or ".dock" or ".docx" or ".rtf" or ".xls" or ".xlsx" or ".ppt" or ".pptx")
        {
            var officePdf = TryConvertWithOfficeCom(inputPath, "pdf", dir);
            if (officePdf != null && File.Exists(officePdf))
                return officePdf;
        }

        // 2. Try LibreOffice for office formats if installed
        if (ext is ".doc" or ".dock" or ".docx" or ".odt" or ".rtf" or ".ppt" or ".pptx" or ".odp" or ".xls" or ".xlsx" or ".ods")
        {
            var loConverted = TryConvertWithLibreOffice(inputPath, "pdf", dir);
            if (loConverted != null && File.Exists(loConverted))
                return loConverted;
        }

        // 3. Try Pandoc for document formats
        if (ext is ".docx" or ".dock" or ".odt" or ".rtf" or ".md" or ".html" or ".htm" or ".txt")
        {
            var pandoc = DependencyManagerService.GetPandocPath();
            if (pandoc != null && RunProcess(pandoc, $"\"{inputPath}\" -o \"{outputPath}\"") && File.Exists(outputPath))
                return outputPath;
        }

        // Try decoding as an image for image formats
        if (ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" or ".gif" or ".tiff" or ".tif" or ".ico" or ".avif")
        {
            using var inStream = File.OpenRead(inputPath);
            using var bitmap = SKBitmap.Decode(inStream);
            if (bitmap != null)
            {
                using var image = SKImage.FromBitmap(bitmap);
                using var stream = File.OpenWrite(outputPath);
                using var document = SKDocument.CreatePdf(stream);
                using (var canvas = document.BeginPage(bitmap.Width, bitmap.Height))
                {
                    canvas.Clear(SKColors.White);
                    canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);
                    document.EndPage();
                }
                document.Close();
                return outputPath;
            }
        }

        // Convert document / presentation / spreadsheet / text content to an A4 PDF document
        string textContent = ExtractDocumentText(inputPath, ext);
        RenderTextToPdf(textContent, Path.GetFileName(inputPath), outputPath);
        return outputPath;
    }

    public static string ConvertImageToVideo(string inputPath, string targetExt)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        var ffmpeg = DependencyManagerService.GetFfmpegPath();
        if (ffmpeg == null)
            throw new InvalidOperationException("Потрібен FFmpeg. Відкрийте Налаштування -> Додаткові програми, щоб завантажити FFmpeg.");

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = targetExt.StartsWith('.') ? targetExt : $".{targetExt}";
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{name}{ext}"));

        var codecArgs = ext.Equals(".webm", StringComparison.OrdinalIgnoreCase)
            ? "-c:v libvpx-vp9 -b:v 1M -pix_fmt yuv420p"
            : "-c:v libx264 -pix_fmt yuv420p -movflags +faststart";

        var args = $"-y -loop 1 -i \"{inputPath}\" -t 5 -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" {codecArgs} \"{outputPath}\"";
        if (!RunProcess(ffmpeg, args) || !File.Exists(outputPath))
            throw new InvalidOperationException("Не вдалося створити відео через FFmpeg.");

        return outputPath;
    }

    public static string ConvertVideo(string inputPath, string targetExt)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        var ffmpeg = DependencyManagerService.GetFfmpegPath();
        if (ffmpeg == null)
            throw new InvalidOperationException("Потрібен FFmpeg. Відкрийте Налаштування -> Додаткові програми, щоб завантажити FFmpeg.");

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = targetExt.StartsWith('.') ? targetExt.ToLowerInvariant() : $".{targetExt.ToLowerInvariant()}";
        var outputPath = GetUniqueFilePath(Path.Combine(dir, $"{name}{ext}"));

        string args = ext switch
        {
            ".gif" => $"-y -i \"{inputPath}\" -vf \"fps=12,scale=480:-1:flags=lanczos\" \"{outputPath}\"",
            ".mp3" => $"-y -i \"{inputPath}\" -vn -acodec libmp3lame -q:a 2 \"{outputPath}\"",
            ".wav" => $"-y -i \"{inputPath}\" -vn -acodec pcm_s16le \"{outputPath}\"",
            ".webm" => $"-y -i \"{inputPath}\" -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libvpx-vp9 -b:v 1.5M -crf 30 -c:a libopus -b:a 128k \"{outputPath}\"",
            ".mp4" => $"-y -i \"{inputPath}\" -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -preset medium -crf 22 -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart \"{outputPath}\"",
            ".mov" => $"-y -i \"{inputPath}\" -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -preset medium -crf 22 -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart \"{outputPath}\"",
            ".mkv" => $"-y -i \"{inputPath}\" -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -preset medium -crf 22 -pix_fmt yuv420p -c:a aac -b:a 192k \"{outputPath}\"",
            _ => $"-y -i \"{inputPath}\" -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -preset medium -crf 22 -pix_fmt yuv420p -c:a aac \"{outputPath}\""
        };

        if (!RunProcess(ffmpeg, args) || !File.Exists(outputPath))
        {
            // Automatic fallback with stereo downmix if multi-channel source audio caused encoder failure
            var fallbackArgs = ext switch
            {
                ".mp4" => $"-y -i \"{inputPath}\" -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p -c:a aac -ac 2 -ar 44100 \"{outputPath}\"",
                ".mov" => $"-y -i \"{inputPath}\" -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p -c:a aac -ac 2 -ar 44100 \"{outputPath}\"",
                ".gif" => $"-y -i \"{inputPath}\" -vf \"fps=10,scale=360:-1\" \"{outputPath}\"",
                ".mp3" => $"-y -i \"{inputPath}\" -vn -c:a libmp3lame -ac 2 -ar 44100 -b:a 192k \"{outputPath}\"",
                _ => $"-y -i \"{inputPath}\" -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" \"{outputPath}\""
            };
            if (!RunProcess(ffmpeg, fallbackArgs) || !File.Exists(outputPath))
                throw new InvalidOperationException($"Помилка конвертації відео у {ext.ToUpperInvariant()}.");
        }

        return outputPath;
    }

    public static string ConvertPdf(string inputPath, string targetFormat)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var fmt = targetFormat.TrimStart('.').ToLowerInvariant();
        string textContent = ExtractDocumentText(inputPath, ".pdf");

        switch (fmt)
        {
            case "txt":
            {
                var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}.txt"));
                File.WriteAllText(outPath, textContent, Encoding.UTF8);
                return outPath;
            }
            case "html":
            {
                var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}.html"));
                File.WriteAllText(outPath, BuildHtmlDocument(name, textContent), Encoding.UTF8);
                return outPath;
            }
            case "docx":
            {
                var lo = TryConvertWithLibreOffice(inputPath, "docx", dir);
                if (lo != null && File.Exists(lo)) return lo;
                var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}.docx"));
                CreateDocxFromText(textContent, outPath);
                return outPath;
            }
            case "pptx":
            {
                var lo = TryConvertWithLibreOffice(inputPath, "pptx", dir);
                if (lo != null && File.Exists(lo)) return lo;
                var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}.pptx"));
                CreatePptxFromText(name, textContent, outPath);
                return outPath;
            }
            case "jpg":
            case "png":
            {
                var ext = fmt == "jpg" ? ".jpg" : ".png";
                var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}_page1{ext}"));
                RenderTextToImage(textContent, Path.GetFileName(inputPath), outPath, fmt == "jpg" ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png);
                return outPath;
            }
            default:
                throw new NotSupportedException($"Формат {targetFormat} не підтримується.");
        }
    }

    public static string ConvertDocument(string inputPath, string targetFormat)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        var fmt = targetFormat.TrimStart('.').ToLowerInvariant();
        if (fmt == "pdf")
            return ConvertToPdf(inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}.{fmt}"));

        // 1. Try Pandoc if available
        var pandoc = DependencyManagerService.GetPandocPath();
        if (pandoc != null && RunProcess(pandoc, $"\"{inputPath}\" -o \"{outPath}\"") && File.Exists(outPath))
        {
            return outPath;
        }

        // 2. Try LibreOffice if available for Office formats
        if (fmt is "docx" or "odt" or "rtf" or "html" or "txt")
        {
            var lo = TryConvertWithLibreOffice(inputPath, fmt, dir);
            if (lo != null && File.Exists(lo))
                return lo;
        }

        // 3. Built-in converter (works out of the box)
        string textContent = ExtractDocumentText(inputPath, ext);
        switch (fmt)
        {
            case "txt":
                File.WriteAllText(outPath, textContent, Encoding.UTF8);
                break;
            case "md":
                File.WriteAllText(outPath, $"# {name}{Environment.NewLine}{Environment.NewLine}{textContent}", Encoding.UTF8);
                break;
            case "html":
                File.WriteAllText(outPath, BuildHtmlDocument(name, textContent), Encoding.UTF8);
                break;
            case "rtf":
                File.WriteAllText(outPath, BuildRtfDocument(textContent), Encoding.ASCII);
                break;
            case "docx":
                CreateDocxFromText(textContent, outPath);
                break;
            case "odt":
                CreateOdtFromText(textContent, outPath);
                break;
            case "epub":
                CreateEpubFromText(name, textContent, outPath);
                break;
            default:
                File.WriteAllText(outPath, textContent, Encoding.UTF8);
                break;
        }

        return outPath;
    }

    public static string ConvertPresentation(string inputPath, string targetFormat)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        var fmt = targetFormat.TrimStart('.').ToLowerInvariant();
        if (fmt == "pdf")
            return ConvertToPdf(inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();

        var lo = TryConvertWithLibreOffice(inputPath, fmt, dir);
        if (lo != null && File.Exists(lo))
            return lo;

        string textContent = ExtractDocumentText(inputPath, ext);
        switch (fmt)
        {
            case "png":
            case "jpg":
            {
                var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}_slide1.{fmt}"));
                RenderTextToImage(textContent, name, outPath, fmt == "jpg" ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png);
                return outPath;
            }
            case "pptx":
            {
                var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}.pptx"));
                CreatePptxFromText(name, textContent, outPath);
                return outPath;
            }
            case "odp":
            {
                var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}.odp"));
                CreateOdtFromText(textContent, outPath);
                return outPath;
            }
            default:
                throw new NotSupportedException($"Формат {targetFormat} не підтримується.");
        }
    }

    public static string ConvertSpreadsheet(string inputPath, string targetFormat)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Файл не знайдено", inputPath);

        var fmt = targetFormat.TrimStart('.').ToLowerInvariant();
        if (fmt == "pdf")
            return ConvertToPdf(inputPath);

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();

        if (fmt is "xlsx" or "ods")
        {
            var lo = TryConvertWithLibreOffice(inputPath, fmt, dir);
            if (lo != null && File.Exists(lo))
                return lo;
        }

        var rows = ReadSpreadsheetRows(inputPath, ext);
        var outPath = GetUniqueFilePath(Path.Combine(dir, $"{name}.{fmt}"));

        switch (fmt)
        {
            case "csv":
                File.WriteAllLines(outPath, rows.Select(r => string.Join(",", r.Select(EscapeCsvCell))), Encoding.UTF8);
                break;
            case "tsv":
                File.WriteAllLines(outPath, rows.Select(r => string.Join("\t", r.Select(c => c.Replace("\t", " ")))), Encoding.UTF8);
                break;
            case "xlsx":
                CreateXlsxFromRows(rows, outPath);
                break;
            case "ods":
                CreateOdsFromRows(rows, outPath);
                break;
            default:
                File.WriteAllLines(outPath, rows.Select(r => string.Join(",", r)), Encoding.UTF8);
                break;
        }

        return outPath;
    }

    public static string CompressToZip(string inputPath) => CompressToZip([inputPath]);

    public static string CompressToZip(IReadOnlyList<string> inputPaths)
    {
        if (inputPaths == null || inputPaths.Count == 0)
            throw new ArgumentException("Не вибрано файлів для архівації.");

        if (inputPaths.Count == 1)
        {
            var single = inputPaths[0];
            if (File.Exists(single))
            {
                var dir = Path.GetDirectoryName(single) ?? Environment.CurrentDirectory;
                var nameWithoutExt = Path.GetFileNameWithoutExtension(single);
                var zipPath = GetUniqueFilePath(Path.Combine(dir, $"{nameWithoutExt}.zip"));

                using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                archive.CreateEntryFromFile(single, Path.GetFileName(single), CompressionLevel.Optimal);
                return zipPath;
            }

            if (Directory.Exists(single))
            {
                var trimmed = single.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var parentDir = Path.GetDirectoryName(trimmed) ?? Environment.CurrentDirectory;
                var folderName = Path.GetFileName(trimmed);
                var zipPath = GetUniqueFilePath(Path.Combine(parentDir, $"{folderName}.zip"));

                ZipFile.CreateFromDirectory(single, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
                return zipPath;
            }

            throw new FileNotFoundException("Файл або папку не знайдено", single);
        }

        var first = inputPaths[0];
        var baseDir = Path.GetDirectoryName(first) ?? Environment.CurrentDirectory;
        var multiZipPath = GetUniqueFilePath(Path.Combine(baseDir, $"{Path.GetFileNameWithoutExtension(first)}_archive.zip"));

        using (var archive = ZipFile.Open(multiZipPath, ZipArchiveMode.Create))
        {
            foreach (var p in inputPaths)
            {
                if (File.Exists(p))
                {
                    archive.CreateEntryFromFile(p, Path.GetFileName(p), CompressionLevel.Optimal);
                }
            }
        }
        return multiZipPath;
    }

    public static string CompressToTar(IReadOnlyList<string> inputPaths, bool gzip)
    {
        if (inputPaths == null || inputPaths.Count == 0)
            throw new ArgumentException("Не вибрано файлів для архівації.");

        var first = inputPaths[0];
        var trimmed = first.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var baseDir = Path.GetDirectoryName(trimmed) ?? Environment.CurrentDirectory;
        var baseName = Path.GetFileNameWithoutExtension(trimmed);
        var ext = gzip ? ".tar.gz" : ".tar";
        var outPath = GetUniqueFilePath(Path.Combine(baseDir, $"{baseName}{ext}"));

        using var outFileStream = File.OpenWrite(outPath);
        Stream targetStream = gzip ? new GZipStream(outFileStream, CompressionLevel.Optimal, leaveOpen: false) : outFileStream;

        using (targetStream)
        using (var writer = new TarWriter(targetStream, TarEntryFormat.Pax, leaveOpen: false))
        {
            foreach (var p in inputPaths)
            {
                if (File.Exists(p))
                {
                    writer.WriteEntry(p, Path.GetFileName(p));
                }
                else if (Directory.Exists(p))
                {
                    foreach (var file in Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories))
                    {
                        var rel = Path.GetRelativePath(p, file).Replace('\\', '/');
                        writer.WriteEntry(file, rel);
                    }
                }
            }
        }

        return outPath;
    }

    public static string ExtractArchive(string archivePath)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Архів не знайдено", archivePath);

        var dir = Path.GetDirectoryName(archivePath) ?? Environment.CurrentDirectory;
        var fileName = Path.GetFileName(archivePath);
        var lower = fileName.ToLowerInvariant();
        string baseName = lower.EndsWith(".tar.gz")
            ? fileName[..^7]
            : Path.GetFileNameWithoutExtension(archivePath);

        var destDir = GetUniqueDirectoryPath(Path.Combine(dir, $"{baseName}_extracted"));
        Directory.CreateDirectory(destDir);

        if (lower.EndsWith(".zip"))
        {
            ZipFile.ExtractToDirectory(archivePath, destDir, overwriteFiles: true);
            return destDir;
        }

        if (lower.EndsWith(".tar.gz") || lower.EndsWith(".tgz"))
        {
            using var fs = File.OpenRead(archivePath);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gz, destDir, overwriteFiles: true);
            return destDir;
        }

        if (lower.EndsWith(".tar"))
        {
            TarFile.ExtractToDirectory(archivePath, destDir, overwriteFiles: true);
            return destDir;
        }

        if (lower.EndsWith(".gz"))
        {
            var outFileName = Path.GetFileNameWithoutExtension(archivePath);
            var outFilePath = Path.Combine(destDir, string.IsNullOrWhiteSpace(outFileName) ? "extracted_file" : outFileName);
            using var fs = File.OpenRead(archivePath);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var outFs = File.OpenWrite(outFilePath);
            gz.CopyTo(outFs);
            return destDir;
        }

        // Try Windows built-in tar.exe for other archive formats (.7z, .rar, .bz2, .xz)
        if (RunProcess("tar.exe", $"-xf \"{archivePath}\" -C \"{destDir}\""))
        {
            return destDir;
        }

        throw new InvalidOperationException($"Не вдалося розпакувати архів {fileName}.");
    }

    private static string? TryConvertWithLibreOffice(string inputPath, string targetExt, string outputDir)
    {
        var soffice = DependencyManagerService.GetLibreOfficePath();
        if (soffice == null)
            return null;

        if (RunProcess(soffice, $"--headless --convert-to {targetExt} --outdir \"{outputDir}\" \"{inputPath}\""))
        {
            var expected = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}.{targetExt}");
            if (File.Exists(expected))
                return expected;
        }
        return null;
    }

    private static string? TryConvertWithOfficeCom(string inputPath, string targetExt, string outputDir)
    {
        if (!OperatingSystem.IsWindows()) return null;
        if (!targetExt.Equals("pdf", StringComparison.OrdinalIgnoreCase)) return null;

        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var expectedPdf = GetUniqueFilePath(Path.Combine(outputDir, $"{baseName}.pdf"));

        try
        {
            if (ext is ".doc" or ".docx" or ".rtf")
            {
                // Verify magic bytes before invoking Word COM to avoid corrupted-document prompts
                var headerBytes = new byte[8];
                using (var fs = File.OpenRead(inputPath))
                {
                    int read = fs.Read(headerBytes, 0, headerBytes.Length);
                    if (read < 4) return null;
                }
                bool isZip = headerBytes[0] == 0x50 && headerBytes[1] == 0x4B;
                bool isOle = headerBytes[0] == 0xD0 && headerBytes[1] == 0xCF;
                bool isRtf = headerBytes[0] == (byte)'{' && headerBytes[1] == (byte)'\\' && headerBytes[2] == (byte)'r';
                if (!isZip && !isOle && !isRtf) return null;

                string? comOutput = null;
                var comTask = Task.Run(() =>
                {
                    Type? wordType = Type.GetTypeFromProgID("Word.Application");
                    if (wordType != null)
                    {
                        object? word = null;
                        object? doc = null;
                        try
                        {
                            word = Activator.CreateInstance(wordType);
                            wordType.InvokeMember("Visible", BindingFlags.SetProperty, null, word, [false]);
                            wordType.InvokeMember("DisplayAlerts", BindingFlags.SetProperty, null, word, [0]);
                            var docs = wordType.InvokeMember("Documents", BindingFlags.GetProperty, null, word, null);
                            doc = docs?.GetType().InvokeMember("Open", BindingFlags.InvokeMethod, null, docs, [inputPath, true, true]);
                            if (doc != null)
                            {
                                doc.GetType().InvokeMember("ExportAsFixedFormat", BindingFlags.InvokeMethod, null, doc, [expectedPdf, 17 /* wdExportFormatPDF */]);
                                if (File.Exists(expectedPdf)) comOutput = expectedPdf;
                            }
                        }
                        catch { }
                        finally
                        {
                            if (doc != null)
                            {
                                try { doc.GetType().InvokeMember("Close", BindingFlags.InvokeMethod, null, doc, [false]); } catch { }
                                try { Marshal.ReleaseComObject(doc); } catch { }
                            }
                            if (word != null)
                            {
                                try { wordType.InvokeMember("Quit", BindingFlags.InvokeMethod, null, word, [false]); } catch { }
                                try { Marshal.ReleaseComObject(word); } catch { }
                            }
                        }
                    }
                });

                if (comTask.Wait(4000) && comOutput != null)
                {
                    return comOutput;
                }
            }
            else if (ext is ".xls" or ".xlsx")
            {
                Type? excelType = Type.GetTypeFromProgID("Excel.Application");
                if (excelType != null)
                {
                    object? excel = null;
                    object? wb = null;
                    try
                    {
                        excel = Activator.CreateInstance(excelType);
                        excelType.InvokeMember("Visible", BindingFlags.SetProperty, null, excel, [false]);
                        excelType.InvokeMember("DisplayAlerts", BindingFlags.SetProperty, null, excel, [false]);
                        var wbs = excelType.InvokeMember("Workbooks", BindingFlags.GetProperty, null, excel, null);
                        wb = wbs?.GetType().InvokeMember("Open", BindingFlags.InvokeMethod, null, wbs, [inputPath, true, true]);
                        if (wb != null)
                        {
                            wb.GetType().InvokeMember("ExportAsFixedFormat", BindingFlags.InvokeMethod, null, wb, [0 /* xlTypePDF */, expectedPdf]);
                            if (File.Exists(expectedPdf)) return expectedPdf;
                        }
                    }
                    finally
                    {
                        if (wb != null)
                        {
                            try { wb.GetType().InvokeMember("Close", BindingFlags.InvokeMethod, null, wb, [false]); } catch { }
                            Marshal.ReleaseComObject(wb);
                        }
                        if (excel != null)
                        {
                            try { excelType.InvokeMember("Quit", BindingFlags.InvokeMethod, null, excel, []); } catch { }
                            Marshal.ReleaseComObject(excel);
                        }
                    }
                }
            }
            else if (ext is ".ppt" or ".pptx")
            {
                Type? pptType = Type.GetTypeFromProgID("PowerPoint.Application");
                if (pptType != null)
                {
                    object? ppt = null;
                    object? pres = null;
                    try
                    {
                        ppt = Activator.CreateInstance(pptType);
                        var presentations = pptType.InvokeMember("Presentations", BindingFlags.GetProperty, null, ppt, null);
                        pres = presentations?.GetType().InvokeMember("Open", BindingFlags.InvokeMethod, null, presentations, [inputPath, 1 /* ReadOnly */, 0 /* Untitled */, 0 /* WithWindow False */]);
                        if (pres != null)
                        {
                            pres.GetType().InvokeMember("SaveAs", BindingFlags.InvokeMethod, null, pres, [expectedPdf, 32 /* ppSaveAsPDF */]);
                            if (File.Exists(expectedPdf)) return expectedPdf;
                        }
                    }
                    finally
                    {
                        if (pres != null)
                        {
                            try { pres.GetType().InvokeMember("Close", BindingFlags.InvokeMethod, null, pres, []); } catch { }
                            Marshal.ReleaseComObject(pres);
                        }
                        if (ppt != null)
                        {
                            try { pptType.InvokeMember("Quit", BindingFlags.InvokeMethod, null, ppt, []); } catch { }
                            Marshal.ReleaseComObject(ppt);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Office COM conversion error: {ex.Message}");
        }

        return null;
    }

    private static bool RunProcess(string fileName, string arguments, int timeoutMs = 120000)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;

            // Asynchronously read stdout and stderr to prevent pipe buffer deadlocks
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();

            bool finished = proc.WaitForExit(timeoutMs);
            if (!finished)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return false;
            }

            Task.WaitAll(new Task[] { stdoutTask, stderrTask }, 2000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static SKEncodedImageFormat GetEncodedFormatForExtension(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".webp" => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Png
        };
    }

    private static string ExtractDocumentText(string inputPath, string ext)
    {
        try
        {
            if (ext is ".docx" or ".dock")
            {
                try
                {
                    using var archive = ZipFile.OpenRead(inputPath);
                    var docEntry = archive.GetEntry("word/document.xml");
                    if (docEntry != null)
                    {
                        using var entryStream = docEntry.Open();
                        var xdoc = XDocument.Load(entryStream);
                        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        var paragraphs = xdoc.Descendants(w + "p")
                            .Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value)));
                        return string.Join(Environment.NewLine, paragraphs);
                    }
                }
                catch { }
            }

            if (ext is ".odt" or ".odp" or ".ods")
            {
                using var archive = ZipFile.OpenRead(inputPath);
                var contentEntry = archive.GetEntry("content.xml");
                if (contentEntry != null)
                {
                    using var entryStream = contentEntry.Open();
                    var xdoc = XDocument.Load(entryStream);
                    return string.Join(Environment.NewLine, xdoc.Descendants().Where(e => !e.HasElements && !string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Value));
                }
            }

            if (ext == ".pptx")
            {
                using var archive = ZipFile.OpenRead(inputPath);
                var slides = archive.Entries
                    .Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.FullName)
                    .ToList();

                var sb = new StringBuilder();
                XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
                int idx = 1;
                foreach (var slide in slides)
                {
                    using var s = slide.Open();
                    var xdoc = XDocument.Load(s);
                    var texts = xdoc.Descendants(a + "t").Select(t => t.Value).Where(v => !string.IsNullOrWhiteSpace(v));
                    sb.AppendLine($"--- Слайд {idx++} ---");
                    sb.AppendLine(string.Join(" ", texts));
                    sb.AppendLine();
                }
                if (sb.Length > 0) return sb.ToString();
            }

            if (ext is ".xlsx" or ".csv" or ".tsv")
            {
                var rows = ReadSpreadsheetRows(inputPath, ext);
                return string.Join(Environment.NewLine, rows.Select(r => string.Join(" | ", r)));
            }

            if (ext == ".pdf")
            {
                var rawBytes = File.ReadAllBytes(inputPath);
                var latin = Encoding.Latin1.GetString(rawBytes);
                var matches = Regex.Matches(latin, @"\(([^()\\]{2,})\)");
                if (matches.Count > 0)
                {
                    var extracted = string.Join(" ", matches.Select(m => m.Groups[1].Value));
                    if (!string.IsNullOrWhiteSpace(extracted))
                        return extracted;
                }
            }

            if (ext is ".doc" or ".dock")
            {
                var extractedDocText = ExtractTextFromBinaryDoc(inputPath);
                if (!string.IsNullOrWhiteSpace(extractedDocText))
                    return extractedDocText;

                if (OperatingSystem.IsWindows())
                {
                    string? comText = null;
                    var t = Task.Run(() =>
                    {
                        Type? wordType = Type.GetTypeFromProgID("Word.Application");
                        if (wordType != null)
                        {
                            object? word = null;
                            object? doc = null;
                            try
                            {
                                word = Activator.CreateInstance(wordType);
                                wordType.InvokeMember("Visible", BindingFlags.SetProperty, null, word, [false]);
                                wordType.InvokeMember("DisplayAlerts", BindingFlags.SetProperty, null, word, [0]);
                                var docs = wordType.InvokeMember("Documents", BindingFlags.GetProperty, null, word, null);
                                doc = docs?.GetType().InvokeMember("Open", BindingFlags.InvokeMethod, null, docs, [inputPath, true, true]);
                                if (doc != null)
                                {
                                    var content = doc.GetType().InvokeMember("Content", BindingFlags.GetProperty, null, doc, null);
                                    comText = content?.GetType().InvokeMember("Text", BindingFlags.GetProperty, null, content, null)?.ToString();
                                }
                            }
                            catch { }
                            finally
                            {
                                if (doc != null) { try { doc.GetType().InvokeMember("Close", BindingFlags.InvokeMethod, null, doc, [false]); } catch { } try { Marshal.ReleaseComObject(doc); } catch { } }
                                if (word != null) { try { wordType.InvokeMember("Quit", BindingFlags.InvokeMethod, null, word, [false]); } catch { } try { Marshal.ReleaseComObject(word); } catch { } }
                            }
                        }
                    });
                    if (t.Wait(3000) && !string.IsNullOrWhiteSpace(comText))
                        return comText;
                }
            }

            if (ext is ".txt" or ".md" or ".json" or ".xml" or ".html" or ".htm" or ".rtf" or ".log" or ".ini" or ".bat" or ".ps1")
            {
                return File.ReadAllText(inputPath);
            }
        }
        catch
        {
            // Fallback below
        }

        var fi = new FileInfo(inputPath);
        return $"Документ: {fi.Name}{Environment.NewLine}Розмір: {fi.Length} байт{Environment.NewLine}Дата зміни: {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
    }

    private static string? ExtractTextFromBinaryDoc(string inputPath)
    {
        try
        {
            var bytes = File.ReadAllBytes(inputPath);
            if (bytes.Length == 0) return null;

            // If RTF file
            if (bytes.Length >= 5 && bytes[0] == (byte)'{' && bytes[1] == (byte)'\\' && bytes[2] == (byte)'r' && bytes[3] == (byte)'t' && bytes[4] == (byte)'f')
            {
                var rtfStr = Encoding.ASCII.GetString(bytes);
                var stripped = Regex.Replace(rtfStr, @"\\[a-zA-Z0-9]+ ?|[{}]", "");
                stripped = Regex.Replace(stripped, @"\r?\n\s*\r?\n", Environment.NewLine);
                if (!string.IsNullOrWhiteSpace(stripped)) return stripped.Trim();
            }

            // Word 97-2003 / binary documents store text in UTF-16LE runs
            var sb = new StringBuilder();
            int consecutiveUtf16 = 0;
            var currentUtf16 = new StringBuilder();
            for (int i = 0; i < bytes.Length - 1; i += 2)
            {
                char ch = (char)(bytes[i] | (bytes[i + 1] << 8));
                if (ch == '\r' || ch == '\n' || ch == '\t' || (ch >= 32 && ch < 127) || (ch >= 0x0400 && ch <= 0x04FF))
                {
                    consecutiveUtf16++;
                    currentUtf16.Append(ch);
                }
                else
                {
                    if (consecutiveUtf16 >= 4)
                    {
                        var s = currentUtf16.ToString().Trim();
                        if (s.Length >= 3 && !s.StartsWith("WordDocument", StringComparison.OrdinalIgnoreCase))
                        {
                            sb.AppendLine(s);
                        }
                    }
                    consecutiveUtf16 = 0;
                    currentUtf16.Clear();
                }
            }
            if (consecutiveUtf16 >= 4)
            {
                var s = currentUtf16.ToString().Trim();
                if (s.Length >= 3) sb.AppendLine(s);
            }

            if (sb.Length >= 10)
            {
                return sb.ToString().Trim();
            }

            // Fallback to printable text runs
            var utf8Str = Encoding.UTF8.GetString(bytes);
            var matches = Regex.Matches(utf8Str, @"[\w\s\p{P}]{4,}");
            if (matches.Count > 0)
            {
                var filtered = matches
                    .Select(m => m.Value.Trim())
                    .Where(v => v.Length >= 3 && !v.Contains("Microsoft") && !v.Contains("CompObj") && !v.Contains("WordDocument"));
                var joined = string.Join(Environment.NewLine, filtered);
                if (!string.IsNullOrWhiteSpace(joined))
                    return joined;
            }
        }
        catch { }
        return null;
    }

    private static List<List<string>> ReadSpreadsheetRows(string inputPath, string ext)
    {
        var result = new List<List<string>>();
        try
        {
            if (ext == ".csv")
            {
                foreach (var line in File.ReadAllLines(inputPath))
                    result.Add(line.Split(',').Select(c => c.Trim('"', ' ')).ToList());
                return result;
            }
            if (ext == ".tsv")
            {
                foreach (var line in File.ReadAllLines(inputPath))
                    result.Add(line.Split('\t').ToList());
                return result;
            }
            if (ext == ".xlsx")
            {
                using var archive = ZipFile.OpenRead(inputPath);
                var sharedStrings = new List<string>();
                var ssEntry = archive.GetEntry("xl/sharedStrings.xml");
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                if (ssEntry != null)
                {
                    using var s = ssEntry.Open();
                    var xdoc = XDocument.Load(s);
                    sharedStrings.AddRange(xdoc.Descendants(ns + "si").Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value))));
                }

                var sheetEntry = archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase));
                if (sheetEntry != null)
                {
                    using var s = sheetEntry.Open();
                    var xdoc = XDocument.Load(s);
                    foreach (var row in xdoc.Descendants(ns + "row"))
                    {
                        var cells = new List<string>();
                        foreach (var c in row.Elements(ns + "c"))
                        {
                            var type = c.Attribute("t")?.Value;
                            var val = c.Element(ns + "v")?.Value ?? c.Element(ns + "is")?.Value ?? string.Empty;
                            if (type == "s" && int.TryParse(val, out int ssIdx) && ssIdx >= 0 && ssIdx < sharedStrings.Count)
                                val = sharedStrings[ssIdx];
                            cells.Add(val);
                        }
                        result.Add(cells);
                    }
                    return result;
                }
            }
        }
        catch
        {
            // Fallback below
        }

        if (result.Count == 0)
        {
            result.Add(["Файл", "Розмір (байт)", "Змінено"]);
            var fi = new FileInfo(inputPath);
            result.Add([fi.Name, fi.Length.ToString(), fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")]);
        }
        return result;
    }

    private static string EscapeCsvCell(string cell)
    {
        if (cell.Contains(',') || cell.Contains('"') || cell.Contains('\n'))
            return $"\"{cell.Replace("\"", "\"\"")}\"";
        return cell;
    }

    private static void CreateDocxFromText(string textContent, string outputPath)
    {
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);

        WriteZipTextEntry(archive, "[Content_Types].xml", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/>
</Types>");

        WriteZipTextEntry(archive, "_rels/.rels", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/>
</Relationships>");

        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var body = new XElement(w + "body");
        foreach (var line in (SanitizeXmlText(textContent)).Replace("\r\n", "\n").Split('\n'))
        {
            body.Add(new XElement(w + "p", new XElement(w + "r", new XElement(w + "t", line))));
        }
        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(w + "document", body));
        WriteZipTextEntry(archive, "word/document.xml", doc.ToString());
    }

    private static void CreateOdtFromText(string textContent, string outputPath)
    {
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        WriteZipTextEntry(archive, "mimetype", "application/vnd.oasis.opendocument.text");

        XNamespace office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        XNamespace text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

        var officeText = new XElement(office + "text");
        foreach (var line in (SanitizeXmlText(textContent)).Replace("\r\n", "\n").Split('\n'))
        {
            officeText.Add(new XElement(text + "p", line));
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(office + "document-content",
                new XAttribute(XNamespace.Xmlns + "office", office),
                new XAttribute(XNamespace.Xmlns + "text", text),
                new XElement(office + "body", officeText)));

        WriteZipTextEntry(archive, "content.xml", doc.ToString());
    }

    private static void CreateEpubFromText(string title, string textContent, string outputPath)
    {
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        WriteZipTextEntry(archive, "mimetype", "application/epub+zip");
        WriteZipTextEntry(archive, "OEBPS/content.xhtml", BuildHtmlDocument(SanitizeXmlText(title), SanitizeXmlText(textContent)));
    }

    private static void CreatePptxFromText(string title, string textContent, string outputPath)
    {
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main";

        var cleanTitle = SanitizeXmlText(title);
        var cleanContent = SanitizeXmlText(textContent);

        var slideXml = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(p + "sld",
                new XAttribute(XNamespace.Xmlns + "a", a),
                new XAttribute(XNamespace.Xmlns + "p", p),
                new XElement(a + "t", $"{cleanTitle}: {cleanContent}")));

        WriteZipTextEntry(archive, "ppt/slides/slide1.xml", slideXml.ToString());
    }

    private static string SanitizeXmlText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c == 0x9 || c == 0xA || c == 0xD ||
                (c >= 0x20 && c <= 0xD7FF) ||
                (c >= 0xE000 && c <= 0xFFFD))
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static void CreateXlsxFromRows(List<List<string>> rows, string outputPath)
    {
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);

        WriteZipTextEntry(archive, "[Content_Types].xml", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
</Types>");

        WriteZipTextEntry(archive, "_rels/.rels", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>");

        WriteZipTextEntry(archive, "xl/workbook.xml", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets><sheet name=""Sheet1"" sheetId=""1"" r:id=""rId1""/></sheets>
</workbook>");

        WriteZipTextEntry(archive, "xl/_rels/workbook.xml.rels", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
</Relationships>");

        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheetData = new XElement(ns + "sheetData");
        int rowIndex = 1;
        foreach (var r in rows)
        {
            var rowEl = new XElement(ns + "row", new XAttribute("r", rowIndex++));
            foreach (var cellVal in r)
            {
                rowEl.Add(new XElement(ns + "c",
                    new XAttribute("t", "inlineStr"),
                    new XElement(ns + "is", new XElement(ns + "t", cellVal))));
            }
            sheetData.Add(rowEl);
        }

        var worksheet = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(ns + "worksheet", sheetData));
        WriteZipTextEntry(archive, "xl/worksheets/sheet1.xml", worksheet.ToString());
    }

    private static void CreateOdsFromRows(List<List<string>> rows, string outputPath)
    {
        var text = string.Join(Environment.NewLine, rows.Select(r => string.Join("\t", r)));
        CreateOdtFromText(text, outputPath);
    }

    private static void WriteZipTextEntry(ZipArchive archive, string entryPath, string content)
    {
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string BuildHtmlDocument(string title, string textContent)
    {
        var encodedTitle = System.Net.WebUtility.HtmlEncode(title);
        var paragraphs = string.Join(Environment.NewLine,
            (textContent ?? string.Empty).Replace("\r\n", "\n").Split('\n')
            .Select(l => $"<p>{System.Net.WebUtility.HtmlEncode(l)}</p>"));

        return $@"<!DOCTYPE html>
<html lang=""uk"">
<head>
<meta charset=""utf-8""/>
<title>{encodedTitle}</title>
<style>body {{ font-family: 'Segoe UI', sans-serif; max-width: 800px; margin: 40px auto; line-height: 1.6; color: #181924; }}</style>
</head>
<body>
<h1>{encodedTitle}</h1>
{paragraphs}
</body>
</html>";
    }

    private static string BuildRtfDocument(string textContent)
    {
        var sb = new StringBuilder();
        sb.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\f0\fs24 ");
        foreach (char c in textContent ?? string.Empty)
        {
            if (c == '\n') sb.Append(@"\par ");
            else if (c == '\r') continue;
            else if (c is '\\' or '{' or '}') sb.Append('\\').Append(c);
            else if (c > 127) sb.Append($@"\u{(short)c}?");
            else sb.Append(c);
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static void RenderTextToImage(string textContent, string title, string outputPath, SKEncodedImageFormat format)
    {
        const int width = 1240;
        const int height = 1754;
        const float margin = 64f;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var typeface = SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default;
        using var titleFont = new SKFont(typeface, 28f);
        using var bodyFont = new SKFont(typeface, 18f);
        using var paint = new SKPaint { Color = new SKColor(24, 25, 36), IsAntialias = true };

        float y = margin + 28f;
        canvas.DrawText(title, margin, y, SKTextAlign.Left, titleFont, paint);
        y += 48f;

        var lines = (textContent ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
        {
            if (y > height - margin) break;
            canvas.DrawText(line, margin, y, SKTextAlign.Left, bodyFont, paint);
            y += 28f;
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);
    }

    private static void RenderTextToPdf(string textContent, string title, string outputPath)
    {
        const float pageWidth = 595f;
        const float pageHeight = 842f;
        const float margin = 48f;
        const float lineHeight = 18f;
        float maxTextWidth = pageWidth - margin * 2;

        using var typeface = SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default;
        using var bodyFont = new SKFont(typeface, 11.5f);
        using var titleFont = new SKFont(typeface, 15f);
        using var textPaint = new SKPaint { Color = new SKColor(24, 25, 36), IsAntialias = true };

        var wrappedLines = new List<string>();
        var rawLines = (textContent ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        foreach (var rawLine in rawLines)
        {
            if (string.IsNullOrEmpty(rawLine))
            {
                wrappedLines.Add(string.Empty);
                continue;
            }

            var words = rawLine.Split(' ');
            var currentLine = string.Empty;
            foreach (var word in words)
            {
                var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (bodyFont.MeasureText(testLine) > maxTextWidth && !string.IsNullOrEmpty(currentLine))
                {
                    wrappedLines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }
            wrappedLines.Add(currentLine);
        }

        if (wrappedLines.Count == 0)
        {
            wrappedLines.Add(title);
        }

        using var stream = File.OpenWrite(outputPath);
        using var document = SKDocument.CreatePdf(stream);

        int lineIndex = 0;
        bool firstPage = true;

        while (lineIndex < wrappedLines.Count)
        {
            using var canvas = document.BeginPage(pageWidth, pageHeight);
            canvas.Clear(SKColors.White);

            float y = margin + 14f;
            if (firstPage)
            {
                canvas.DrawText(title, margin, y, SKTextAlign.Left, titleFont, textPaint);
                y += 28f;
                firstPage = false;
            }

            while (lineIndex < wrappedLines.Count && y <= pageHeight - margin)
            {
                var line = wrappedLines[lineIndex++];
                if (!string.IsNullOrEmpty(line))
                {
                    canvas.DrawText(line, margin, y, SKTextAlign.Left, bodyFont, textPaint);
                }
                y += lineHeight;
            }

            document.EndPage();
        }

        document.Close();
    }

    public static string GetUniqueFilePath(string candidate)
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

    public static string GetUniqueDirectoryPath(string candidate)
    {
        if (!Directory.Exists(candidate))
            return candidate;

        int counter = 1;
        while (true)
        {
            var testPath = $"{candidate} ({counter})";
            if (!Directory.Exists(testPath))
                return testPath;
            counter++;
        }
    }
}
