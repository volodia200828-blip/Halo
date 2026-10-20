using System.Collections.Generic;

namespace Halo.Core.Models;

public class ActionItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconKey { get; set; } = "⚡";
    public string AccentColor { get; set; } = "#3B82F6"; // Modern blue
    public ActionCategory Category { get; set; } = ActionCategory.System;
    public List<FileTypeCategory> SupportedFileTypes { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public int Order { get; set; }
    public bool IsCustom { get; set; }
    public string? CustomCommand { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();

    public ActionItem Clone()
    {
        return new ActionItem
        {
            Id = Id,
            Title = Title,
            Description = Description,
            IconKey = IconKey,
            AccentColor = AccentColor,
            Category = Category,
            SupportedFileTypes = new List<FileTypeCategory>(SupportedFileTypes),
            IsEnabled = IsEnabled,
            Order = Order,
            IsCustom = IsCustom,
            CustomCommand = CustomCommand,
            Parameters = new Dictionary<string, string>(Parameters)
        };
    }
}
