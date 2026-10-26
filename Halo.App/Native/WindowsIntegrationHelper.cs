using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Halo.App.Native;

public static class WindowsIntegrationHelper
{
    private const string FileShellKeyPath = @"Software\Classes\*\shell\Halo";
    private const string FolderShellKeyPath = @"Software\Classes\Directory\shell\Halo";
    private const string BackgroundShellKeyPath = @"Software\Classes\Directory\Background\shell\Halo";
    private const string DriveShellKeyPath = @"Software\Classes\Drive\shell\Halo";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "Halo";

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private static string GetTargetExePath()
    {
        string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        if (string.IsNullOrEmpty(exePath) || exePath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase) || !exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            string candidate = Path.Combine(AppContext.BaseDirectory, "Halo.App.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return exePath;
    }

    public static bool SetExplorerContextMenu(bool enable, string menuTitle = "Відкрити в Halo")
    {
        try
        {
            string exePath = GetTargetExePath();
            if (string.IsNullOrEmpty(exePath)) return false;

            if (enable)
            {
                RegisterShellKey(FileShellKeyPath, menuTitle, exePath, "%1");
                RegisterShellKey(FolderShellKeyPath, menuTitle, exePath, "%1");
                RegisterShellKey(BackgroundShellKeyPath, menuTitle, exePath, "%V");
                RegisterShellKey(DriveShellKeyPath, menuTitle, exePath, "%1");

                // Clean up any old naming keys if they exist
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\*\shell\Halo.App", throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\shell\Halo.App", throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\Background\shell\Halo.App", throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Drive\shell\Halo.App", throwOnMissingSubKey: false);
            }
            else
            {
                Registry.CurrentUser.DeleteSubKeyTree(FileShellKeyPath, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(FolderShellKeyPath, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(BackgroundShellKeyPath, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(DriveShellKeyPath, throwOnMissingSubKey: false);

                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\*\shell\Halo.App", throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\shell\Halo.App", throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\Background\shell\Halo.App", throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Drive\shell\Halo.App", throwOnMissingSubKey: false);
            }

            // Notify Windows Explorer of association change immediately
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Explorer context menu update failed: {ex.Message}");
            return false;
        }
    }

    private static void RegisterShellKey(string baseKeyPath, string menuTitle, string exePath, string paramPlaceholder)
    {
        using var shellKey = Registry.CurrentUser.CreateSubKey(baseKeyPath);
        if (shellKey != null)
        {
            shellKey.SetValue("", menuTitle);

            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Halo");
            string activeIco = Path.Combine(appData, "halo_active.ico");
            string bundledIco = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");

            if (File.Exists(activeIco))
            {
                shellKey.SetValue("Icon", $"\"{activeIco}\"");
            }
            else if (File.Exists(bundledIco))
            {
                shellKey.SetValue("Icon", $"\"{bundledIco}\"");
            }
            else
            {
                shellKey.SetValue("Icon", $"\"{exePath}\",0");
            }

            using var cmdKey = shellKey.CreateSubKey("command");
            cmdKey?.SetValue("", $"\"{exePath}\" \"{paramPlaceholder}\"");
        }
    }

    public static bool IsExplorerContextMenuEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(FileShellKeyPath);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    public static bool SetAutostart(bool enable)
    {
        try
        {
            string exePath = GetTargetExePath();
            if (string.IsNullOrEmpty(exePath)) return false;

            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (runKey == null) return false;

            if (enable)
            {
                runKey.SetValue(AppRegistryName, $"\"{exePath}\" --autostart");
            }
            else
            {
                runKey.DeleteValue(AppRegistryName, throwOnMissingValue: false);
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Autostart update failed: {ex.Message}");
            return false;
        }
    }
}
