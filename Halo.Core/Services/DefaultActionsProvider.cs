using System;
using System.Collections.Generic;
using Halo.Core.Models;

namespace Halo.Core.Services;

public static class DefaultActionsProvider
{
    private static readonly List<FileTypeCategory> AllFileCategories = new()
    {
        FileTypeCategory.Generic,
        FileTypeCategory.Image,
        FileTypeCategory.Video,
        FileTypeCategory.Pdf,
        FileTypeCategory.Document,
        FileTypeCategory.Presentation,
        FileTypeCategory.Spreadsheet,
        FileTypeCategory.Archive,
        FileTypeCategory.Folder
    };

    public static List<ActionItem> GetDefaults(string lang = "uk")
    {
        int order = 1;
        var list = new List<ActionItem>
        {
            // =========================================================
            // РЕЖИМ 1: КОНВЕРТАЦІЯ (Shift) — ActionCategory.Convert
            // Текстові назви форматів зі збереженням кольорів попередніх піктограм
            // =========================================================

            // --- Зображення (Image) — тільки відповідні графічні формати ---
            new ActionItem
            {
                Id = "img_to_jpg",
                Title = "Формат JPG",
                Description = "Універсальне стиснення для фотографій",
                IconKey = "JPG",
                AccentColor = "#F59E0B",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "img_to_png",
                Title = "Формат PNG",
                Description = "Збереження чіткості та прозорості без втрат",
                IconKey = "PNG",
                AccentColor = "#6366F1",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "img_to_webp",
                Title = "Формат WEBP",
                Description = "Сучасний легкий формат для вебу",
                IconKey = "WEBP",
                AccentColor = "#38BDF8",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "img_to_avif",
                Title = "Формат AVIF",
                Description = "Максимальне стиснення нового покоління",
                IconKey = "AVIF",
                AccentColor = "#A855F7",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "img_to_tiff",
                Title = "Формат TIFF",
                Description = "Висока якість для друку та поліграфії",
                IconKey = "TIFF",
                AccentColor = "#14B8A6",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "img_to_ico",
                Title = "Формат ICO",
                Description = "Створення системної іконки Windows",
                IconKey = "ICO",
                AccentColor = "#3B82F6",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = order++,
                IsEnabled = true
            },

            // --- Відео (Video) ---
            new ActionItem
            {
                Id = "vid_to_mp4",
                Title = "Формат MP4",
                Description = "Універсальний відеоконтейнер H.264",
                IconKey = "MP4",
                AccentColor = "#38BDF8",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Video },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "vid_to_mov",
                Title = "Формат MOV",
                Description = "Контейнер Apple QuickTime для монтажу",
                IconKey = "MOV",
                AccentColor = "#6366F1",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Video },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "vid_to_mkv",
                Title = "Формат MKV",
                Description = "Мультимедійний контейнер Matroska",
                IconKey = "MKV",
                AccentColor = "#8B5CF6",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Video },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "vid_to_webm",
                Title = "Формат WEBM",
                Description = "Оптимізоване потокове відео для браузерів",
                IconKey = "WEBM",
                AccentColor = "#10B981",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Video },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "vid_to_gif",
                Title = "Формат GIF",
                Description = "Циклічна анімація без звуку",
                IconKey = "GIF",
                AccentColor = "#F59E0B",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Video },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "vid_to_mp3",
                Title = "Формат MP3",
                Description = "Вилучення чистої звукової доріжки",
                IconKey = "MP3",
                AccentColor = "#EC4899",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Video },
                Order = order++,
                IsEnabled = true
            },

            // --- PDF (Pdf) ---
            new ActionItem
            {
                Id = "pdf_to_docx",
                Title = "Формат DOCX",
                Description = "Редагований текстовий документ Microsoft Word",
                IconKey = "DOCX",
                AccentColor = "#3B82F6",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Pdf },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "pdf_to_jpg",
                Title = "Формат JPG",
                Description = "Експорт сторінок документа у растрові знімки",
                IconKey = "JPG",
                AccentColor = "#F59E0B",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Pdf },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "pdf_to_png",
                Title = "Формат PNG",
                Description = "Експорт сторінок без втрати якості",
                IconKey = "PNG",
                AccentColor = "#6366F1",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Pdf },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "pdf_to_pptx",
                Title = "Формат PPTX",
                Description = "Перетворення сторінок на слайди PowerPoint",
                IconKey = "PPTX",
                AccentColor = "#F43F5E",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Pdf },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "pdf_to_txt",
                Title = "Формат TXT",
                Description = "Витягнення чистого неформатованого тексту",
                IconKey = "TXT",
                AccentColor = "#10B981",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Pdf },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "pdf_to_html",
                Title = "Формат HTML",
                Description = "Веб-сторінка для перегляду у браузері",
                IconKey = "HTML",
                AccentColor = "#38BDF8",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Pdf },
                Order = order++,
                IsEnabled = true
            },

            // --- Документи (Document) ---
            new ActionItem
            {
                Id = "doc_to_docx",
                Title = "Формат DOCX",
                Description = "Стандартний документ Microsoft Word",
                IconKey = "DOCX",
                AccentColor = "#3B82F6",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Document },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "doc_to_pdf",
                Title = "Формат PDF",
                Description = "Фіксована верстка для друку та обміну",
                IconKey = "PDF",
                AccentColor = "#F43F5E",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Document },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "doc_to_odt",
                Title = "Формат ODT",
                Description = "Відкритий текстовий стандарт OpenDocument",
                IconKey = "ODT",
                AccentColor = "#14B8A6",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Document },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "doc_to_md",
                Title = "Формат MD",
                Description = "Легка текстова розмітка Markdown",
                IconKey = "MD",
                AccentColor = "#8B5CF6",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Document },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "doc_to_html",
                Title = "Формат HTML",
                Description = "Веб-документ із базовою розміткою",
                IconKey = "HTML",
                AccentColor = "#38BDF8",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Document },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "doc_to_txt",
                Title = "Формат TXT",
                Description = "Чистий текст без стилів та форматування",
                IconKey = "TXT",
                AccentColor = "#10B981",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Document },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "doc_to_epub",
                Title = "Формат EPUB",
                Description = "Електронна книга для рідерів та смартфонів",
                IconKey = "EPUB",
                AccentColor = "#F59E0B",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Document },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "doc_to_rtf",
                Title = "Формат RTF",
                Description = "Універсальний сумісний текстовий формат",
                IconKey = "RTF",
                AccentColor = "#EC4899",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Document },
                Order = order++,
                IsEnabled = true
            },

            // --- Презентації (Presentation) ---
            new ActionItem
            {
                Id = "pres_to_pdf",
                Title = "Формат PDF",
                Description = "Збереження слайдів у єдиний документ для показу",
                IconKey = "PDF",
                AccentColor = "#F43F5E",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Presentation },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "pres_to_png",
                Title = "Формат PNG",
                Description = "Експорт слайдів у чітку графіку без втрат",
                IconKey = "PNG",
                AccentColor = "#6366F1",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Presentation },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "pres_to_jpg",
                Title = "Формат JPG",
                Description = "Експорт слайдів у компактні знімки",
                IconKey = "JPG",
                AccentColor = "#F59E0B",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Presentation },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "pres_to_pptx",
                Title = "Формат PPTX",
                Description = "Стандартна презентація Microsoft PowerPoint",
                IconKey = "PPTX",
                AccentColor = "#EC4899",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Presentation },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "pres_to_odp",
                Title = "Формат ODP",
                Description = "Відкритий стандарт презентацій OpenDocument",
                IconKey = "ODP",
                AccentColor = "#14B8A6",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Presentation },
                Order = order++,
                IsEnabled = true
            },

            // --- Таблиці (Spreadsheet) ---
            new ActionItem
            {
                Id = "sheet_to_xlsx",
                Title = "Формат XLSX",
                Description = "Робоча книга таблиць Microsoft Excel",
                IconKey = "XLSX",
                AccentColor = "#10B981",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Spreadsheet },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "sheet_to_csv",
                Title = "Формат CSV",
                Description = "Текстові табличні дані з роздільником комою",
                IconKey = "CSV",
                AccentColor = "#38BDF8",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Spreadsheet },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "sheet_to_tsv",
                Title = "Формат TSV",
                Description = "Табличні дані з роздільником табуляцією",
                IconKey = "TSV",
                AccentColor = "#6366F1",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Spreadsheet },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "sheet_to_ods",
                Title = "Формат ODS",
                Description = "Електронна таблиця стандарту OpenDocument",
                IconKey = "ODS",
                AccentColor = "#8B5CF6",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Spreadsheet },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "sheet_to_pdf",
                Title = "Формат PDF",
                Description = "Звіт таблиці для друку та перегляду",
                IconKey = "PDF",
                AccentColor = "#F43F5E",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Spreadsheet },
                Order = order++,
                IsEnabled = true
            },

            // --- Архіви (Archive & Packing) ---
            new ActionItem
            {
                Id = "arch_extract",
                Title = "Розпакування",
                Description = "Видобути всі файли в окремий каталог поруч",
                IconKey = "UNZIP",
                AccentColor = "#F97316",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Archive },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "arch_to_zip",
                Title = "Архів ZIP",
                Description = "Швидке стиснення у стандартний архів",
                IconKey = "ZIP",
                AccentColor = "#38BDF8",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Archive, FileTypeCategory.Generic, FileTypeCategory.Folder },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "arch_to_tar",
                Title = "Архів TAR",
                Description = "Об'єднання файлів у пакет без втрат",
                IconKey = "TAR",
                AccentColor = "#8B5CF6",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Archive, FileTypeCategory.Generic, FileTypeCategory.Folder },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "arch_to_targz",
                Title = "Архів TAR.GZ",
                Description = "Високе стиснення за алгоритмом GZip",
                IconKey = "TAR.GZ",
                AccentColor = "#10B981",
                Category = ActionCategory.Convert,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Archive, FileTypeCategory.Generic, FileTypeCategory.Folder },
                Order = order++,
                IsEnabled = true
            },

            // =========================================================
            // РЕЖИМ 2: ІНСТРУМЕНТИ (Ctrl) — ActionCategory.Edit
            // Динамічні інструменти залежно від типу обраного файлу
            // =========================================================

            // --- Інструменти для Зображень (Image) ---
            new ActionItem
            {
                Id = "tool_img_ocr",
                Title = "Витягти текст (OCR)",
                Description = "Розпізнати текст на зображенні та скопіювати у буфер і TXT",
                IconKey = "🔍",
                AccentColor = "#EC4899",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_img_compress",
                Title = "Оптимізувати розмір",
                Description = "Стиснути фото для зменшення ваги файлу без втрати чіткості",
                IconKey = "⚡",
                AccentColor = "#10B981",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_img_crop_square",
                Title = "Квадрат 1:1",
                Description = "Обрізати зображення по центру у пропорції 1:1",
                IconKey = "✂️",
                AccentColor = "#38BDF8",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_img_flip_h",
                Title = "Віддзеркалити",
                Description = "Горизонтальне дзеркальне відображення зображення",
                IconKey = "↔️",
                AccentColor = "#6366F1",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_img_border",
                Title = "Додати рамку",
                Description = "Додати акуратну білу рамку навколо зображення",
                IconKey = "🖼️",
                AccentColor = "#F59E0B",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Image },
                Order = order++,
                IsEnabled = true
            },

            // --- Інструменти для Відео (Video) ---
            new ActionItem
            {
                Id = "tool_vid_mute",
                Title = "Видалити звук",
                Description = "Зберегти копію відеофайлу без звукової доріжки",
                IconKey = "🔇",
                AccentColor = "#F43F5E",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Video },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_vid_frame",
                Title = "Зберегти кадр",
                Description = "Зберегти стоп-кадр із відео у форматі PNG",
                IconKey = "📸",
                AccentColor = "#38BDF8",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Video },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_vid_compress",
                Title = "Стиснути відео",
                Description = "Оптимізувати бітрейт відео для зменшення розміру файлу",
                IconKey = "📉",
                AccentColor = "#10B981",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Video },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_vid_clip15",
                Title = "Обрізати відео",
                Description = "Відкрити відеоредактор для вибору моменту та обрізки",
                IconKey = "✂️",
                AccentColor = "#F59E0B",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Video },
                Order = order++,
                IsEnabled = true
            },

            // --- Інструменти для PDF (Pdf) ---
            new ActionItem
            {
                Id = "tool_pdf_extract_text",
                Title = "Витягти весь текст",
                Description = "Витягти текст із PDF у буфер обміну та текстовий файл",
                IconKey = "🔍",
                AccentColor = "#EC4899",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Pdf },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_pdf_stats",
                Title = "Статистика PDF",
                Description = "Підрахувати кількість слів, символів та обсяг документа",
                IconKey = "📊",
                AccentColor = "#38BDF8",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Pdf },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_pdf_optimize",
                Title = "Оптимізувати PDF",
                Description = "Перепакувати PDF-документ для швидкого відкриття",
                IconKey = "⚡",
                AccentColor = "#10B981",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Pdf },
                Order = order++,
                IsEnabled = true
            },

            // --- Інструменти для Документів (Document) ---
            new ActionItem
            {
                Id = "tool_doc_stats",
                Title = "Статистика тексту",
                Description = "Підрахувати кількість слів, символів, рядків та час читання",
                IconKey = "📊",
                AccentColor = "#38BDF8",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Document },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_doc_clean_spaces",
                Title = "Очистити пробіли",
                Description = "Видалити подвійні пробіли та зайві порожні рядки в тексті",
                IconKey = "🧹",
                AccentColor = "#10B981",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Document },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_doc_copy_text",
                Title = "Текст у буфер",
                Description = "Скопіювати весь текстовий вміст документа в буфер обміну",
                IconKey = "📋",
                AccentColor = "#8B5CF6",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Document },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_doc_sort_lines",
                Title = "Сортувати рядки",
                Description = "Відсортувати рядки текстового документа за алфавітом",
                IconKey = "🔤",
                AccentColor = "#F59E0B",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Document },
                Order = order++,
                IsEnabled = true
            },

            // --- Інструменти для Презентацій (Presentation) ---
            new ActionItem
            {
                Id = "tool_pres_extract_text",
                Title = "Конспект слайдів",
                Description = "Зібрати весь текст із презентації в один текстовий документ",
                IconKey = "📝",
                AccentColor = "#F59E0B",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Presentation },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_pres_summary",
                Title = "Аналіз презентації",
                Description = "Створити звіт зі структурою та обсягом тексту слайдів",
                IconKey = "📊",
                AccentColor = "#38BDF8",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Presentation },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_pres_copy_text",
                Title = "Текст у буфер",
                Description = "Скопіювати текст усіх слайдів презентації в буфер обміну",
                IconKey = "📋",
                AccentColor = "#8B5CF6",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Presentation },
                Order = order++,
                IsEnabled = true
            },

            // --- Інструменти для Таблиць (Spreadsheet) ---
            new ActionItem
            {
                Id = "tool_sheet_dedup",
                Title = "Видалити дублікати",
                Description = "Очистити таблицю від повністю повторюваних рядків",
                IconKey = "🧹",
                AccentColor = "#10B981",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Spreadsheet },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_sheet_sort",
                Title = "Сортувати рядки",
                Description = "Відсортувати дані таблиці за першим стовпцем (А–Я)",
                IconKey = "🔤",
                AccentColor = "#38BDF8",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Spreadsheet },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_sheet_transpose",
                Title = "Транспонувати",
                Description = "Поміняти місцями рядки та стовпці таблиці",
                IconKey = "🔄",
                AccentColor = "#8B5CF6",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Spreadsheet },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_sheet_stats",
                Title = "Аналіз таблиці",
                Description = "Підрахувати кількість рядків, стовпців і заповнених комірок",
                IconKey = "📈",
                AccentColor = "#F59E0B",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Spreadsheet },
                Order = order++,
                IsEnabled = true
            },

            // --- Інструменти для Архівів (Archive) ---
            new ActionItem
            {
                Id = "tool_arch_list",
                Title = "Список вмісту",
                Description = "Зберегти текстовий перелік усіх файлів всередині архіву",
                IconKey = "📋",
                AccentColor = "#FB923C",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Archive, FileTypeCategory.Generic, FileTypeCategory.Folder },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_arch_verify",
                Title = "Перевірити архів",
                Description = "Перевірити цілісність архіву та підрахувати стиснення",
                IconKey = "✅",
                AccentColor = "#F97316",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Archive },
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "tool_file_hash",
                Title = "SHA-256 Хеш",
                Description = "Обчислити контрольну суму SHA-256 і скопіювати в буфер",
                IconKey = "#️⃣",
                AccentColor = "#8B5CF6",
                Category = ActionCategory.Edit,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Archive, FileTypeCategory.Generic, FileTypeCategory.Folder },
                Order = order++,
                IsEnabled = true
            },

            // =========================================================
            // РЕЖИМ 3: ДІЇ (Alt) — ActionCategory.System
            // =========================================================
            new ActionItem
            {
                Id = "act_copy_to_folder",
                Title = "Копіювати в папку",
                Description = "Вибрати цільову папку та скопіювати туди файл",
                IconKey = "📂",
                AccentColor = "#38BDF8",
                Category = ActionCategory.System,
                SupportedFileTypes = new List<FileTypeCategory>(AllFileCategories),
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "act_move_to_folder",
                Title = "Перемістити в папку",
                Description = "Вибрати цільову папку та перемістити туди файл",
                IconKey = "📦",
                AccentColor = "#6366F1",
                Category = ActionCategory.System,
                SupportedFileTypes = new List<FileTypeCategory>(AllFileCategories),
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "act_copy_path",
                Title = "Копіювати шлях",
                Description = "Скопіювати повний системний шлях до файлу в буфер обміну",
                IconKey = "📋",
                AccentColor = "#8B5CF6",
                Category = ActionCategory.System,
                SupportedFileTypes = new List<FileTypeCategory>(AllFileCategories),
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "act_open_terminal",
                Title = "Відкрити термінал тут",
                Description = "Відкрити PowerShell у папці цього файлу",
                IconKey = "💻",
                AccentColor = "#14B8A6",
                Category = ActionCategory.System,
                SupportedFileTypes = new List<FileTypeCategory>(AllFileCategories),
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "act_shelf_add",
                Title = "Покласти на полицю",
                Description = "Додати файл на поличку Halo та відкрити її для роботи",
                IconKey = "📌",
                AccentColor = "#F59E0B",
                Category = ActionCategory.System,
                SupportedFileTypes = new List<FileTypeCategory>(AllFileCategories),
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "act_shelf_open",
                Title = "Відкрити полицю",
                Description = "Відкрити інтерфейс полиці зі збереженими файлами Halo",
                IconKey = "🗂️",
                AccentColor = "#10B981",
                Category = ActionCategory.System,
                SupportedFileTypes = new List<FileTypeCategory>(AllFileCategories),
                Order = order++,
                IsEnabled = true
            },
            new ActionItem
            {
                Id = "act_windows_share",
                Title = "Показати в Провіднику",
                Description = "Виділити файл у вікні Провідника Windows",
                IconKey = "🔗",
                AccentColor = "#EC4899",
                Category = ActionCategory.System,
                SupportedFileTypes = new List<FileTypeCategory>(AllFileCategories),
                Order = order++,
                IsEnabled = true
            }
        };

        LocalizeActions(list, lang);
        return list;
    }

    public static void LocalizeActions(IEnumerable<ActionItem> actions, string lang)
    {
        bool en = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);
        foreach (var item in actions)
        {
            if (item.IsCustom) continue;
            var (ukTitle, ukDesc, enTitle, enDesc) = GetTranslation(item.Id, item.Title, item.Description);
            item.Title = en ? enTitle : ukTitle;
            item.Description = en ? enDesc : ukDesc;
        }
    }

    private static (string UkTitle, string UkDesc, string EnTitle, string EnDesc) GetTranslation(string id, string fallbackTitle, string fallbackDesc)
    {
        return id switch
        {
            // Convert - Image
            "img_to_jpg" => ("Формат JPG", "Універсальне стиснення для фотографій", "JPG Format", "Universal photo compression"),
            "img_to_png" => ("Формат PNG", "Збереження чіткості та прозорості без втрат", "PNG Format", "Lossless quality with transparency support"),
            "img_to_webp" => ("Формат WEBP", "Сучасний легкий формат для вебу", "WEBP Format", "Modern lightweight web graphics"),
            "img_to_avif" => ("Формат AVIF", "Максимальне стиснення нового покоління", "AVIF Format", "Next-generation high-efficiency compression"),
            "img_to_tiff" => ("Формат TIFF", "Висока якість для друку та поліграфії", "TIFF Format", "Uncompressed print-ready quality"),
            "img_to_ico" => ("Формат ICO", "Створення системної іконки Windows", "ICO Format", "Windows application icon"),

            // Convert - Video
            "vid_to_mp4" => ("Формат MP4", "Універсальний відеоконтейнер H.264", "MP4 Format", "Universal H.264 video container"),
            "vid_to_mov" => ("Формат MOV", "Контейнер Apple QuickTime для монтажу", "MOV Format", "Apple QuickTime editing container"),
            "vid_to_mkv" => ("Формат MKV", "Мультимедійний контейнер Matroska", "MKV Format", "Matroska multimedia container"),
            "vid_to_webm" => ("Формат WEBM", "Оптимізоване потокове відео для браузерів", "WEBM Format", "Lightweight web streaming video"),
            "vid_to_gif" => ("Формат GIF", "Циклічна анімація без звуку", "GIF Format", "Looping silent animation"),
            "vid_to_mp3" => ("Формат MP3", "Вилучення чистої звукової доріжки", "MP3 Format", "Extract clean audio track"),

            // Convert - PDF
            "pdf_to_docx" => ("Формат DOCX", "Редагований текстовий документ Microsoft Word", "DOCX Format", "Editable Microsoft Word document"),
            "pdf_to_jpg" => ("Формат JPG", "Експорт сторінок документа у растрові знімки", "JPG Format", "Export document pages as raster images"),
            "pdf_to_png" => ("Формат PNG", "Експорт сторінок без втрати якості", "PNG Format", "Export pages as crisp lossless images"),
            "pdf_to_pptx" => ("Формат PPTX", "Перетворення сторінок на слайди PowerPoint", "PPTX Format", "Convert pages into PowerPoint slides"),
            "pdf_to_txt" => ("Формат TXT", "Витягнення чистого неформатованого тексту", "TXT Format", "Extract plain unformatted text"),
            "pdf_to_html" => ("Формат HTML", "Веб-сторінка для перегляду у браузері", "HTML Format", "Web page for browser viewing"),

            // Convert - Document
            "doc_to_docx" => ("Формат DOCX", "Стандартний документ Microsoft Word", "DOCX Format", "Standard Microsoft Word document"),
            "doc_to_pdf" => ("Формат PDF", "Фіксована верстка для друку та обміну", "PDF Format", "Fixed layout for printing and sharing"),
            "doc_to_odt" => ("Формат ODT", "Відкритий текстовий стандарт OpenDocument", "ODT Format", "OpenDocument text standard"),
            "doc_to_md" => ("Формат MD", "Легка текстова розмітка Markdown", "MD Format", "Lightweight Markdown text markup"),
            "doc_to_html" => ("Формат HTML", "Веб-документ із базовою розміткою", "HTML Format", "Web document with clean markup"),
            "doc_to_txt" => ("Формат TXT", "Чистий текст без стилів та форматування", "TXT Format", "Plain text without styling"),
            "doc_to_epub" => ("Формат EPUB", "Електронна книга для рідерів та смартфонів", "EPUB Format", "E-book for readers and mobile devices"),
            "doc_to_rtf" => ("Формат RTF", "Універсальний сумісний текстовий формат", "RTF Format", "Universally compatible rich text"),

            // Convert - Presentation
            "pres_to_pdf" => ("Формат PDF", "Збереження слайдів у єдиний документ для показу", "PDF Format", "Save slides as a single portable document"),
            "pres_to_png" => ("Формат PNG", "Експорт слайдів у чітку графіку без втрат", "PNG Format", "Export slides as lossless graphics"),
            "pres_to_jpg" => ("Формат JPG", "Експорт слайдів у компактні знімки", "JPG Format", "Export slides as compact images"),
            "pres_to_pptx" => ("Формат PPTX", "Стандартна презентація Microsoft PowerPoint", "PPTX Format", "Standard Microsoft PowerPoint deck"),
            "pres_to_odp" => ("Формат ODP", "Відкритий стандарт презентацій OpenDocument", "ODP Format", "OpenDocument presentation standard"),

            // Convert - Spreadsheet
            "sheet_to_xlsx" => ("Формат XLSX", "Робоча книга таблиць Microsoft Excel", "XLSX Format", "Microsoft Excel workbook"),
            "sheet_to_csv" => ("Формат CSV", "Текстові табличні дані з роздільником комою", "CSV Format", "Comma-separated tabular data"),
            "sheet_to_tsv" => ("Формат TSV", "Табличні дані з роздільником табуляцією", "TSV Format", "Tab-separated tabular data"),
            "sheet_to_ods" => ("Формат ODS", "Електронна таблиця стандарту OpenDocument", "ODS Format", "OpenDocument spreadsheet"),
            "sheet_to_pdf" => ("Формат PDF", "Звіт таблиці для друку та перегляду", "PDF Format", "Print-ready table report"),

            // Convert - Archive
            "arch_extract" => ("Розпакування", "Видобути всі файли в окремий каталог поруч", "Extract Files", "Unpack all files into a folder next to the archive"),
            "arch_to_zip" => ("Архів ZIP", "Швидке стиснення у стандартний архів", "ZIP Archive", "Fast compression into a standard archive"),
            "arch_to_tar" => ("Архів TAR", "Об'єднання файлів у пакет без втрат", "TAR Archive", "Bundle files into a standard package"),
            "arch_to_targz" => ("Архів TAR.GZ", "Високе стиснення за алгоритмом GZip", "TAR.GZ Archive", "High-ratio GZip archive compression"),

            // Tools - Image
            "tool_img_ocr" => ("Витягти текст (OCR)", "Розпізнати текст на зображенні та скопіювати у буфер і TXT", "Extract Text (OCR)", "Recognize text in image and copy to clipboard & TXT"),
            "tool_img_compress" => ("Оптимізувати розмір", "Стиснути фото для зменшення ваги файлу без втрати чіткості", "Optimize Size", "Compress image to reduce file size while keeping clarity"),
            "tool_img_crop_square" => ("Квадрат 1:1", "Обрізати зображення по центру у пропорції 1:1", "Square Crop 1:1", "Center-crop image to a 1:1 square aspect ratio"),
            "tool_img_flip_h" => ("Віддзеркалити", "Горизонтальне дзеркальне відображення зображення", "Flip Horizontal", "Mirror the image horizontally"),
            "tool_img_border" => ("Додати рамку", "Додати акуратну білу рамку навколо зображення", "Add Frame", "Add a clean white border around the image"),

            // Tools - Video
            "tool_vid_mute" => ("Видалити звук", "Зберегти копію відеофайлу без звукової доріжки", "Mute Audio", "Save a copy of the video without the audio track"),
            "tool_vid_frame" => ("Зберегти кадр", "Зберегти стоп-кадр із відео у форматі PNG", "Capture Frame", "Extract a high-quality PNG frame from the video"),
            "tool_vid_compress" => ("Стиснути відео", "Оптимізувати бітрейт відео для зменшення розміру файлу", "Compress Video", "Optimize video bitrate to reduce file size"),
            "tool_vid_clip15" => ("Обрізати відео", "Відкрити відеоредактор для вибору моменту та обрізки", "Trim Video", "Open video editor to choose exact moment and trim"),

            // Tools - PDF
            "tool_pdf_extract_text" => ("Витягти весь текст", "Витягти текст із PDF у буфер обміну та текстовий файл", "Extract All Text", "Extract PDF text to clipboard and a TXT file"),
            "tool_pdf_stats" => ("Статистика PDF", "Підрахувати кількість слів, символів та обсяг документа", "PDF Statistics", "Count words, characters, lines, and document metrics"),
            "tool_pdf_optimize" => ("Оптимізувати PDF", "Перепакувати PDF-документ для швидкого відкриття", "Optimize PDF", "Repack and optimize the PDF document"),

            // Tools - Document
            "tool_doc_stats" => ("Статистика тексту", "Підрахувати кількість слів, символів, рядків та час читання", "Text Statistics", "Count words, characters, lines, and reading time"),
            "tool_doc_clean_spaces" => ("Очистити пробіли", "Видалити подвійні пробіли та зайві порожні рядки в тексті", "Clean Whitespace", "Remove extra spaces and redundant blank lines"),
            "tool_doc_copy_text" => ("Текст у буфер", "Скопіювати весь текстовий вміст документа в буфер обміну", "Copy All Text", "Copy all document text directly to the clipboard"),
            "tool_doc_sort_lines" => ("Сортувати рядки", "Відсортувати рядки текстового документа за алфавітом", "Sort Lines A–Z", "Sort document lines alphabetically"),

            // Tools - Presentation
            "tool_pres_extract_text" => ("Конспект слайдів", "Зібрати весь текст із презентації в один текстовий документ", "Extract Slide Notes", "Collect all text from slides into a single document"),
            "tool_pres_summary" => ("Аналіз презентації", "Створити звіт зі структурою та обсягом тексту слайдів", "Slide Summary", "Generate a summary report of slide contents"),
            "tool_pres_copy_text" => ("Текст у буфер", "Скопіювати текст усіх слайдів презентації в буфер обміну", "Copy Slide Text", "Copy text from all slides to the clipboard"),

            // Tools - Spreadsheet
            "tool_sheet_dedup" => ("Видалити дублікати", "Очистити таблицю від повністю повторюваних рядків", "Remove Duplicates", "Remove duplicate rows from the spreadsheet"),
            "tool_sheet_sort" => ("Сортувати рядки", "Відсортувати дані таблиці за першим стовпцем (А–Я)", "Sort Rows A–Z", "Sort spreadsheet rows by the first column (A–Z)"),
            "tool_sheet_transpose" => ("Транспонувати", "Поміняти місцями рядки та стовпці таблиці", "Transpose Table", "Swap rows and columns in the spreadsheet"),
            "tool_sheet_stats" => ("Аналіз таблиці", "Підрахувати кількість рядків, стовпців і заповнених комірок", "Table Statistics", "Count rows, columns, and non-empty cells"),

            // Tools - Archive & Generic
            "tool_arch_list" => ("Список вмісту", "Зберегти текстовий перелік усіх файлів всередині архіву", "List Contents", "Save a text list of all files inside the archive"),
            "tool_arch_verify" => ("Перевірити архів", "Перевірити цілісність архіву та підрахувати стиснення", "Verify Archive", "Check archive integrity and entry count"),
            "tool_file_hash" => ("SHA-256 Хеш", "Обчислити контрольну суму SHA-256 і скопіювати в буфер", "SHA-256 Hash", "Compute SHA-256 checksum and copy to clipboard"),

            // System Actions
            "act_copy_to_folder" => ("Копіювати в папку", "Вибрати цільову папку та скопіювати туди файл", "Copy to Folder", "Choose a destination folder and copy the file there"),
            "act_move_to_folder" => ("Перемістити в папку", "Вибрати цільову папку та перемістити туди файл", "Move to Folder", "Choose a destination folder and move the file there"),
            "act_copy_path" => ("Копіювати шлях", "Скопіювати повний системний шлях до файлу в буфер обміну", "Copy Path", "Copy the full file path to the clipboard"),
            "act_open_terminal" => ("Відкрити термінал тут", "Відкрити PowerShell у папці цього файлу", "Open Terminal Here", "Open PowerShell in the file's directory"),
            "act_shelf_add" => ("Покласти на полицю", "Додати файл на поличку Halo та відкрити її для роботи", "Add to Shelf", "Pin the file to Halo Shelf and open it"),
            "act_shelf_open" => ("Відкрити полицю", "Відкрити інтерфейс полиці зі збереженими файлами Halo", "Open Shelf", "Open the Halo File Shelf window"),
            "act_windows_share" => ("Показати в Провіднику", "Виділити файл у вікні Провідника Windows", "Reveal in Explorer", "Highlight the file in Windows Explorer"),

            _ => (fallbackTitle, fallbackDesc, fallbackTitle, fallbackDesc)
        };
    }
}
