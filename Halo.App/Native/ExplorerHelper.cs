using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Halo.App.Native;

public static class ExplorerHelper
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    public static List<string> GetSelectedFiles()
    {
        var selectedFiles = new List<string>();

        try
        {
            var foregroundHwnd = GetForegroundWindow();
            if (foregroundHwnd == IntPtr.Zero) return selectedFiles;

            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return selectedFiles;

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null) return selectedFiles;

            dynamic windows = shell.Windows();

            // Check if the foreground window is the Windows Desktop (Progman or WorkerW)
            var classNameSb = new StringBuilder(256);
            GetClassName(foregroundHwnd, classNameSb, classNameSb.Capacity);
            string className = classNameSb.ToString();

            if (className == "Progman" || className == "WorkerW")
            {
                try
                {
                    object missing = Type.Missing;
                    int desktopHwnd = 0;
                    // SWC_DESKTOP = 8, SWFO_NEEDDISPATCH = 1
                    dynamic? desktopWin = windows.FindWindowSW(ref missing, ref missing, 8, out desktopHwnd, 1);
                    if (desktopWin != null)
                    {
                        ExtractSelectedFromWindow(desktopWin, selectedFiles);
                    }
                }
                catch
                {
                    // Fallback to standard enumeration below
                }
                return selectedFiles;
            }

            int count = windows.Count;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    dynamic window = windows.Item(i);
                    if (window == null) continue;

                    var hwnd = (IntPtr)window.HWND;
                    // If the foreground window matches the explorer window
                    if (hwnd == foregroundHwnd)
                    {
                        ExtractSelectedFromWindow(window, selectedFiles);
                        break;
                    }
                }
                catch
                {
                    // Ignore COM exceptions on specific windows (like IE/Edge tabs)
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to retrieve explorer selection: {ex.Message}");
        }

        return selectedFiles;
    }

    private static void ExtractSelectedFromWindow(dynamic window, List<string> selectedFiles)
    {
        dynamic document = window.Document;
        if (document != null)
        {
            dynamic selectedItems = document.SelectedItems();
            int itemCount = selectedItems.Count;
            for (int j = 0; j < itemCount; j++)
            {
                dynamic item = selectedItems.Item(j);
                string path = item.Path;
                if (File.Exists(path) || Directory.Exists(path))
                {
                    selectedFiles.Add(path);
                }
            }
        }
    }
}
