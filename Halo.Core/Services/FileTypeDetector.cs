using System;
using System.IO;
using Halo.Core.Models;

namespace Halo.Core.Services;

public static class FileTypeDetector
{
    private static readonly string[] ImageExtensions =
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".ico", ".tiff", ".avif"
    };

    private static readonly string[] VideoExtensions =
    {
        ".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".m4v"
    };

    private static readonly string[] AudioExtensions =
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma"
    };

    private static readonly string[] DocumentExtensions =
    {
        ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".txt", ".md", ".json", ".xml", ".csv"
    };

    public static FileTypeCategory Detect(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return FileTypeCategory.Generic;

        if (Directory.Exists(path))
            return FileTypeCategory.Folder;

        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (Array.IndexOf(ImageExtensions, ext) >= 0)
            return FileTypeCategory.Image;

        if (Array.IndexOf(VideoExtensions, ext) >= 0)
            return FileTypeCategory.Video;

        if (Array.IndexOf(AudioExtensions, ext) >= 0)
            return FileTypeCategory.Audio;

        if (Array.IndexOf(DocumentExtensions, ext) >= 0)
            return FileTypeCategory.Document;

        return FileTypeCategory.Generic;
    }
}
