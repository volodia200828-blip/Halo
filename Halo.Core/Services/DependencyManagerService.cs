using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Halo.Core.Services;

public static class DependencyManagerService
{
    private static bool IsEn => string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
    private static string T(string uk, string en) => IsEn ? en : uk;

    public static string ToolsRootDir
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Halo", "tools");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }
    }

    // 1. Вбудований функціонал + Pandoc
    public static bool IsBuiltInAndPandocReady => true;
    public static bool IsPandocInstalled => GetPandocPath() != null;

    public static string? GetPandocPath()
    {
        var localPandoc = Path.Combine(ToolsRootDir, "pandoc", "pandoc.exe");
        if (File.Exists(localPandoc)) return localPandoc;

        if (Directory.Exists(ToolsRootDir))
        {
            try
            {
                var foundInTools = Directory.EnumerateFiles(ToolsRootDir, "pandoc.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (foundInTools != null) return foundInTools;
            }
            catch
            {
                // Ignore
            }
        }

        var programFilesPandoc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Pandoc", "pandoc.exe");
        if (File.Exists(programFilesPandoc)) return programFilesPandoc;

        var localAppDataPandoc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pandoc", "pandoc.exe");
        if (File.Exists(localAppDataPandoc)) return localAppDataPandoc;

        var wingetPackages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(wingetPackages))
        {
            try
            {
                var pandocDirs = Directory.EnumerateDirectories(wingetPackages, "*Pandoc*", SearchOption.TopDirectoryOnly);
                foreach (var dir in pandocDirs)
                {
                    var exe = Directory.EnumerateFiles(dir, "pandoc.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (exe != null) return exe;
                }
            }
            catch
            {
                // Ignore
            }
        }

        return FindInPath("pandoc.exe");
    }

    // 2. FFmpeg (Відео та Аудіо)
    public static bool IsFfmpegInstalled => GetFfmpegPath() != null;

    public static string? GetFfmpegPath()
    {
        var localFfmpeg = Path.Combine(ToolsRootDir, "ffmpeg", "ffmpeg.exe");
        if (File.Exists(localFfmpeg)) return localFfmpeg;

        if (Directory.Exists(ToolsRootDir))
        {
            try
            {
                var foundInTools = Directory.EnumerateFiles(ToolsRootDir, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (foundInTools != null) return foundInTools;
            }
            catch
            {
                // Ignore
            }
        }

        if (File.Exists(@"C:\ffmpeg\bin\ffmpeg.exe")) return @"C:\ffmpeg\bin\ffmpeg.exe";

        var wingetPackages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(wingetPackages))
        {
            try
            {
                var ffmpegDirs = Directory.EnumerateDirectories(wingetPackages, "*FFmpeg*", SearchOption.TopDirectoryOnly);
                foreach (var dir in ffmpegDirs)
                {
                    var exe = Directory.EnumerateFiles(dir, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (exe != null) return exe;
                }
            }
            catch
            {
                // Ignore permission errors
            }
        }

        return FindInPath("ffmpeg.exe");
    }

    // 3. LibreOffice (Офісні формати DOC, PPTX, ODS та PDF)
    public static bool IsLibreOfficeInstalled => GetLibreOfficePath() != null;

    public static string? GetLibreOfficePath()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe"),
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
            Path.Combine(ToolsRootDir, "libreoffice", "program", "soffice.exe")
        };

        foreach (var path in candidates)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return path;
        }

        return FindInPath("soffice.exe");
    }

    public static async Task<(bool Success, string Message)> InstallPandocAsync(Action<string>? onStatus = null)
    {
        if (IsPandocInstalled)
            return (true, T("Pandoc вже встановлено!", "Pandoc is already installed!"));

        return await Task.Run(() =>
        {
            try
            {
                onStatus?.Invoke(T("Запуск встановлення Pandoc через PowerShell...", "Installing Pandoc via PowerShell..."));
                var targetDir = Path.Combine(ToolsRootDir, "pandoc");
                Directory.CreateDirectory(targetDir);

                var psScript = $@"
$ErrorActionPreference = 'Stop'
Write-Host '=== Halo Dependency Installer: Pandoc ===' -ForegroundColor Cyan
if (Get-Command winget -ErrorAction SilentlyContinue) {{
    Write-Host 'Installing JohnMacFarlane.Pandoc via winget...' -ForegroundColor Yellow
    winget install --id JohnMacFarlane.Pandoc -e --accept-source-agreements --accept-package-agreements --silent
    if ($LASTEXITCODE -eq 0) {{ exit 0 }}
}}
Write-Host 'Downloading portable Pandoc via PowerShell...' -ForegroundColor Yellow
$zipPath = Join-Path '{targetDir.Replace("'", "''")}' 'pandoc.zip'
$release = Invoke-RestMethod -Uri 'https://api.github.com/repos/jgm/pandoc/releases/latest' -Headers @{{ 'User-Agent' = 'HaloApp' }}
$asset = $release.assets | Where-Object {{ $_.name -like '*windows-x86_64.zip' }} | Select-Object -First 1
if ($null -ne $asset) {{
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath -UseBasicParsing
    Expand-Archive -Path $zipPath -DestinationPath '{targetDir.Replace("'", "''")}' -Force
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    exit 0
}}
exit 1
";
                bool ok = RunPowerShellInstallerScript("install_pandoc.ps1", psScript);
                if (ok || IsPandocInstalled)
                {
                    return (true, T("Pandoc успішно встановлено через PowerShell!", "Pandoc installed via PowerShell!"));
                }

                return (false, T("Не вдалося встановити Pandoc через PowerShell. Перевірте підключення до Інтернету.", "Failed to install Pandoc via PowerShell. Check your internet connection."));
            }
            catch (Exception ex)
            {
                return (false, T($"Помилка PowerShell під час встановлення Pandoc: {ex.Message}", $"PowerShell error installing Pandoc: {ex.Message}"));
            }
        });
    }

    public static async Task<(bool Success, string Message)> InstallFfmpegAsync(Action<string>? onStatus = null)
    {
        if (IsFfmpegInstalled)
            return (true, T("FFmpeg вже встановлено!", "FFmpeg is already installed!"));

        return await Task.Run(() =>
        {
            try
            {
                onStatus?.Invoke(T("Встановлення FFmpeg через PowerShell...", "Installing FFmpeg via PowerShell..."));
                var targetDir = Path.Combine(ToolsRootDir, "ffmpeg");
                Directory.CreateDirectory(targetDir);

                var psScript = $@"
$ErrorActionPreference = 'Continue'
Write-Host '=== Halo Dependency Installer: FFmpeg ===' -ForegroundColor Cyan
if (Get-Command winget -ErrorAction SilentlyContinue) {{
    Write-Host 'Installing Gyan.FFmpeg via winget in PowerShell...' -ForegroundColor Yellow
    winget install --id Gyan.FFmpeg -e --accept-source-agreements --accept-package-agreements
    if ($LASTEXITCODE -eq 0) {{
        Write-Host 'FFmpeg installed successfully via winget!' -ForegroundColor Green
        exit 0
    }}
}}
Write-Host 'Downloading portable FFmpeg build via PowerShell...' -ForegroundColor Yellow
$zipPath = Join-Path '{targetDir.Replace("'", "''")}' 'ffmpeg.zip'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' -OutFile $zipPath -UseBasicParsing
if (Test-Path $zipPath) {{
    Write-Host 'Extracting FFmpeg archive...' -ForegroundColor Yellow
    Expand-Archive -Path $zipPath -DestinationPath '{targetDir.Replace("'", "''")}' -Force
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    Write-Host 'FFmpeg installed successfully!' -ForegroundColor Green
    exit 0
}}
exit 1
";
                bool ok = RunPowerShellInstallerScript("install_ffmpeg.ps1", psScript);
                if (ok && IsFfmpegInstalled)
                {
                    return (true, T("FFmpeg успішно встановлено через PowerShell!", "FFmpeg installed via PowerShell!"));
                }

                if (IsFfmpegInstalled)
                {
                    return (true, T("FFmpeg успішно підключено!", "FFmpeg connected successfully!"));
                }

                return (false, T("Встановлення FFmpeg через PowerShell не завершилося або було скасовано.", "FFmpeg PowerShell installation did not complete or was cancelled."));
            }
            catch (Exception ex)
            {
                return (false, T($"Помилка встановлення FFmpeg через PowerShell: {ex.Message}", $"Error installing FFmpeg via PowerShell: {ex.Message}"));
            }
        });
    }

    public static async Task<(bool Success, string Message)> InstallLibreOfficeAsync(Action<string>? onStatus = null)
    {
        if (IsLibreOfficeInstalled)
            return (true, T("LibreOffice вже встановлено!", "LibreOffice is already installed!"));

        return await Task.Run(() =>
        {
            try
            {
                onStatus?.Invoke(T("Встановлення LibreOffice через PowerShell...", "Installing LibreOffice via PowerShell..."));
                var targetDir = Path.Combine(ToolsRootDir, "libreoffice");
                Directory.CreateDirectory(targetDir);

                var psScript = $@"
$ErrorActionPreference = 'Continue'
Write-Host '=== Halo Dependency Installer: LibreOffice ===' -ForegroundColor Cyan
if (Get-Command winget -ErrorAction SilentlyContinue) {{
    Write-Host 'Installing TheDocumentFoundation.LibreOffice via winget in PowerShell...' -ForegroundColor Yellow
    winget install --id TheDocumentFoundation.LibreOffice -e --accept-source-agreements --accept-package-agreements
    if ($LASTEXITCODE -eq 0) {{
        Write-Host 'LibreOffice installed successfully via winget!' -ForegroundColor Green
        exit 0
    }}
}}
Write-Host 'Downloading LibreOffice MSI installer via PowerShell...' -ForegroundColor Yellow
$msiPath = Join-Path '{targetDir.Replace("'", "''")}' 'LibreOffice_Win_x86-64.msi'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri 'https://download.documentfoundation.org/libreoffice/stable/24.8.4/win/x86_64/LibreOffice_24.8.4_Win_x86-64.msi' -OutFile $msiPath -UseBasicParsing
if (Test-Path $msiPath) {{
    Write-Host 'Launching MSI installer...' -ForegroundColor Yellow
    Start-Process 'msiexec.exe' -ArgumentList ""/i `""$msiPath`"" /passive /norestart"" -Wait
    Remove-Item $msiPath -Force -ErrorAction SilentlyContinue
    exit 0
}}
exit 1
";
                bool ok = RunPowerShellInstallerScript("install_libreoffice.ps1", psScript);
                if (ok && IsLibreOfficeInstalled)
                {
                    return (true, T("LibreOffice успішно встановлено через PowerShell!", "LibreOffice installed via PowerShell!"));
                }

                if (IsLibreOfficeInstalled)
                {
                    return (true, T("LibreOffice успішно підключено!", "LibreOffice connected successfully!"));
                }

                return (false, T("Встановлення LibreOffice через PowerShell не завершилося або було скасовано.", "LibreOffice PowerShell installation did not complete or was cancelled."));
            }
            catch (Exception ex)
            {
                return (false, T($"Помилка встановлення LibreOffice через PowerShell: {ex.Message}", $"Error installing LibreOffice via PowerShell: {ex.Message}"));
            }
        });
    }

    private static bool RunPowerShellInstallerScript(string scriptFileName, string scriptContent)
    {
        try
        {
            var scriptPath = Path.Combine(ToolsRootDir, scriptFileName);
            File.WriteAllText(scriptPath, scriptContent, new UTF8Encoding(true));

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(300_000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindInPath(string exeName)
    {
        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var full = Path.Combine(dir.Trim(), exeName);
                    if (File.Exists(full)) return full;
                }
                catch
                {
                    // Ignore invalid path entries
                }
            }
        }
        catch
        {
            // Ignore
        }
        return null;
    }
}
