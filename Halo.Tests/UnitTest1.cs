using System;
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
    public void FileTypeDetector_ShouldDetectAllCategories()
    {
        Assert.Equal(FileTypeCategory.Image, FileTypeDetector.Detect("test.png"));
        Assert.Equal(FileTypeCategory.Image, FileTypeDetector.Detect("art.jpg"));
        Assert.Equal(FileTypeCategory.Image, FileTypeDetector.Detect("photo.avif"));
        Assert.Equal(FileTypeCategory.Video, FileTypeDetector.Detect("video.mp4"));
        Assert.Equal(FileTypeCategory.Video, FileTypeDetector.Detect("clip.mkv"));
        Assert.Equal(FileTypeCategory.Pdf, FileTypeDetector.Detect("report.pdf"));
        Assert.Equal(FileTypeCategory.Document, FileTypeDetector.Detect("notes.docx"));
        Assert.Equal(FileTypeCategory.Document, FileTypeDetector.Detect("readme.md"));
        Assert.Equal(FileTypeCategory.Presentation, FileTypeDetector.Detect("slides.pptx"));
        Assert.Equal(FileTypeCategory.Presentation, FileTypeDetector.Detect("deck.odp"));
        Assert.Equal(FileTypeCategory.Spreadsheet, FileTypeDetector.Detect("budget.xlsx"));
        Assert.Equal(FileTypeCategory.Spreadsheet, FileTypeDetector.Detect("data.csv"));
        Assert.Equal(FileTypeCategory.Archive, FileTypeDetector.Detect("backup.zip"));
        Assert.Equal(FileTypeCategory.Archive, FileTypeDetector.Detect("pkg.tar.gz"));
        Assert.Equal(FileTypeCategory.Generic, FileTypeDetector.Detect("unknown.xyz"));
    }

    [Fact]
    public void DefaultActionsProvider_ShouldHaveAllThreeModesAndExactActions()
    {
        var defaults = DefaultActionsProvider.GetDefaults();
        Assert.NotEmpty(defaults);
        Assert.All(defaults, a => Assert.True(a.IsEnabled));

        // Mode 1: Конвертація (Shift) — ActionCategory.Convert
        var convertActions = defaults.Where(a => a.Category == ActionCategory.Convert).ToList();
        var imageConvertActions = convertActions.Where(a => a.SupportedFileTypes.Contains(FileTypeCategory.Image)).ToList();
        Assert.Equal(6, imageConvertActions.Count);
        Assert.DoesNotContain(imageConvertActions, a => a.IconKey is "MP4" or "WEBM" or "PDF");
        Assert.Equal(6, convertActions.Count(a => a.SupportedFileTypes.Contains(FileTypeCategory.Video)));
        Assert.Equal(6, convertActions.Count(a => a.SupportedFileTypes.Contains(FileTypeCategory.Pdf)));
        Assert.Equal(8, convertActions.Count(a => a.SupportedFileTypes.Contains(FileTypeCategory.Document)));
        Assert.Equal(5, convertActions.Count(a => a.SupportedFileTypes.Contains(FileTypeCategory.Presentation)));
        Assert.Equal(5, convertActions.Count(a => a.SupportedFileTypes.Contains(FileTypeCategory.Spreadsheet)));
        Assert.Equal(4, convertActions.Count(a => a.SupportedFileTypes.Contains(FileTypeCategory.Archive)));

        // Every Convert action must use an uppercase format badge text as IconKey and preserve AccentColor
        Assert.All(convertActions, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.IconKey));
            Assert.Equal(a.IconKey.ToUpperInvariant(), a.IconKey);
            Assert.All(a.IconKey, ch => Assert.True(ch <= 127 && (char.IsLetterOrDigit(ch) || ch == '.')));
            Assert.False(string.IsNullOrWhiteSpace(a.AccentColor));
        });

        // Mode 2: Інструменти (Ctrl) — ActionCategory.Edit
        var toolActions = defaults.Where(a => a.Category == ActionCategory.Edit).ToList();
        // Verify removed tools are completely absent
        Assert.DoesNotContain(toolActions, a => a.Id == "tool_img_resize_50");
        Assert.DoesNotContain(toolActions, a => a.Id == "tool_img_rotate_right");
        Assert.DoesNotContain(toolActions, a => a.Id == "tool_img_rotate_left");
        Assert.DoesNotContain(toolActions, a => a.Id == "tool_img_grayscale");
        Assert.DoesNotContain(toolActions, a => a.Id == "tool_img_strip_metadata");
        Assert.DoesNotContain(toolActions, a => a.Id == "tool_img_merge_pdf");

        // Verify dynamic category-specific tools exist for each file type
        Assert.Contains(toolActions, a => a.SupportedFileTypes.Contains(FileTypeCategory.Image));
        Assert.Contains(toolActions, a => a.SupportedFileTypes.Contains(FileTypeCategory.Video));
        Assert.Contains(toolActions, a => a.SupportedFileTypes.Contains(FileTypeCategory.Pdf));
        Assert.Contains(toolActions, a => a.SupportedFileTypes.Contains(FileTypeCategory.Document));
        Assert.Contains(toolActions, a => a.SupportedFileTypes.Contains(FileTypeCategory.Presentation));
        Assert.Contains(toolActions, a => a.SupportedFileTypes.Contains(FileTypeCategory.Spreadsheet));
        Assert.Contains(toolActions, a => a.SupportedFileTypes.Contains(FileTypeCategory.Archive));

        // Verify spreadsheets do NOT receive image tools
        var sheetTools = toolActions.Where(a => a.SupportedFileTypes.Contains(FileTypeCategory.Spreadsheet)).ToList();
        Assert.DoesNotContain(sheetTools, a => a.Id.StartsWith("tool_img_"));
        Assert.Contains(sheetTools, a => a.Id == "tool_sheet_dedup");
        Assert.Contains(sheetTools, a => a.Id == "tool_sheet_sort");

        // Mode 3: Дії (Alt) — ActionCategory.System
        var systemActions = defaults.Where(a => a.Category == ActionCategory.System).ToList();
        Assert.Equal(7, systemActions.Count);
        Assert.Contains(systemActions, a => a.Id == "act_copy_to_folder");
        Assert.Contains(systemActions, a => a.Id == "act_move_to_folder");
        Assert.Contains(systemActions, a => a.Id == "act_copy_path");
        Assert.Contains(systemActions, a => a.Id == "act_open_terminal");
        Assert.Contains(systemActions, a => a.Id == "act_shelf_open");
        Assert.Contains(systemActions, a => a.Id == "act_shelf_add");
        Assert.Contains(systemActions, a => a.Id == "act_windows_share");

        // Localization check: English localization translates all titles and descriptions
        DefaultActionsProvider.LocalizeActions(defaults, "en");
        Assert.All(defaults, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Title));
            Assert.False(string.IsNullOrWhiteSpace(a.Description));
        });
        Assert.Equal("Open Shelf", defaults.First(a => a.Id == "act_shelf_open").Title);
        Assert.Equal("Add to Shelf", defaults.First(a => a.Id == "act_shelf_add").Title);
    }

    [Fact]
    public async Task ImageProcessor_And_ActionExecutor_ShouldExecuteConversionsToolsAndArchives()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"halo_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempPng = Path.Combine(tempDir, "sample.png");

        try
        {
            using (var bmp = new SKBitmap(100, 60))
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

            // Image conversions: JPG, WEBP, TIFF, ICO, PDF
            var jpgRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "img_to_jpg", Title = "У JPG" }, tempPng);
            Assert.True(jpgRes.Success && File.Exists(jpgRes.OutputPath));

            var webpRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "img_to_webp", Title = "У WEBP" }, tempPng);
            Assert.True(webpRes.Success && File.Exists(webpRes.OutputPath));

            var tiffRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "img_to_tiff", Title = "У TIFF" }, tempPng);
            Assert.True(tiffRes.Success && File.Exists(tiffRes.OutputPath));

            var icoRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "img_to_ico", Title = "У ICO" }, tempPng);
            Assert.True(icoRes.Success && File.Exists(icoRes.OutputPath));

            // Direct PDF conversion helper (used by Dashboard "Зберегти в PDF")
            var pdfPath = ImageProcessor.ConvertToPdf(tempPng);
            Assert.True(File.Exists(pdfPath));

            // Image tools: Compress, Crop Square, Flip Horizontal, White Border
            var compressRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "tool_img_compress", Title = "Оптимізувати розмір" }, tempPng);
            Assert.True(compressRes.Success && File.Exists(compressRes.OutputPath));

            var cropSqRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "tool_img_crop_square", Title = "Кадрувати 1:1" }, tempPng);
            Assert.True(cropSqRes.Success && File.Exists(cropSqRes.OutputPath));

            var flipRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "tool_img_flip_h", Title = "Віддзеркалити" }, tempPng);
            Assert.True(flipRes.Success && File.Exists(flipRes.OutputPath));

            var borderRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "tool_img_border", Title = "Додати білу рамку" }, tempPng);
            Assert.True(borderRes.Success && File.Exists(borderRes.OutputPath));

            // Shelf actions
            string? addedToShelfPath = null;
            bool shelfOpened = false;
            var shelfAddRes = await ActionExecutor.ExecuteAsync(
                new ActionItem { Id = "act_shelf_add", Title = "Додати на полицю" },
                tempPng,
                onAddToShelf: p => addedToShelfPath = p,
                onOpenShelf: () => shelfOpened = true);
            Assert.True(shelfAddRes.Success && addedToShelfPath == tempPng && shelfOpened);

            shelfOpened = false;
            var shelfOpenRes = await ActionExecutor.ExecuteAsync(
                new ActionItem { Id = "act_shelf_open", Title = "Відкрити полицю" },
                tempPng,
                onOpenShelf: () => shelfOpened = true);
            Assert.True(shelfOpenRes.Success && shelfOpened);

            // Archives: ZIP, TAR, TAR.GZ, Extract
            var zipRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "arch_to_zip", Title = "Запакувати в ZIP" }, tempPng);
            Assert.True(zipRes.Success && File.Exists(zipRes.OutputPath));

            var tarRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "arch_to_tar", Title = "Запакувати в TAR" }, tempPng);
            Assert.True(tarRes.Success && File.Exists(tarRes.OutputPath));

            var targzRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "arch_to_targz", Title = "Запакувати в TAR.GZ" }, tempPng);
            Assert.True(targzRes.Success && File.Exists(targzRes.OutputPath));

            var extractRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "arch_extract", Title = "Розпакувати" }, zipRes.OutputPath!);
            Assert.True(extractRes.Success && Directory.Exists(extractRes.OutputPath));

            // Document & Spreadsheet conversions
            var tempTxt = Path.Combine(tempDir, "notes.txt");
            File.WriteAllText(tempTxt, "Привіт Halo!\nДругий рядок документа.");

            var docxRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "doc_to_docx", Title = "У DOCX" }, tempTxt);
            Assert.True(docxRes.Success && File.Exists(docxRes.OutputPath));

            var htmlRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "doc_to_html", Title = "У HTML" }, tempTxt);
            Assert.True(htmlRes.Success && File.Exists(htmlRes.OutputPath));

            var tempCsv = Path.Combine(tempDir, "table.csv");
            File.WriteAllText(tempCsv, "Ім'я,Значення\nHalo,100\nHalo,100\nAlpha,50");

            var xlsxRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "sheet_to_xlsx", Title = "У XLSX" }, tempCsv);
            Assert.True(xlsxRes.Success && File.Exists(xlsxRes.OutputPath));

            var dedupRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "tool_sheet_dedup", Title = "Видалити дублікати" }, tempCsv);
            Assert.True(dedupRes.Success && File.Exists(dedupRes.OutputPath));

            var sortSheetRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "tool_sheet_sort", Title = "Сортувати таблицю" }, tempCsv);
            Assert.True(sortSheetRes.Success && File.Exists(sortSheetRes.OutputPath));

            var transposeRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "tool_sheet_transpose", Title = "Транспонувати" }, tempCsv);
            Assert.True(transposeRes.Success && File.Exists(transposeRes.OutputPath));

            var docStatsRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "tool_doc_stats", Title = "Статистика тексту" }, tempTxt);
            Assert.True(docStatsRes.Success && File.Exists(docStatsRes.OutputPath));

            var archListRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "tool_arch_list", Title = "Список файлів архіву" }, zipRes.OutputPath!);
            Assert.True(archListRes.Success && File.Exists(archListRes.OutputPath));

            var archVerifyRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "tool_arch_verify", Title = "Перевірити цілісність" }, zipRes.OutputPath!);
            Assert.True(archVerifyRes.Success);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
                // Background handles or indexing locks can be cleaned up on next run
            }
        }
    }

    [Fact]
    public async Task DocumentToPdf_And_DockConversions_ShouldSucceed()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"halo_doc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. Plain text to PDF
            var txtPath = Path.Combine(tempDir, "sample.txt");
            File.WriteAllText(txtPath, "Тестовий документ Halo\nРядок 2 з важливим текстом.\nРядок 3.");
            var txtPdfRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "doc_to_pdf", Title = "Формат PDF" }, txtPath);
            Assert.True(txtPdfRes.Success && File.Exists(txtPdfRes.OutputPath));
            Assert.True(new FileInfo(txtPdfRes.OutputPath).Length > 100);

            // 2. .dock file to PDF (testing typo/phonetic extension handling)
            var dockPath = Path.Combine(tempDir, "contract.dock");
            File.WriteAllText(dockPath, "Договір про надання послуг Halo.\nСторона 1 та Сторона 2.\nУсі умови виконано.");
            Assert.Equal(FileTypeCategory.Document, FileTypeDetector.Detect(dockPath));
            var dockPdfRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "doc_to_pdf", Title = "Формат PDF" }, dockPath);
            Assert.True(dockPdfRes.Success && File.Exists(dockPdfRes.OutputPath));
            Assert.True(new FileInfo(dockPdfRes.OutputPath).Length > 100);

            // 3. Binary .doc file with UTF-16LE text stream
            var docPath = Path.Combine(tempDir, "legacy_word.doc");
            var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, System.Text.Encoding.Unicode, leaveOpen: true))
            {
                // Write OLE header placeholder bytes
                writer.Write(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 });
                writer.Write(new byte[64]);
                // Write readable Unicode text paragraph
                writer.Write("Офіційний документ Word 97-2003 у форматі DOC.\nДругий абзац тексту.");
                writer.Write(new byte[32]);
            }
            File.WriteAllBytes(docPath, ms.ToArray());

            var docPdfRes = await ActionExecutor.ExecuteAsync(new ActionItem { Id = "doc_to_pdf", Title = "Формат PDF" }, docPath);
            Assert.True(docPdfRes.Success && File.Exists(docPdfRes.OutputPath));
            Assert.True(new FileInfo(docPdfRes.OutputPath).Length > 100);

            // Verify the generated PDF files all start with standard PDF header "%PDF-"
            var pdfBytes = File.ReadAllBytes(docPdfRes.OutputPath);
            Assert.True(pdfBytes.Length >= 5);
            var header = System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 5);
            Assert.Equal("%PDF-", header);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
                // Background handles or indexing locks can be cleaned up on next run
            }
        }
    }

    [Fact]
    public void OperationHistoryService_ShouldRecord_AndTrackOperations()
    {
        var service = OperationHistoryService.Instance;
        service.ClearHistory();
        Assert.Empty(service.GetRecentOperations());

        service.RecordOperation(new OperationRecord
        {
            ActionId = "img_to_jpg",
            Title = "Конвертація у JPG",
            Description = "photo.png → photo.jpg",
            CanUndo = false,
            CreatedFiles = ["C:\\test\\photo.jpg"]
        });

        service.RecordOperation(new OperationRecord
        {
            ActionId = "video_trim",
            Title = "Обрізка відео",
            Description = "clip.mp4 → clip_trimmed.mp4",
            CanUndo = false,
            CreatedFiles = ["C:\\test\\clip_trimmed.mp4"]
        });

        var ops = service.GetRecentOperations();
        Assert.Equal(2, ops.Count);
        Assert.Equal("video_trim", ops[0].ActionId);
        Assert.False(ops[0].IsUndone);

        service.ClearHistory();
        Assert.Empty(service.GetRecentOperations());
    }

    [Fact]
    public void Localization_ShouldProvideCompleteUkrainianAndEnglishStrings()
    {
        var defaults = DefaultActionsProvider.GetDefaults("uk");
        Assert.NotEmpty(defaults);

        // Ukrainian checks
        foreach (var action in defaults)
        {
            Assert.False(string.IsNullOrWhiteSpace(action.Title), $"Empty UK title for action {action.Id}");
            Assert.False(string.IsNullOrWhiteSpace(action.Description), $"Empty UK desc for action {action.Id}");
        }

        // Switch to English
        DefaultActionsProvider.LocalizeActions(defaults, "en");
        foreach (var action in defaults)
        {
            Assert.False(string.IsNullOrWhiteSpace(action.Title), $"Empty EN title for action {action.Id}");
            Assert.False(string.IsNullOrWhiteSpace(action.Description), $"Empty EN desc for action {action.Id}");
            // English titles must not contain distinct Ukrainian letters (і, ї, є, ґ)
            Assert.DoesNotContain("і", action.Title);
            Assert.DoesNotContain("ї", action.Title);
            Assert.DoesNotContain("є", action.Title);
            Assert.DoesNotContain("ґ", action.Title);
        }
    }

    [Fact]
    public void CrashReporterService_ShouldGenerateDumpFile_AndSafelyFlushUserData()
    {
        var reporter = CrashReporterService.Instance;
        Assert.False(string.IsNullOrWhiteSpace(reporter.CrashDirectory));

        var testEx = new InvalidOperationException("Simulation of an unhandled runtime error for test suite.");
        string dumpPath = reporter.LogCrash(testEx, "Unit Testing Suite", isFatal: false);

        Assert.True(File.Exists(dumpPath));
        string content = File.ReadAllText(dumpPath);
        Assert.Contains("HALO APPLICATION CRASH REPORT / ERROR DUMP", content);
        Assert.Contains("Unit Testing Suite", content);
        Assert.Contains("Simulation of an unhandled runtime error for test suite.", content);

        var recent = reporter.GetRecentCrashLogs();
        Assert.NotEmpty(recent);
        Assert.Contains(dumpPath, recent);

        // Verify SafeFlushUserData does not throw
        var record = new Exception();
        var ex = Record.Exception(() => reporter.SafeFlushUserData());
        Assert.Null(ex);
    }

    [Fact]
    public void TrimVideoRange_ShouldInvokeCallbackWithFalse_WhenFileNotFound()
    {
        bool callbackInvoked = false;
        bool callbackSuccess = true;
        string? callbackPath = null;

        var ex = Assert.Throws<FileNotFoundException>(() =>
        {
            ImageProcessor.TrimVideoRange(
                "C:\\non_existent_directory_123\\video_not_found.mp4",
                TimeSpan.Zero,
                TimeSpan.FromSeconds(10),
                onCompleted: (success, path) =>
                {
                    callbackInvoked = true;
                    callbackSuccess = success;
                    callbackPath = path;
                });
        });

        Assert.NotNull(ex);
        Assert.True(callbackInvoked);
        Assert.False(callbackSuccess);
        Assert.Equal(string.Empty, callbackPath);
    }
}
