using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Halo.Core.Services;

namespace Halo.Core.Models;

public class WatchedFolderConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FolderPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string AssignedActionId { get; set; } = "folder_auto_sort";
    public string TargetOutputFolder { get; set; } = string.Empty;
    public bool DeleteSourceAfterProcess { get; set; } = false;
    public bool IncludeSubdirectories { get; set; } = false;
    public string FileFilter { get; set; } = "*.*";
    public int ProcessedCount { get; set; } = 0;
    public string LastProcessedAt { get; set; } = string.Empty;

    [JsonIgnore]
    public string ActionDisplayName
    {
        get
        {
            bool en = string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
            return AssignedActionId switch
            {
                "compress_to_zip" => en
                    ? "Auto-pack new files into ZIP archive"
                    : "Авто-пакування нових файлів у ZIP-архів",
                "extract_archive" => en
                    ? "Auto-extract new archives (ZIP, TAR, TAR.GZ)"
                    : "Авто-розпакування нових архівів (ZIP, TAR, TAR.GZ)",
                "image_to_png" => en
                    ? "Auto-convert new images to PNG"
                    : "Авто-конвертація нових зображень у PNG",
                "image_to_jpg" => en
                    ? "Auto-convert new images to JPG"
                    : "Авто-конвертація нових зображень у JPG",
                "image_to_webp" => en
                    ? "Auto-convert new images to WEBP"
                    : "Авто-конвертація нових зображень у WEBP",
                "doc_to_pdf" => en
                    ? "Auto-convert documents & images to PDF"
                    : "Авто-конвертація документів і фото у PDF",
                "image_compress" => en
                    ? "Auto-optimize new photos without quality loss"
                    : "Авто-оптимізація нових фото без втрати чіткості",
                "clean_temp_files" => en
                    ? "Auto-clean temporary files (.tmp, .bak, .crdownload)"
                    : "Авто-очищення тимчасових файлів (.tmp, .bak, .crdownload)",
                "folder_auto_sort" => en
                    ? "Auto-sort files into category subfolders"
                    : "Авто-сортування файлів по підпапках за типом",
                _ => en ? $"Automation rule: {AssignedActionId}" : $"Автоматичне правило: {AssignedActionId}"
            };
        }
    }

    [JsonIgnore]
    public string OptionsSummary
    {
        get
        {
            bool en = string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
            var parts = new List<string>();

            string filterLabel = FileFilter switch
            {
                "images" => en ? "Filter: Images" : "Фільтр: Зображення",
                "docs" => en ? "Filter: Documents & PDF" : "Фільтр: Документи та PDF",
                "archives" => en ? "Filter: Archives" : "Фільтр: Архіви",
                "videos" => en ? "Filter: Videos" : "Фільтр: Відео",
                _ => en ? "Filter: All files" : "Фільтр: Усі файли"
            };
            parts.Add(filterLabel);

            if (IncludeSubdirectories)
                parts.Add(en ? "With subfolders" : "З підпапками");

            if (DeleteSourceAfterProcess)
                parts.Add(en ? "Delete original" : "Видаляти оригінал");

            if (!string.IsNullOrWhiteSpace(TargetOutputFolder))
                parts.Add(en ? $"Out: {System.IO.Path.GetFileName(TargetOutputFolder.TrimEnd('\\', '/'))}" : $"Вихід: {System.IO.Path.GetFileName(TargetOutputFolder.TrimEnd('\\', '/'))}");

            parts.Add(en ? $"Processed: {ProcessedCount}" : $"Оброблено: {ProcessedCount}");
            return string.Join("  •  ", parts);
        }
    }

    [JsonIgnore]
    public string RunNowButtonText => string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase)
        ? "▶ Run now"
        : "▶ Обробити зараз";

    [JsonIgnore]
    public string OpenFolderButtonText => string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase)
        ? "📂 Open"
        : "📂 Відкрити";

    [JsonIgnore]
    public string DeleteButtonText => string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase)
        ? "Delete"
        : "Видалити";
}
