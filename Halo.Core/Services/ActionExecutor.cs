using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Halo.Core.Models;
using SkiaSharp;

namespace Halo.Core.Services;

public class ActionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
}

public static class ActionExecutor
{
    private static bool IsEn => string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
    private static string T(string uk, string en) => IsEn ? en : uk;

    public static Task<ActionResult> ExecuteAsync(
        ActionItem action,
        string targetPath,
        Action<string>? onOpenSettings = null,
        Action<string>? onAddToShelf = null,
        string? destinationFolder = null,
        Action? onOpenShelf = null)
    {
        return ExecuteAsync(action, string.IsNullOrWhiteSpace(targetPath) ? [] : [targetPath], onOpenSettings, onAddToShelf, destinationFolder, onOpenShelf);
    }

    public static async Task<ActionResult> ExecuteAsync(
        ActionItem action,
        IReadOnlyList<string> targetPaths,
        Action<string>? onOpenSettings = null,
        Action<string>? onAddToShelf = null,
        string? destinationFolder = null,
        Action? onOpenShelf = null)
    {
        return await Task.Run(() =>
        {
            try
            {
                var result = ExecuteInternal(action, targetPaths, onOpenSettings, onAddToShelf, destinationFolder, onOpenShelf);
                if (result.Success && action.Id != "act_move_to_folder")
                {
                    string target = targetPaths != null && targetPaths.Count > 0 ? targetPaths[0] : string.Empty;
                    var created = new List<string>();
                    if (!string.IsNullOrWhiteSpace(result.OutputPath) &&
                        (File.Exists(result.OutputPath) || Directory.Exists(result.OutputPath)) &&
                        !string.Equals(result.OutputPath, target, StringComparison.OrdinalIgnoreCase))
                    {
                        created.Add(result.OutputPath);
                    }

                    if (created.Count > 0)
                    {
                        string fileName = Path.GetFileName(target);
                        string outName = Path.GetFileName(result.OutputPath ?? string.Empty);
                        string desc = string.Equals(fileName, outName, StringComparison.OrdinalIgnoreCase)
                            ? result.Message
                            : $"{fileName} → {outName}";

                        OperationHistoryService.Instance.RecordOperation(new OperationRecord
                        {
                            ActionId = action.Id ?? string.Empty,
                            Title = !string.IsNullOrWhiteSpace(action.Title) ? action.Title : result.Message,
                            Description = desc,
                            CanUndo = false,
                            CreatedFiles = created
                        });
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                return new ActionResult { Success = false, Message = T($"Помилка: {ex.Message}", $"Error: {ex.Message}") };
            }
        });
    }

    private static ActionResult ExecuteInternal(
        ActionItem action,
        IReadOnlyList<string> targetPaths,
        Action<string>? onOpenSettings = null,
        Action<string>? onAddToShelf = null,
        string? destinationFolder = null,
        Action? onOpenShelf = null)
    {
        var id = action.Id?.Trim() ?? string.Empty;

                // Actions that do not require a file path
                if (id == "act_shelf_open" || id == "system_shelf_open")
                {
                    onOpenShelf?.Invoke();
                    return new ActionResult { Success = true, Message = T("Полицю відкрито", "Shelf opened") };
                }

                if (id == "system_open_settings")
                {
                    var first = targetPaths != null && targetPaths.Count > 0 ? targetPaths[0] : string.Empty;
                    onOpenSettings?.Invoke(first);
                    return new ActionResult { Success = true, Message = T("Відкрито налаштування", "Settings opened") };
                }

                if (targetPaths == null || targetPaths.Count == 0 || string.IsNullOrWhiteSpace(targetPaths[0]))
                {
                    if (id == "act_shelf_add" || id == "system_shelf_add")
                    {
                        onOpenShelf?.Invoke();
                        return new ActionResult { Success = true, Message = T("Полицю відкрито", "Shelf opened") };
                    }
                    return new ActionResult { Success = false, Message = T("Файл не вибрано", "No file selected") };
                }

                var targetPath = targetPaths[0];

                if (action.IsCustom && !string.IsNullOrWhiteSpace(action.CustomCommand))
                {
                    return ExecuteCustomCommand(action, targetPath);
                }

                // Normalize legacy titles if needed
                if (action.Title != null && action.Title.Contains("PDF", StringComparison.OrdinalIgnoreCase) &&
                    (action.Title.Contains("Зберегти", StringComparison.OrdinalIgnoreCase) ||
                     action.Title.Contains("Save", StringComparison.OrdinalIgnoreCase) ||
                     action.Title.Contains("У PDF", StringComparison.OrdinalIgnoreCase)) &&
                    string.IsNullOrEmpty(id))
                {
                    id = "doc_to_pdf";
                }

                switch (id)
                {
                    // ==========================================
                    // РЕЖИМ 1: КОНВЕРТАЦІЯ — ЗОБРАЖЕННЯ
                    // ==========================================
                    case "img_to_jpg":
                    case "image_to_jpg":
                    {
                        var outPath = ImageProcessor.ConvertFormat(targetPath, SKEncodedImageFormat.Jpeg);
                        return new ActionResult { Success = true, Message = T("Конвертовано у JPG", "Converted to JPG"), OutputPath = outPath };
                    }
                    case "img_to_png":
                    case "image_to_png":
                    {
                        var outPath = ImageProcessor.ConvertFormat(targetPath, SKEncodedImageFormat.Png);
                        return new ActionResult { Success = true, Message = T("Конвертовано у PNG", "Converted to PNG"), OutputPath = outPath };
                    }
                    case "img_to_webp":
                    case "image_to_webp":
                    {
                        var outPath = ImageProcessor.ConvertFormat(targetPath, SKEncodedImageFormat.Webp);
                        return new ActionResult { Success = true, Message = T("Конвертовано у WEBP", "Converted to WEBP"), OutputPath = outPath };
                    }
                    case "img_to_avif":
                    {
                        var outPath = ImageProcessor.ConvertToAvif(targetPath);
                        return new ActionResult { Success = true, Message = T("Конвертовано у AVIF", "Converted to AVIF"), OutputPath = outPath };
                    }
                    case "img_to_tiff":
                    {
                        var outPath = ImageProcessor.ConvertToTiff(targetPath);
                        return new ActionResult { Success = true, Message = T("Конвертовано у TIFF", "Converted to TIFF"), OutputPath = outPath };
                    }
                    case "img_to_ico":
                    {
                        var outPath = ImageProcessor.ConvertToIco(targetPath);
                        return new ActionResult { Success = true, Message = T("Конвертовано у ICO", "Converted to ICO"), OutputPath = outPath };
                    }
                    case "img_to_pdf":
                    case "image_to_pdf":
                    {
                        var outPath = targetPaths.Count > 1
                            ? ImageProcessor.MergeImagesToPdf(targetPaths)
                            : ImageProcessor.ConvertToPdf(targetPath);
                        return new ActionResult { Success = true, Message = T("Збережено у PDF", "Saved to PDF"), OutputPath = outPath };
                    }
                    case "img_to_mp4":
                    {
                        var outPath = ImageProcessor.ConvertImageToVideo(targetPath, ".mp4");
                        return new ActionResult { Success = true, Message = T("Створено відео MP4", "Created MP4 video"), OutputPath = outPath };
                    }
                    case "img_to_webm":
                    {
                        var outPath = ImageProcessor.ConvertImageToVideo(targetPath, ".webm");
                        return new ActionResult { Success = true, Message = T("Створено відео WEBM", "Created WEBM video"), OutputPath = outPath };
                    }

                    // ==========================================
                    // РЕЖИМ 1: КОНВЕРТАЦІЯ — ВІДЕО
                    // ==========================================
                    case "vid_to_mp4":
                    {
                        var outPath = ImageProcessor.ConvertVideo(targetPath, ".mp4");
                        return new ActionResult { Success = true, Message = T("Конвертовано у MP4", "Converted to MP4"), OutputPath = outPath };
                    }
                    case "vid_to_mov":
                    {
                        var outPath = ImageProcessor.ConvertVideo(targetPath, ".mov");
                        return new ActionResult { Success = true, Message = T("Конвертовано у MOV", "Converted to MOV"), OutputPath = outPath };
                    }
                    case "vid_to_mkv":
                    {
                        var outPath = ImageProcessor.ConvertVideo(targetPath, ".mkv");
                        return new ActionResult { Success = true, Message = T("Конвертовано у MKV", "Converted to MKV"), OutputPath = outPath };
                    }
                    case "vid_to_webm":
                    {
                        var outPath = ImageProcessor.ConvertVideo(targetPath, ".webm");
                        return new ActionResult { Success = true, Message = T("Конвертовано у WEBM", "Converted to WEBM"), OutputPath = outPath };
                    }
                    case "vid_to_gif":
                    {
                        var outPath = ImageProcessor.ConvertVideo(targetPath, ".gif");
                        return new ActionResult { Success = true, Message = T("Створено анімацію GIF", "Created GIF animation"), OutputPath = outPath };
                    }
                    case "vid_to_mp3":
                    {
                        var outPath = ImageProcessor.ConvertVideo(targetPath, ".mp3");
                        return new ActionResult { Success = true, Message = T("Звук вилучено у MP3", "Extracted audio to MP3"), OutputPath = outPath };
                    }

                    // ==========================================
                    // РЕЖИМ 1: КОНВЕРТАЦІЯ — PDF
                    // ==========================================
                    case "pdf_to_docx":
                    {
                        var outPath = ImageProcessor.ConvertPdf(targetPath, "docx");
                        return new ActionResult { Success = true, Message = T("PDF конвертовано у DOCX", "PDF converted to DOCX"), OutputPath = outPath };
                    }
                    case "pdf_to_jpg":
                    {
                        var outPath = ImageProcessor.ConvertPdf(targetPath, "jpg");
                        return new ActionResult { Success = true, Message = T("PDF конвертовано у JPG", "PDF converted to JPG"), OutputPath = outPath };
                    }
                    case "pdf_to_png":
                    {
                        var outPath = ImageProcessor.ConvertPdf(targetPath, "png");
                        return new ActionResult { Success = true, Message = T("PDF конвертовано у PNG", "PDF converted to PNG"), OutputPath = outPath };
                    }
                    case "pdf_to_pptx":
                    {
                        var outPath = ImageProcessor.ConvertPdf(targetPath, "pptx");
                        return new ActionResult { Success = true, Message = T("PDF конвертовано у PPTX", "PDF converted to PPTX"), OutputPath = outPath };
                    }
                    case "pdf_to_txt":
                    {
                        var outPath = ImageProcessor.ConvertPdf(targetPath, "txt");
                        return new ActionResult { Success = true, Message = T("PDF конвертовано у TXT", "PDF converted to TXT"), OutputPath = outPath };
                    }
                    case "pdf_to_html":
                    {
                        var outPath = ImageProcessor.ConvertPdf(targetPath, "html");
                        return new ActionResult { Success = true, Message = T("PDF конвертовано у HTML", "PDF converted to HTML"), OutputPath = outPath };
                    }

                    // ==========================================
                    // РЕЖИМ 1: КОНВЕРТАЦІЯ — ДОКУМЕНТИ
                    // ==========================================
                    case "doc_to_docx":
                    {
                        var outPath = ImageProcessor.ConvertDocument(targetPath, "docx");
                        return new ActionResult { Success = true, Message = T("Конвертовано у DOCX", "Converted to DOCX"), OutputPath = outPath };
                    }
                    case "doc_to_pdf":
                    {
                        var outPath = ImageProcessor.ConvertToPdf(targetPath);
                        return new ActionResult { Success = true, Message = T("Збережено у PDF", "Saved to PDF"), OutputPath = outPath };
                    }
                    case "doc_to_odt":
                    {
                        var outPath = ImageProcessor.ConvertDocument(targetPath, "odt");
                        return new ActionResult { Success = true, Message = T("Конвертовано у ODT", "Converted to ODT"), OutputPath = outPath };
                    }
                    case "doc_to_md":
                    {
                        var outPath = ImageProcessor.ConvertDocument(targetPath, "md");
                        return new ActionResult { Success = true, Message = T("Конвертовано у Markdown", "Converted to Markdown"), OutputPath = outPath };
                    }
                    case "doc_to_html":
                    {
                        var outPath = ImageProcessor.ConvertDocument(targetPath, "html");
                        return new ActionResult { Success = true, Message = T("Конвертовано у HTML", "Converted to HTML"), OutputPath = outPath };
                    }
                    case "doc_to_txt":
                    {
                        var outPath = ImageProcessor.ConvertDocument(targetPath, "txt");
                        return new ActionResult { Success = true, Message = T("Конвертовано у TXT", "Converted to TXT"), OutputPath = outPath };
                    }
                    case "doc_to_epub":
                    {
                        var outPath = ImageProcessor.ConvertDocument(targetPath, "epub");
                        return new ActionResult { Success = true, Message = T("Конвертовано у EPUB", "Converted to EPUB"), OutputPath = outPath };
                    }
                    case "doc_to_rtf":
                    {
                        var outPath = ImageProcessor.ConvertDocument(targetPath, "rtf");
                        return new ActionResult { Success = true, Message = T("Конвертовано у RTF", "Converted to RTF"), OutputPath = outPath };
                    }

                    // ==========================================
                    // РЕЖИМ 1: КОНВЕРТАЦІЯ — ПРЕЗЕНТАЦІЇ
                    // ==========================================
                    case "pres_to_pdf":
                    {
                        var outPath = ImageProcessor.ConvertPresentation(targetPath, "pdf");
                        return new ActionResult { Success = true, Message = T("Презентацію збережено у PDF", "Presentation saved to PDF"), OutputPath = outPath };
                    }
                    case "pres_to_png":
                    {
                        var outPath = ImageProcessor.ConvertPresentation(targetPath, "png");
                        return new ActionResult { Success = true, Message = T("Слайди збережено у PNG", "Slides saved to PNG"), OutputPath = outPath };
                    }
                    case "pres_to_jpg":
                    {
                        var outPath = ImageProcessor.ConvertPresentation(targetPath, "jpg");
                        return new ActionResult { Success = true, Message = T("Слайди збережено у JPG", "Slides saved to JPG"), OutputPath = outPath };
                    }
                    case "pres_to_pptx":
                    {
                        var outPath = ImageProcessor.ConvertPresentation(targetPath, "pptx");
                        return new ActionResult { Success = true, Message = T("Конвертовано у PPTX", "Converted to PPTX"), OutputPath = outPath };
                    }
                    case "pres_to_odp":
                    {
                        var outPath = ImageProcessor.ConvertPresentation(targetPath, "odp");
                        return new ActionResult { Success = true, Message = T("Конвертовано у ODP", "Converted to ODP"), OutputPath = outPath };
                    }

                    // ==========================================
                    // РЕЖИМ 1: КОНВЕРТАЦІЯ — ТАБЛИЦІ
                    // ==========================================
                    case "sheet_to_xlsx":
                    {
                        var outPath = ImageProcessor.ConvertSpreadsheet(targetPath, "xlsx");
                        return new ActionResult { Success = true, Message = T("Таблицю збережено у XLSX", "Spreadsheet saved to XLSX"), OutputPath = outPath };
                    }
                    case "sheet_to_csv":
                    {
                        var outPath = ImageProcessor.ConvertSpreadsheet(targetPath, "csv");
                        return new ActionResult { Success = true, Message = T("Таблицю збережено у CSV", "Spreadsheet saved to CSV"), OutputPath = outPath };
                    }
                    case "sheet_to_tsv":
                    {
                        var outPath = ImageProcessor.ConvertSpreadsheet(targetPath, "tsv");
                        return new ActionResult { Success = true, Message = T("Таблицю збережено у TSV", "Spreadsheet saved to TSV"), OutputPath = outPath };
                    }
                    case "sheet_to_ods":
                    {
                        var outPath = ImageProcessor.ConvertSpreadsheet(targetPath, "ods");
                        return new ActionResult { Success = true, Message = T("Таблицю збережено у ODS", "Spreadsheet saved to ODS"), OutputPath = outPath };
                    }
                    case "sheet_to_pdf":
                    {
                        var outPath = ImageProcessor.ConvertSpreadsheet(targetPath, "pdf");
                        return new ActionResult { Success = true, Message = T("Таблицю збережено у PDF", "Spreadsheet saved to PDF"), OutputPath = outPath };
                    }

                    // ==========================================
                    // РЕЖИМ 1: КОНВЕРТАЦІЯ — АРХІВИ
                    // ==========================================
                    case "arch_extract":
                    case "extract_archive":
                    {
                        var outDir = ImageProcessor.ExtractArchive(targetPath);
                        return new ActionResult { Success = true, Message = T("Архів розпаковано", "Archive extracted"), OutputPath = outDir };
                    }
                    case "arch_to_zip":
                    case "compress_to_zip":
                    {
                        var outPath = ImageProcessor.CompressToZip(targetPaths);
                        return new ActionResult { Success = true, Message = T("Запаковано в ZIP", "Packed into ZIP"), OutputPath = outPath };
                    }
                    case "arch_to_tar":
                    {
                        var outPath = ImageProcessor.CompressToTar(targetPaths, gzip: false);
                        return new ActionResult { Success = true, Message = T("Запаковано в TAR", "Packed into TAR"), OutputPath = outPath };
                    }
                    case "arch_to_targz":
                    {
                        var outPath = ImageProcessor.CompressToTar(targetPaths, gzip: true);
                        return new ActionResult { Success = true, Message = T("Запаковано в TAR.GZ", "Packed into TAR.GZ"), OutputPath = outPath };
                    }

                    // ==========================================
                    // РЕЖИМ 2: ІНСТРУМЕНТИ (ЗА КАТЕГОРІЯМИ ФАЙЛІВ)
                    // ==========================================
                    // --- Зображення ---
                    case "tool_img_ocr":
                    {
                        var outPath = ImageProcessor.ExtractTextOcr(targetPath);
                        return new ActionResult { Success = true, Message = T("Текст розпізнано (OCR)", "Text extracted (OCR)"), OutputPath = outPath };
                    }
                    case "tool_img_compress":
                    case "image_compress":
                    {
                        var outPath = ImageProcessor.CompressImage(targetPath, 75);
                        return new ActionResult { Success = true, Message = T("Зображення стиснено", "Image compressed"), OutputPath = outPath };
                    }
                    case "tool_img_crop_square":
                    {
                        var outPath = ImageProcessor.CropToSquare(targetPath);
                        return new ActionResult { Success = true, Message = T("Обрізано у квадрат 1:1", "Cropped to 1:1 square"), OutputPath = outPath };
                    }
                    case "tool_img_flip_h":
                    {
                        var outPath = ImageProcessor.FlipImageHorizontal(targetPath);
                        return new ActionResult { Success = true, Message = T("Віддзеркалено по горизонталі", "Flipped horizontally"), OutputPath = outPath };
                    }
                    case "tool_img_border":
                    {
                        var outPath = ImageProcessor.AddWhiteBorder(targetPath);
                        return new ActionResult { Success = true, Message = T("Додано білу рамку", "White border added"), OutputPath = outPath };
                    }

                    // --- Відео ---
                    case "tool_vid_mute":
                    {
                        var outPath = ImageProcessor.MuteVideo(targetPath);
                        return new ActionResult { Success = true, Message = T("Звук видалено з відео", "Audio removed from video"), OutputPath = outPath };
                    }
                    case "tool_vid_frame":
                    {
                        var outPath = ImageProcessor.ExtractVideoFrame(targetPath);
                        return new ActionResult { Success = true, Message = T("Кадр збережено у PNG", "Frame saved as PNG"), OutputPath = outPath };
                    }
                    case "tool_vid_compress":
                    {
                        var outPath = ImageProcessor.CompressVideo(targetPath);
                        return new ActionResult { Success = true, Message = T("Відео стиснено", "Video compressed"), OutputPath = outPath };
                    }
                    case "tool_vid_clip15":
                    {
                        var outPath = ImageProcessor.TrimVideoClip(targetPath, 15);
                        return new ActionResult { Success = true, Message = T("Збережено кліп 15 сек", "Saved 15s clip"), OutputPath = outPath };
                    }

                    // --- PDF ---
                    case "tool_pdf_extract_text":
                    {
                        var outPath = ImageProcessor.ConvertPdf(targetPath, "txt");
                        return new ActionResult { Success = true, Message = T("Текст витягнуто з PDF", "Text extracted from PDF"), OutputPath = outPath };
                    }
                    case "tool_pdf_stats":
                    case "tool_doc_stats":
                    {
                        var outPath = ImageProcessor.GenerateDocumentStats(targetPath);
                        return new ActionResult { Success = true, Message = T("Статистику збережено", "Statistics saved"), OutputPath = outPath };
                    }
                    case "tool_pdf_optimize":
                    {
                        var outPath = ImageProcessor.OptimizePdf(targetPath);
                        return new ActionResult { Success = true, Message = T("PDF оптимізовано", "PDF optimized"), OutputPath = outPath };
                    }

                    // --- Документи & Презентації ---
                    case "tool_doc_clean_spaces":
                    {
                        var outPath = ImageProcessor.CleanExtraSpacesInDocument(targetPath);
                        return new ActionResult { Success = true, Message = T("Зайві пробіли очищено", "Extra spaces cleaned"), OutputPath = outPath };
                    }
                    case "tool_doc_copy_text":
                    case "tool_pres_copy_text":
                    {
                        var text = ImageProcessor.ExtractPlainText(targetPath);
                        return new ActionResult { Success = true, Message = T("Текст скопійовано в буфер", "Text copied to clipboard"), OutputPath = text };
                    }
                    case "tool_doc_sort_lines":
                    {
                        var outPath = ImageProcessor.SortLinesInDocument(targetPath);
                        return new ActionResult { Success = true, Message = T("Рядки відсортовано за А–Я", "Lines sorted A–Z"), OutputPath = outPath };
                    }
                    case "tool_pres_extract_text":
                    {
                        var outPath = ImageProcessor.ConvertDocument(targetPath, "txt");
                        return new ActionResult { Success = true, Message = T("Текст слайдів витягнуто", "Slide text extracted"), OutputPath = outPath };
                    }
                    case "tool_pres_summary":
                    {
                        var outPath = ImageProcessor.GeneratePresentationSummary(targetPath);
                        return new ActionResult { Success = true, Message = T("Конспект презентації створено", "Presentation summary created"), OutputPath = outPath };
                    }

                    // --- Таблиці ---
                    case "tool_sheet_dedup":
                    {
                        var outPath = ImageProcessor.DeduplicateSpreadsheet(targetPath);
                        return new ActionResult { Success = true, Message = T("Дублікати рядків видалено", "Duplicate rows removed"), OutputPath = outPath };
                    }
                    case "tool_sheet_sort":
                    {
                        var outPath = ImageProcessor.SortSpreadsheet(targetPath);
                        return new ActionResult { Success = true, Message = T("Таблицю відсортовано А–Я", "Spreadsheet sorted A–Z"), OutputPath = outPath };
                    }
                    case "tool_sheet_transpose":
                    {
                        var outPath = ImageProcessor.TransposeSpreadsheet(targetPath);
                        return new ActionResult { Success = true, Message = T("Таблицю транспоновано", "Spreadsheet transposed"), OutputPath = outPath };
                    }
                    case "tool_sheet_stats":
                    {
                        var outPath = ImageProcessor.GenerateSpreadsheetStats(targetPath);
                        return new ActionResult { Success = true, Message = T("Статистику таблиці збережено", "Spreadsheet stats saved"), OutputPath = outPath };
                    }

                    // --- Архіви та Контрольна сума ---
                    case "tool_arch_list":
                    {
                        var outPath = ImageProcessor.ListArchiveContents(targetPath);
                        return new ActionResult { Success = true, Message = T("Список файлів архіву збережено", "Archive file list saved"), OutputPath = outPath };
                    }
                    case "tool_arch_verify":
                    {
                        int count = ImageProcessor.VerifyArchiveIntegrity(targetPath);
                        return new ActionResult
                        {
                            Success = true,
                            Message = T($"Архів цілісний ({count} елементів)", $"Archive verified ({count} entries)"),
                            OutputPath = targetPath
                        };
                    }
                    case "tool_file_hash":
                    {
                        var (reportPath, sha256) = ImageProcessor.ComputeFileHashReport(targetPath);
                        var shortHash = sha256.Length >= 12 ? sha256[..12] : sha256;
                        return new ActionResult
                        {
                            Success = true,
                            Message = $"SHA-256: {shortHash}…",
                            OutputPath = reportPath
                        };
                    }

                    // ==========================================
                    // РЕЖИМ 3: ДІЇ — БУДЬ-ЯКІ ФАЙЛИ
                    // ==========================================
                    case "act_copy_to_folder":
                    {
                        if (string.IsNullOrWhiteSpace(destinationFolder) || !Directory.Exists(destinationFolder))
                            return new ActionResult { Success = false, Message = T("Папку призначення не вибрано", "Destination folder not selected") };

                        string? lastCopied = null;
                        foreach (var p in targetPaths)
                        {
                            if (File.Exists(p))
                            {
                                var destFile = ImageProcessor.GetUniqueFilePath(Path.Combine(destinationFolder, Path.GetFileName(p)));
                                File.Copy(p, destFile, overwrite: false);
                                lastCopied = destFile;
                            }
                            else if (Directory.Exists(p))
                            {
                                var trimmed = p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                                var destSubDir = ImageProcessor.GetUniqueDirectoryPath(Path.Combine(destinationFolder, Path.GetFileName(trimmed)));
                                CopyDirectoryRecursive(trimmed, destSubDir);
                                lastCopied = destSubDir;
                            }
                        }
                        return new ActionResult { Success = true, Message = T("Скопійовано в папку", "Copied to folder"), OutputPath = lastCopied ?? destinationFolder };
                    }

                    case "act_move_to_folder":
                    {
                        if (string.IsNullOrWhiteSpace(destinationFolder) || !Directory.Exists(destinationFolder))
                            return new ActionResult { Success = false, Message = T("Папку призначення не вибрано", "Destination folder not selected") };

                        string? lastMoved = null;
                        var movements = new List<FileMovementRecord>();
                        foreach (var p in targetPaths)
                        {
                            if (File.Exists(p))
                            {
                                var destFile = ImageProcessor.GetUniqueFilePath(Path.Combine(destinationFolder, Path.GetFileName(p)));
                                File.Move(p, destFile);
                                lastMoved = destFile;
                                movements.Add(new FileMovementRecord { SourcePath = p, DestinationPath = destFile, IsDirectory = false });
                            }
                            else if (Directory.Exists(p))
                            {
                                var trimmed = p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                                var destSubDir = ImageProcessor.GetUniqueDirectoryPath(Path.Combine(destinationFolder, Path.GetFileName(trimmed)));
                                Directory.Move(trimmed, destSubDir);
                                lastMoved = destSubDir;
                                movements.Add(new FileMovementRecord { SourcePath = trimmed, DestinationPath = destSubDir, IsDirectory = true });
                            }
                        }

                        if (movements.Count > 0)
                        {
                            OperationHistoryService.Instance.RecordOperation(new OperationRecord
                            {
                                ActionId = "act_move_to_folder",
                                Title = T("Переміщення файлів", "Move files"),
                                Description = T($"Переміщено {movements.Count} елементів у {Path.GetFileName(destinationFolder)}",
                                                $"Moved {movements.Count} items to {Path.GetFileName(destinationFolder)}"),
                                CanUndo = false,
                                Movements = movements
                            });
                        }

                        return new ActionResult { Success = true, Message = T("Переміщено в папку", "Moved to folder"), OutputPath = lastMoved ?? destinationFolder };
                    }

                    case "act_copy_path":
                    case "system_copy_path":
                    {
                        var joined = string.Join(Environment.NewLine, targetPaths);
                        return new ActionResult { Success = true, Message = T("Шлях скопійовано", "Path copied"), OutputPath = joined };
                    }

                    case "act_open_terminal":
                    case "system_open_terminal":
                    {
                        var dir = Directory.Exists(targetPath)
                            ? targetPath
                            : Path.GetDirectoryName(targetPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            WorkingDirectory = dir,
                            UseShellExecute = true
                        });
                        return new ActionResult { Success = true, Message = T("Термінал відкрито", "Terminal opened") };
                    }

                    case "act_shelf_add":
                    case "system_shelf_add":
                    {
                        foreach (var p in targetPaths)
                        {
                            onAddToShelf?.Invoke(p);
                        }
                        onOpenShelf?.Invoke();
                        return new ActionResult { Success = true, Message = T("Покладено на полицю", "Added to Shelf"), OutputPath = targetPath };
                    }

                    case "act_windows_share":
                    case "system_reveal_explorer":
                    {
                        if (File.Exists(targetPath) || Directory.Exists(targetPath))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "explorer.exe",
                                Arguments = $"/select,\"{targetPath}\"",
                                UseShellExecute = true
                            });
                            return new ActionResult { Success = true, Message = T("Показано у Провіднику Windows", "Revealed in Windows Explorer") };
                        }
                        return new ActionResult { Success = false, Message = T("Файл не знайдено", "File not found") };
                    }

                    default:
                    {
                        if (action.Title != null && action.Title.Contains("PDF", StringComparison.OrdinalIgnoreCase))
                        {
                            var outPath = ImageProcessor.ConvertToPdf(targetPath);
                            return new ActionResult { Success = true, Message = T("Збережено у PDF", "Saved to PDF"), OutputPath = outPath };
                        }
                        return new ActionResult { Success = false, Message = T($"Невідома дія: {action.Title}", $"Unknown action: {action.Title}") };
                    }
                }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSub = Path.Combine(targetDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, destSub);
        }
    }

    private static ActionResult ExecuteCustomCommand(ActionItem action, string targetPath)
    {
        try
        {
            var cmd = action.CustomCommand!.Replace("%FILE%", $"\"{targetPath}\"")
                                          .Replace("%DIR%", $"\"{Path.GetDirectoryName(targetPath)}\"");

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {cmd}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(10000);

            return new ActionResult
            {
                Success = true,
                Message = T($"Виконано макрос: {action.Title}", $"Macro executed: {action.Title}")
            };
        }
        catch (Exception ex)
        {
            return new ActionResult { Success = false, Message = T($"Помилка макросу: {ex.Message}", $"Macro error: {ex.Message}") };
        }
    }
}
