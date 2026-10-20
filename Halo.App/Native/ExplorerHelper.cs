using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Halo.App.Native;

public static class ExplorerHelper
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    public static List<string> GetSelectedFiles()
    {
        var selectedFiles = new List<string>();

        try
        {
            var foregroundHwnd = GetForegroundWindow();
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return selectedFiles;

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null) return selectedFiles;

            dynamic windows = shell.Windows();
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
}
