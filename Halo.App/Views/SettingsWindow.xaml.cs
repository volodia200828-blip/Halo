using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Halo.App.Services;
using Halo.Core.Models;
using Halo.Core.Services;

namespace Halo.App.Views;

public partial class SettingsWindow : Window
{
    private List<ActionItem> _workingActions = new();
    private bool _isInitializing = true;

    public SettingsWindow()
    {
        _isInitializing = true;
        InitializeComponent();
        LoadCurrentSettings();
        _isInitializing = false;
    }

    private void LoadCurrentSettings()
    {
        var settings = SettingsService.Instance.Current;
        _workingActions = settings.Actions.OrderBy(a => a.Order).ToList();
        RefreshActionsList();

        RadiusSlider.Value = settings.MenuRadius;
        RadiusValueText.Text = $"{settings.MenuRadius} px";
        ShowLabelsCheck.IsChecked = settings.ShowLabelsOnSectors;
        AutoHideCheck.IsChecked = settings.AutoHideOnAction;
        HotkeyTextBox.Text = settings.GlobalHotkey;

        ThemeCombo.SelectedIndex = settings.Theme switch
        {
            "Indigo" => 1,
            "Light" => 2,
            _ => 0
        };

        WatchedFoldersControl.ItemsSource = null;
        WatchedFoldersControl.ItemsSource = settings.WatchedFolders;
    }

    private void RefreshActionsList()
    {
        if (_isInitializing || _workingActions == null || FilterCategoryCombo == null || ActionsItemsControl == null) return;
        int filterIndex = FilterCategoryCombo.SelectedIndex;
        var filtered = _workingActions.AsEnumerable();

        if (filterIndex == 1) // Images
            filtered = filtered.Where(a => a.SupportedFileTypes.Contains(FileTypeCategory.Image));
        else if (filterIndex == 2) // Documents
            filtered = filtered.Where(a => a.SupportedFileTypes.Contains(FileTypeCategory.Document));
        else if (filterIndex == 3) // System
            filtered = filtered.Where(a => a.Category == ActionCategory.System);
        else if (filterIndex == 4) // Custom
            filtered = filtered.Where(a => a.IsCustom);

        ActionsItemsControl.ItemsSource = null;
        ActionsItemsControl.ItemsSource = filtered.ToList();
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (PanelActions == null) return;

        PanelActions.Visibility = Visibility.Collapsed;
        PanelMacros.Visibility = Visibility.Collapsed;
        PanelAppearance.Visibility = Visibility.Collapsed;
        PanelHotkeys.Visibility = Visibility.Collapsed;
        PanelFolders.Visibility = Visibility.Collapsed;

        if (sender == TabActions) PanelActions.Visibility = Visibility.Visible;
        else if (sender == TabMacros) PanelMacros.Visibility = Visibility.Visible;
        else if (sender == TabAppearance) PanelAppearance.Visibility = Visibility.Visible;
        else if (sender == TabHotkeys) PanelHotkeys.Visibility = Visibility.Visible;
        else if (sender == TabFolders) PanelFolders.Visibility = Visibility.Visible;
    }

    private void FilterCategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        RefreshActionsList();
    }

    private void ActionToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is ActionItem item)
        {
            item.IsEnabled = cb.IsChecked ?? true;
            SaveActionsState();
        }
    }

    private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ActionItem item)
        {
            int index = _workingActions.IndexOf(item);
            if (index > 0)
            {
                _workingActions.RemoveAt(index);
                _workingActions.Insert(index - 1, item);
                UpdateOrderIndices();
                RefreshActionsList();
                SaveActionsState();
            }
        }
    }

    private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ActionItem item)
        {
            int index = _workingActions.IndexOf(item);
            if (index >= 0 && index < _workingActions.Count - 1)
            {
                _workingActions.RemoveAt(index);
                _workingActions.Insert(index + 1, item);
                UpdateOrderIndices();
                RefreshActionsList();
                SaveActionsState();
            }
        }
    }

    private void BtnDeleteAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ActionItem item)
        {
            var res = MessageBox.Show($"Ви впевнені, що хочете приховати або видалити функцію «{item.Title}»?",
                "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                _workingActions.Remove(item);
                UpdateOrderIndices();
                RefreshActionsList();
                SaveActionsState();
            }
        }
    }

    private void UpdateOrderIndices()
    {
        for (int i = 0; i < _workingActions.Count; i++)
        {
            _workingActions[i].Order = i + 1;
        }
    }

    private void SaveActionsState()
    {
        var settings = SettingsService.Instance.Current;
        settings.Actions = _workingActions;
        SettingsService.Instance.SaveSettings(settings);
    }

    private void BtnAddCustomAction_Click(object sender, RoutedEventArgs e)
    {
        TabMacros.IsChecked = true;
    }

    private void BtnSaveMacro_Click(object sender, RoutedEventArgs e)
    {
        var title = MacroTitleBox.Text.Trim();
        var desc = MacroDescBox.Text.Trim();
        var icon = string.IsNullOrWhiteSpace(MacroIconBox.Text) ? "⚡" : MacroIconBox.Text.Trim();
        var cmd = MacroCommandBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            MessageBox.Show("Будь ласка, вкажіть назву макросу.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var targetCat = MacroTargetTypeCombo.SelectedIndex switch
        {
            1 => new List<FileTypeCategory> { FileTypeCategory.Image },
            2 => new List<FileTypeCategory> { FileTypeCategory.Document },
            3 => new List<FileTypeCategory> { FileTypeCategory.Video },
            _ => new List<FileTypeCategory> { FileTypeCategory.Generic, FileTypeCategory.Image, FileTypeCategory.Document, FileTypeCategory.Video, FileTypeCategory.Folder }
        };

        var newAction = new ActionItem
        {
            Id = "macro_" + Guid.NewGuid().ToString("N")[..8],
            Title = title,
            Description = string.IsNullOrWhiteSpace(desc) ? "Власний користувацький макрос" : desc,
            IconKey = icon,
            AccentColor = "#38BDF8",
            Category = ActionCategory.CustomMacro,
            SupportedFileTypes = targetCat,
            IsEnabled = true,
            IsCustom = true,
            CustomCommand = cmd,
            Order = _workingActions.Count + 1
        };

        _workingActions.Add(newAction);
        SaveActionsState();

        MessageBox.Show($"Макрос «{title}» успішно додано до Halo!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

        // Reset inputs
        MacroTitleBox.Text = string.Empty;
        MacroDescBox.Text = string.Empty;

        TabActions.IsChecked = true;
        RefreshActionsList();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || ThemeCombo == null) return;
        var settings = SettingsService.Instance.Current;
        string newTheme = ThemeCombo.SelectedIndex switch
        {
            1 => "Indigo",
            2 => "Light",
            _ => "Dark"
        };
        settings.Theme = newTheme;
        ThemeManager.Instance.ApplyTheme(newTheme);
        SettingsService.Instance.SaveSettings(settings);
    }

    private void RadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RadiusValueText == null) return;
        int val = (int)e.NewValue;
        RadiusValueText.Text = $"{val} px";

        var settings = SettingsService.Instance.Current;
        settings.MenuRadius = val;
        SettingsService.Instance.SaveSettings(settings);
    }

    private void ShowLabelsCheck_Click(object sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Instance.Current;
        settings.ShowLabelsOnSectors = ShowLabelsCheck.IsChecked ?? true;
        SettingsService.Instance.SaveSettings(settings);
    }

    private void AutoHideCheck_Click(object sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Instance.Current;
        settings.AutoHideOnAction = AutoHideCheck.IsChecked ?? true;
        SettingsService.Instance.SaveSettings(settings);
    }

    private void BtnApplyHotkey_Click(object sender, RoutedEventArgs e)
    {
        var hotkey = HotkeyTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(hotkey))
        {
            var settings = SettingsService.Instance.Current;
            settings.GlobalHotkey = hotkey;
            SettingsService.Instance.SaveSettings(settings);
            MessageBox.Show($"Комбінацію «{hotkey}» збережено. Вона активна в системі!", "Halo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnAddWatchedFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = NewFolderInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            MessageBox.Show("Будь ласка, вкажіть дійсну існуючу папку.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var actionTag = (NewFolderActionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "image_to_webp";

        var rule = new WatchedFolderConfig
        {
            FolderPath = folder,
            AssignedActionId = actionTag,
            IsEnabled = true
        };

        var settings = SettingsService.Instance.Current;
        settings.WatchedFolders.Add(rule);
        SettingsService.Instance.SaveSettings(settings);

        WatchedFoldersControl.ItemsSource = null;
        WatchedFoldersControl.ItemsSource = settings.WatchedFolders;

        FolderWatcherService.Instance.Start();
    }

    private void BtnDeleteFolderRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WatchedFolderConfig rule)
        {
            var settings = SettingsService.Instance.Current;
            settings.WatchedFolders.Remove(rule);
            SettingsService.Instance.SaveSettings(settings);

            WatchedFoldersControl.ItemsSource = null;
            WatchedFoldersControl.ItemsSource = settings.WatchedFolders;

            FolderWatcherService.Instance.Start();
        }
    }

    private void FolderRuleToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is WatchedFolderConfig rule)
        {
            rule.IsEnabled = cb.IsChecked ?? true;
            var settings = SettingsService.Instance.Current;
            SettingsService.Instance.SaveSettings(settings);
            FolderWatcherService.Instance.Start();
        }
    }
}
