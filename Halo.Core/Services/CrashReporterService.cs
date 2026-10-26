using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Halo.Core.Services;

public class CrashReporterService
{
    private static readonly Lazy<CrashReporterService> _instance = new(() => new CrashReporterService());
    public static CrashReporterService Instance => _instance.Value;

    public string CrashDirectory { get; }

    public CrashReporterService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        CrashDirectory = Path.Combine(appData, "Halo", "CrashDumps");
        try
        {
            if (!Directory.Exists(CrashDirectory))
            {
                Directory.CreateDirectory(CrashDirectory);
            }
        }
        catch { }
    }

    public string LogCrash(Exception ex, string context = "", bool isFatal = false)
    {
        // 1. Immediately ensure user settings and data are safely saved to disk
        SafeFlushUserData();

        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("HALO APPLICATION CRASH REPORT / ERROR DUMP");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Timestamp (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Timestamp (Local): {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Severity: {(isFatal ? "FATAL (Process Terminating)" : "RECOVERABLE (Exception Handled)")}");
        sb.AppendLine($"Context: {(!string.IsNullOrWhiteSpace(context) ? context : "Unhandled Application Exception")}");
        sb.AppendLine($"OS: {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})");
        sb.AppendLine($".NET Runtime: {Environment.Version}");
        sb.AppendLine($"Process Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"Working Set Memory: {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB");

        try
        {
            var settings = SettingsService.Instance.Current;
            sb.AppendLine($"Current Language: {SettingsService.CurrentLanguageCode}");
            sb.AppendLine($"Current Theme: {settings.Theme}");
            sb.AppendLine($"Game Mode: {settings.IsGameModeEnabled}");
        }
        catch { }

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("EXCEPTION DETAILS:");
        sb.AppendLine("--------------------------------------------------------------------------------");

        int level = 0;
        Exception? cur = ex;
        while (cur != null)
        {
            sb.AppendLine($"[Level {level}] Exception Type: {cur.GetType().FullName}");
            sb.AppendLine($"Message: {cur.Message}");
            sb.AppendLine($"HResult: 0x{cur.HResult:X8}");
            sb.AppendLine($"TargetSite: {cur.TargetSite}");
            sb.AppendLine("StackTrace:");
            sb.AppendLine(cur.StackTrace ?? "(none)");
            sb.AppendLine();
            cur = cur.InnerException;
            level++;
        }

        string reportText = sb.ToString();
        string logFileName = $"crash_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.log";
        string filePath = Path.Combine(CrashDirectory, logFileName);

        try
        {
            if (!Directory.Exists(CrashDirectory))
            {
                Directory.CreateDirectory(CrashDirectory);
            }
            File.WriteAllText(filePath, reportText, Encoding.UTF8);

            string latestFile = Path.Combine(CrashDirectory, "latest_crash.log");
            File.WriteAllText(latestFile, reportText, Encoding.UTF8);
        }
        catch { }

        // Local fallback in current directory
        try
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last_crash.log"), reportText, Encoding.UTF8);
        }
        catch { }

        return filePath;
    }

    public void SafeFlushUserData()
    {
        try
        {
            SettingsService.Instance.SaveSettings(SettingsService.Instance.Current);
        }
        catch { }

        try
        {
            OperationHistoryService.Instance.SaveHistory();
        }
        catch { }
    }

    public IReadOnlyList<string> GetRecentCrashLogs(int maxCount = 10)
    {
        try
        {
            if (!Directory.Exists(CrashDirectory)) return [];
            return Directory.GetFiles(CrashDirectory, "crash_*.log")
                .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                .Take(maxCount)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public string? GetLastCrashLogContent()
    {
        try
        {
            var latest = Path.Combine(CrashDirectory, "latest_crash.log");
            if (File.Exists(latest)) return File.ReadAllText(latest);

            var files = GetRecentCrashLogs(1);
            if (files.Count > 0 && File.Exists(files[0]))
            {
                return File.ReadAllText(files[0]);
            }
        }
        catch { }
        return null;
    }
}
