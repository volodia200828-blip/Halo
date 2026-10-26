using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Halo.Core.Services;

namespace Halo.App.Views;

public partial class OrbitMessageDialog : Window
{
    private static bool IsEn => string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
    private static string T(string uk, string en) => IsEn ? en : uk;

    private string? _actionFilePath;

    public OrbitMessageDialog(string message, string title = "Halo", string icon = "✦")
    {
        InitializeComponent();
        DialogTitleBlock.Text = title;
        DialogSubBlock.Text = T("Сповіщення системи", "System Notification");
        DialogMessageBlock.Text = message;
        BtnOk.Content = T("Зрозуміло", "OK");
    }

    public static bool ShowConfirmation(
        Window? owner,
        string message,
        string title = "Halo",
        string confirmText = "Так, скасувати",
        string cancelText = "Залишити")
    {
        try
        {
            var dialog = new OrbitMessageDialog(message, title, "↺");
            dialog.BtnCancel.Visibility = Visibility.Visible;
            dialog.BtnCancel.Content = cancelText;
            dialog.BtnOk.Content = confirmText;
            dialog.BtnOk.Style = (Style)dialog.FindResource("OrbitPrimaryButton");

            if (owner != null && owner.IsLoaded && owner.IsVisible)
            {
                dialog.Owner = owner;
            }
            else if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded && Application.Current.MainWindow.IsVisible)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            return dialog.ShowDialog() == true;
        }
        catch
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }
    }

    public static void Show(Window? owner, string message, string title = "Halo", string icon = "✦")
    {
        try
        {
            var dialog = new OrbitMessageDialog(message, title, icon);
            if (owner != null && owner.IsLoaded && owner.IsVisible)
            {
                dialog.Owner = owner;
            }
            else if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded && Application.Current.MainWindow.IsVisible)
            {
                dialog.Owner = Application.Current.MainWindow;
            }
            dialog.ShowDialog();
        }
        catch
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public static void ShowVideoResult(Window? owner, string outPath)
    {
        try
        {
            var dialog = new OrbitMessageDialog(
                T($"Відео успішно обрізано та збережено:\n{outPath}",
                  $"Video successfully trimmed and saved to:\n{outPath}"),
                T("Обрізка завершена", "Trimming Completed"),
                "✓");

            dialog._actionFilePath = outPath;
            dialog.BtnPlayClip.Visibility = Visibility.Visible;
            dialog.BtnRevealFolder.Visibility = Visibility.Visible;
            dialog.BtnPlayClip.Content = T("▶  Переглянути кліп", "▶  Play Clip");
            dialog.BtnRevealFolder.Content = T("📂  Показати в папці", "📂  Show in Folder");
            dialog.BtnOk.Content = T("Закрити", "Close");

            if (owner != null && owner.IsLoaded && owner.IsVisible)
            {
                dialog.Owner = owner;
            }
            else if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded && Application.Current.MainWindow.IsVisible)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            dialog.ShowDialog();
        }
        catch
        {
            if (File.Exists(outPath))
            {
                Process.Start(new ProcessStartInfo { FileName = outPath, UseShellExecute = true });
            }
        }
    }

    private void BtnPlayClip_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_actionFilePath) && File.Exists(_actionFilePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _actionFilePath,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    private void BtnRevealFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_actionFilePath) && File.Exists(_actionFilePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{_actionFilePath}\"",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Enter)
        {
            e.Handled = true;
            Close();
        }
    }
}
