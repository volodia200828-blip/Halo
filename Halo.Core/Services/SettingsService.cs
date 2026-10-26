using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Halo.Core.Models;

namespace Halo.Core.Services;

public class SettingsService
{
    private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
    public static SettingsService Instance => _instance.Value;

    public static string CurrentLanguageCode => _instance.IsValueCreated ? _instance.Value.Current.Language : "uk";

    private readonly string _settingsFilePath;
    private AppSettings _currentSettings;

    public event Action<AppSettings>? SettingsChanged;

    public AppSettings Current => _currentSettings;

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var haloFolder = Path.Combine(appData, "Halo");
        if (!Directory.Exists(haloFolder))
        {
            Directory.CreateDirectory(haloFolder);
        }

        _settingsFilePath = Path.Combine(haloFolder, "settings.json");
        _currentSettings = LoadSettings();
    }

    public AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (settings != null)
                {
                    EnsureDefaultActions(settings);
                    SaveSettings(settings);
                    return settings;
                }
            }
        }
        catch (Exception)
        {
            // Fallback to defaults on corrupt file
        }

        var defaultSettings = new AppSettings
        {
            Actions = DefaultActionsProvider.GetDefaults()
        };

        SaveSettings(defaultSettings);
        return defaultSettings;
    }

    private void EnsureDefaultActions(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Language) || (settings.Language != "uk" && settings.Language != "en"))
        {
            settings.Language = "uk";
        }

        var defaults = DefaultActionsProvider.GetDefaults();
        var defaultIds = defaults.Select(d => d.Id).ToHashSet();

        // Keep only actions from the current specification
        settings.Actions.RemoveAll(a => !defaultIds.Contains(a.Id));

        var existingById = settings.Actions.ToDictionary(a => a.Id, a => a);

        foreach (var def in defaults)
        {
            if (existingById.TryGetValue(def.Id, out var existing))
            {
                existing.Title = def.Title;
                existing.Description = def.Description;
                existing.IconKey = def.IconKey;
                existing.AccentColor = def.AccentColor;
                existing.Category = def.Category;
                existing.SupportedFileTypes = def.SupportedFileTypes;
                existing.Order = def.Order;
            }
            else
            {
                settings.Actions.Add(def);
            }
        }

        DefaultActionsProvider.LocalizeActions(settings.Actions, settings.Language);
        settings.Actions = settings.Actions.OrderBy(a => a.Order).ToList();
    }

    public void SaveSettings(AppSettings? settings = null)
    {
        if (settings != null)
        {
            _currentSettings = settings;
        }

        if (string.IsNullOrWhiteSpace(_currentSettings.Language) || (_currentSettings.Language != "uk" && _currentSettings.Language != "en"))
        {
            _currentSettings.Language = "uk";
        }

        DefaultActionsProvider.LocalizeActions(_currentSettings.Actions, _currentSettings.Language);

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(_currentSettings, options);
            File.WriteAllText(_settingsFilePath, json);
            SettingsChanged?.Invoke(_currentSettings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}
