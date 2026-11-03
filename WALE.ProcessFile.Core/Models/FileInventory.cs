namespace WALE.ProcessFile.Core.Models;

/// <summary>
/// Represents a file inventory record from WaterPdfs_Inventory extract
/// </summary>
public class FileInventory
{
    public string FolderName { get; set; } = string.Empty;
    public string? PermitNumber { get; set; } = string.Empty;
    public string? FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;
    public string ModifiedTime { get; set; } = string.Empty;
}