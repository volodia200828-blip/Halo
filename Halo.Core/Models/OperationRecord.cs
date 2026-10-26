using System;
using System.Collections.Generic;

namespace Halo.Core.Models;

public class FileMovementRecord
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
}

public class OperationRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string ActionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool CanUndo { get; set; } = true;
    public bool IsUndone { get; set; } = false;
    public List<FileMovementRecord> Movements { get; set; } = [];
    public List<string> CreatedFiles { get; set; } = [];
}
