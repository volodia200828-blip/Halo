using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Halo.App.Native;

public static class WindowsIntegrationHelper
{
    private const string FileShellKeyPath = @"Software\Classes\*\shell\Halo";
    private const string FolderShellKeyPath = @"Software\Classes\Directory\shell\Halo";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "Halo";

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    public static bool SetExplorerContextMenu(bool enable, string menuTitle = "Відкрити у Halo")
    {
        try
        {
            string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(exePath)) return false;

            if (enable)
            {
                RegisterShellKey(FileShellKeyPath, menuTitle, exePath);
                RegisterShellKey(FolderShellKeyPath, menuTitle, exePath);
            }
            else
            {
                Registry.CurrentUser.DeleteSubKeyTree(FileShellKeyPath, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(FolderShellKeyPath, throwOnMissingSubKey: false);
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

    private static void RegisterShellKey(string baseKeyPath, string menuTitle, string exePath)
    {
        using var shellKey = Registry.CurrentUser.CreateSubKey(baseKeyPath);
        if (shellKey != null)
        {
            shellKey.SetValue("", menuTitle);
            shellKey.SetValue("Icon", $"\"{exePath}\",0");

            using var cmdKey = shellKey.CreateSubKey("command");
            cmdKey?.SetValue("", $"\"{exePath}\" \"%1\"");
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
            string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(exePath)) return false;

            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (runKey == null) return false;

            if (enable)
            {
                runKey.SetValue(AppRegistryName, $"\"{exePath}\"");
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
