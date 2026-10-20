using System.Collections.Generic;
using Halo.Core.Models;

namespace Halo.Core.Services;

public static class DefaultActionsProvider
{
    public static List<ActionItem> GetDefaults()
    {
        return new List<ActionItem>
        {
            // === Image Actions ===
            new ActionItem
            {
                Id = "image_to_webp",
                Title = "У WebP",
                Description = "Миттєва конвертація у легкий сучасний формат WebP",
                IconKey = "🌐",
                AccentColor = "#06B6D4",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = 1,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "image_to_png",
                Title = "У PNG",
                Description = "Конвертувати у формат без втрат з підтримкою прозорості",
                IconKey = "🖼️",
                AccentColor = "#3B82F6",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = 2,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "image_to_jpg",
                Title = "У JPG",
                Description = "Конвертувати у стандартний фото-формат JPEG",
                IconKey = "📷",
                AccentColor = "#F59E0B",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = 3,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "image_resize_50",
                Title = "Зменшити 50%",
                Description = "Швидка оптимізація розмірів зображення вдвічі",
                IconKey = "📐",
                AccentColor = "#10B981",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = 4,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "image_grayscale",
                Title = "Чорно-біле",
                Description = "Застосувати вишуканий монохромний чорно-білий фільтр",
                IconKey = "🖤",
                AccentColor = "#8B5CF6",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = 5,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "image_compress",
                Title = "Стиснути",
                Description = "Оптимізація ваги зображення без візуальної втрати чіткості",
                IconKey = "🗜️",
                AccentColor = "#EC4899",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = 6,
                IsEnabled = true
            },

            // === Universal / Document Actions ===
            new ActionItem
            {
                Id = "doc_to_pdf",
                Title = "Зберегти як PDF",
                Description = "Швидко створити документ PDF із вибраного файлу",
                IconKey = "📄",
                AccentColor = "#EF4444",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image, FileTypeCategory.Document },
                Order = 7,
                IsEnabled = true
            },

            // === System Actions ===
            new ActionItem
            {
                Id = "system_shelf_add",
                Title = "На поличку",
                Description = "Закріпити файл на тимчасовій поличці для подальших дій",
                IconKey = "📌",
                AccentColor = "#F97316",
                Category = ActionCategory.System,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Generic, FileTypeCategory.Image, FileTypeCategory.Video, FileTypeCategory.Audio, FileTypeCategory.Document, FileTypeCategory.Folder },
                Order = 8,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "system_copy_path",
                Title = "Копіювати шлях",
                Description = "Скопіювати повний системний шлях до файлу в буфер обміну",
                IconKey = "📋",
                AccentColor = "#6366F1",
                Category = ActionCategory.System,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Generic, FileTypeCategory.Image, FileTypeCategory.Video, FileTypeCategory.Audio, FileTypeCategory.Document, FileTypeCategory.Folder },
                Order = 9,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "system_open_terminal",
                Title = "Термінал тут",
                Description = "Відкрити PowerShell у папці цього файлу",
                IconKey = "💻",
                AccentColor = "#14B8A6",
                Category = ActionCategory.System,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Generic, FileTypeCategory.Image, FileTypeCategory.Video, FileTypeCategory.Audio, FileTypeCategory.Document, FileTypeCategory.Folder },
                Order = 10,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "system_reveal_explorer",
                Title = "У Провіднику",
                Description = "Відкрити батьківську папку у Windows Explorer",
                IconKey = "📁",
                AccentColor = "#0284C7",
                Category = ActionCategory.System,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Generic, FileTypeCategory.Image, FileTypeCategory.Video, FileTypeCategory.Audio, FileTypeCategory.Document, FileTypeCategory.Folder },
                Order = 11,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "system_open_settings",
                Title = "Налаштування",
                Description = "Відкрити центр налаштувань Halo та кастомізації дій",
                IconKey = "⚙️",
                AccentColor = "#64748B",
                Category = ActionCategory.System,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Generic, FileTypeCategory.Image, FileTypeCategory.Video, FileTypeCategory.Audio, FileTypeCategory.Document, FileTypeCategory.Folder },
                Order = 12,
                IsEnabled = true
            }
        };
    }
}
