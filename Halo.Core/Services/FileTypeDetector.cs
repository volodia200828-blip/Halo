using System;
using System.IO;
using Halo.Core.Models;

namespace Halo.Core.Services;

public static class FileTypeDetector
{
    private static readonly string[] ImageExtensions =
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".ico", ".tiff", ".tif", ".avif"
    };

    private static readonly string[] VideoExtensions =
    {
        ".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".m4v", ".flv"
    };

    private static readonly string[] PdfExtensions =
    {
        ".pdf"
    };

    private static readonly string[] DocumentExtensions =
    {
        ".docx", ".doc", ".dock", ".odt", ".md", ".markdown", ".html", ".htm", ".txt", ".epub", ".rtf"
    };

    private static readonly string[] PresentationExtensions =
    {
        ".pptx", ".ppt", ".odp"
    };

    private static readonly string[] SpreadsheetExtensions =
    {
        ".xlsx", ".xls", ".csv", ".tsv", ".ods"
    };

    private static readonly string[] ArchiveExtensions =
    {
        ".zip", ".tar", ".gz", ".tgz", ".7z", ".rar"
    };

    public static FileTypeCategory Detect(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return FileTypeCategory.Generic;

        if (Directory.Exists(path))
            return FileTypeCategory.Folder;

        var lowerPath = path.ToLowerInvariant();
        if (lowerPath.EndsWith(".tar.gz"))
            return FileTypeCategory.Archive;

        var ext = Path.GetExtension(lowerPath);

        if (Array.IndexOf(ImageExtensions, ext) >= 0)
            return FileTypeCategory.Image;

        if (Array.IndexOf(VideoExtensions, ext) >= 0)
            return FileTypeCategory.Video;

        if (Array.IndexOf(PdfExtensions, ext) >= 0)
            return FileTypeCategory.Pdf;

        if (Array.IndexOf(DocumentExtensions, ext) >= 0)
            return FileTypeCategory.Document;

        if (Array.IndexOf(PresentationExtensions, ext) >= 0)
            return FileTypeCategory.Presentation;

        if (Array.IndexOf(SpreadsheetExtensions, ext) >= 0)
            return FileTypeCategory.Spreadsheet;

        if (Array.IndexOf(ArchiveExtensions, ext) >= 0)
            return FileTypeCategory.Archive;

        return FileTypeCategory.Generic;
    }
}
