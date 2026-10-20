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
        var defaults = DefaultActionsProvider.GetDefaults();
        var existingIds = settings.Actions.Select(a => a.Id).ToHashSet();

        foreach (var def in defaults)
        {
            if (!existingIds.Contains(def.Id))
            {
                settings.Actions.Add(def);
            }
        }
    }

    public void SaveSettings(AppSettings? settings = null)
    {
        if (settings != null)
        {
            _currentSettings = settings;
        }

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
