namespace Halo.Core.Models;

public class WatchedFolderConfig
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString("N");
    public string FolderPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string AssignedActionId { get; set; } = "image_to_webp";
    public string TargetOutputFolder { get; set; } = string.Empty;
    public bool DeleteSourceAfterProcess { get; set; } = false;
}
