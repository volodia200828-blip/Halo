using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Halo.Core.Models;
using Halo.Core.Services;
using SkiaSharp;
using Xunit;

namespace Halo.Tests;

public class CoreTests
{
    [Fact]
    public void FileTypeDetector_ShouldDetectCorrectCategories()
    {
        Assert.Equal(FileTypeCategory.Image, FileTypeDetector.Detect("test.png"));
        Assert.Equal(FileTypeCategory.Image, FileTypeDetector.Detect("photo.WEBP"));
        Assert.Equal(FileTypeCategory.Image, FileTypeDetector.Detect("art.jpg"));
        Assert.Equal(FileTypeCategory.Video, FileTypeDetector.Detect("video.mp4"));
        Assert.Equal(FileTypeCategory.Audio, FileTypeDetector.Detect("song.mp3"));
        Assert.Equal(FileTypeCategory.Document, FileTypeDetector.Detect("report.pdf"));
        Assert.Equal(FileTypeCategory.Document, FileTypeDetector.Detect("notes.docx"));
        Assert.Equal(FileTypeCategory.Generic, FileTypeDetector.Detect("archive.xyz"));
    }

    [Fact]
    public void DefaultActionsProvider_ShouldHaveEssentialActions()
    {
        var defaults = DefaultActionsProvider.GetDefaults();
        Assert.NotEmpty(defaults);

        var webpAction = defaults.FirstOrDefault(a => a.Id == "image_to_webp");
        Assert.NotNull(webpAction);
        Assert.Contains(FileTypeCategory.Image, webpAction.SupportedFileTypes);
        Assert.True(webpAction.IsEnabled);

        var copyAction = defaults.FirstOrDefault(a => a.Id == "system_copy_path");
        Assert.NotNull(copyAction);
    }

    [Fact]
    public async Task ImageProcessor_ShouldConvertResizeAndGrayscale()
    {
        // 1. Create a temporary bitmap using SkiaSharp
        var tempPng = Path.Combine(Path.GetTempPath(), $"halo_test_{System.Guid.NewGuid():N}.png");
        try
        {
            using (var bmp = new SKBitmap(100, 100))
            {
                using (var canvas = new SKCanvas(bmp))
                {
                    canvas.Clear(SKColors.CornflowerBlue);
                }
                using var img = SKImage.FromBitmap(bmp);
                using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                using var fs = File.OpenWrite(tempPng);
                data.SaveTo(fs);
            }

            // 2. Test WebP conversion
            var webpOut = ImageProcessor.ConvertFormat(tempPng, SKEncodedImageFormat.Webp);
            Assert.True(File.Exists(webpOut));
            Assert.True(new FileInfo(webpOut).Length > 0);
            File.Delete(webpOut);

            // 3. Test Resize (50%)
            var resizeOut = ImageProcessor.ResizeImage(tempPng, 0.5);
            Assert.True(File.Exists(resizeOut));
            using (var resBmp = SKBitmap.Decode(resizeOut))
            {
                Assert.Equal(50, resBmp.Width);
                Assert.Equal(50, resBmp.Height);
            }
            File.Delete(resizeOut);

            // 4. Test Grayscale
            var grayOut = ImageProcessor.ApplyGrayscale(tempPng);
            Assert.True(File.Exists(grayOut));
            File.Delete(grayOut);

            // 5. Test ActionExecutor
            var action = new ActionItem
            {
                Id = "image_to_jpg",
                Title = "У JPG",
                Category = ActionCategory.Convert
            };
            var execResult = await ActionExecutor.ExecuteAsync(action, tempPng);
            Assert.True(execResult.Success);
            Assert.NotNull(execResult.OutputPath);
            Assert.True(File.Exists(execResult.OutputPath));
            File.Delete(execResult.OutputPath);
        }
        finally
        {
            if (File.Exists(tempPng))
            {
                File.Delete(tempPng);
            }
        }
    }
}
