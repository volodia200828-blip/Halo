using System.Collections.Generic;

namespace Halo.Core.Models;

public class AppSettings
{
    public string Theme { get; set; } = "Dark"; // "Dark", "Indigo", "Light"
    public string Language { get; set; } = "uk"; // "uk", "en"
    public double MenuRadius { get; set; } = 175;
    public double InnerRadius { get; set; } = 66;
    public int AnimationDurationMs { get; set; } = 260;
    public double Opacity { get; set; } = 0.96;
    public bool ShowLabelsOnSectors { get; set; } = true;
    public bool PlaySounds { get; set; } = false;

    public string GlobalHotkey { get; set; } = "Alt+Space";
    public string GameModeHotkey { get; set; } = "Alt+G";
    public bool IsGameModeEnabled { get; set; } = false;
    public bool EnableDragShakeTrigger { get; set; } = true;
    public bool EnableMiddleMouseTrigger { get; set; } = true;

    public bool AutoHideOnAction { get; set; } = true;
    public bool EnableAutostart { get; set; } = false;
    public bool EnableExplorerContextMenu { get; set; } = false;
    public bool SaveNextToOriginal { get; set; } = true;
    public bool SmoothAnimations { get; set; } = true;

    public List<WatchedFolderConfig> WatchedFolders { get; set; } = new();
    public List<ActionItem> Actions { get; set; } = new();
    public List<string> ShelfFilePaths { get; set; } = new();
}
