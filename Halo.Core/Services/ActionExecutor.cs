using System;
using System.Diagnostics;
using System.IO;
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
    public static async Task<ActionResult> ExecuteAsync(ActionItem action, string targetPath, Action<string>? onOpenSettings = null, Action<string>? onAddToShelf = null)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (action.IsCustom && !string.IsNullOrWhiteSpace(action.CustomCommand))
                {
                    return ExecuteCustomCommand(action, targetPath);
                }

                switch (action.Id)
                {
                    case "image_to_webp":
                    {
                        var outPath = ImageProcessor.ConvertFormat(targetPath, SKEncodedImageFormat.Webp);
                        return new ActionResult { Success = true, Message = "Конвертовано у WebP", OutputPath = outPath };
                    }

                    case "image_to_png":
                    {
                        var outPath = ImageProcessor.ConvertFormat(targetPath, SKEncodedImageFormat.Png);
                        return new ActionResult { Success = true, Message = "Конвертовано у PNG", OutputPath = outPath };
                    }

                    case "image_to_jpg":
                    {
                        var outPath = ImageProcessor.ConvertFormat(targetPath, SKEncodedImageFormat.Jpeg);
                        return new ActionResult { Success = true, Message = "Конвертовано у JPG", OutputPath = outPath };
                    }

                    case "image_resize_50":
                    {
                        var outPath = ImageProcessor.ResizeImage(targetPath, 0.5);
                        return new ActionResult { Success = true, Message = "Зменшено на 50%", OutputPath = outPath };
                    }

                    case "image_grayscale":
                    {
                        var outPath = ImageProcessor.ApplyGrayscale(targetPath);
                        return new ActionResult { Success = true, Message = "Застосовано чорно-білий фільтр", OutputPath = outPath };
                    }

                    case "image_compress":
                    {
                        var outPath = ImageProcessor.CompressImage(targetPath, 75);
                        return new ActionResult { Success = true, Message = "Зображення стиснено", OutputPath = outPath };
                    }

                    case "system_copy_path":
                    {
                        return new ActionResult { Success = true, Message = "Шлях скопійовано", OutputPath = targetPath };
                    }

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
                            return new ActionResult { Success = true, Message = "Відкрито у Провіднику" };
                        }
                        return new ActionResult { Success = false, Message = "Файл не знайдено" };
                    }

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
                        return new ActionResult { Success = true, Message = "Термінал відкрито" };
                    }

                    case "system_shelf_add":
                    {
                        onAddToShelf?.Invoke(targetPath);
                        return new ActionResult { Success = true, Message = "Додано на поличку" };
                    }

                    case "system_open_settings":
                    {
                        onOpenSettings?.Invoke(targetPath);
                        return new ActionResult { Success = true, Message = "Відкрито налаштування" };
                    }

                    default:
                        return new ActionResult { Success = false, Message = $"Невідома дія: {action.Title}" };
                }
            }
            catch (Exception ex)
            {
                return new ActionResult { Success = false, Message = $"Помилка: {ex.Message}" };
            }
        });
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
                Message = $"Виконано макрос: {action.Title}"
            };
        }
        catch (Exception ex)
        {
            return new ActionResult { Success = false, Message = $"Помилка макросу: {ex.Message}" };
        }
    }
}
