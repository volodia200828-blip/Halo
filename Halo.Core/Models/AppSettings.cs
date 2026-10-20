using System.Collections.Generic;

namespace Halo.Core.Models;

public class AppSettings
{
    public string Theme { get; set; } = "Dark"; // "Dark", "Light", "System"
    public double MenuRadius { get; set; } = 175;
    public double InnerRadius { get; set; } = 65;
    public int AnimationDurationMs { get; set; } = 280;
    public double Opacity { get; set; } = 0.94;
    public bool ShowLabelsOnSectors { get; set; } = true;
    public bool PlaySounds { get; set; } = false;
    public string GlobalHotkey { get; set; } = "Alt+Space";
    public bool AutoHideOnAction { get; set; } = true;
    
    public List<WatchedFolderConfig> WatchedFolders { get; set; } = new();
    public List<ActionItem> Actions { get; set; } = new();
    public List<string> ShelfFilePaths { get; set; } = new();
}
