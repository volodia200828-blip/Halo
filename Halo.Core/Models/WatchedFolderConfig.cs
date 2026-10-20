using System.Text.Json.Serialization;

namespace Halo.Core.Models;

public class WatchedFolderConfig
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString("N");
    public string FolderPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string AssignedActionId { get; set; } = "image_to_webp";
    public string TargetOutputFolder { get; set; } = string.Empty;
    public bool DeleteSourceAfterProcess { get; set; } = false;

    [JsonIgnore]
    public string ActionDisplayName => AssignedActionId switch
    {
        "image_to_webp" => "Авто-конвертація нових зображень у WebP",
        "image_to_png" => "Авто-конвертація нових зображень у PNG",
        "image_compress" => "Авто-стиснення нових фото без втрати чіткості",
        "image_grayscale" => "Автоматичний чорно-білий фільтр для фото",
        "folder_auto_sort" => "Авто-сортування файлів по підпапках (Фото, Документи, Відео)",
        _ => $"Автоматичне правило: {AssignedActionId}"
    };
}
