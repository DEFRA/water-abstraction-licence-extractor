namespace WALE.ProcessFile.Core.Models;

/// <summary>
/// Represents download information for files not present in inventory
/// </summary>
public class DownloadInfoAll
{
    public string PermitNumber { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string SitePath { get; set; } = string.Empty;
    public string LibraryAndFilePath { get; set; } = string.Empty;
    public int? RegionId { get; set; }
    public string? FileName { get; set; }
    public Guid? FileId { get; set; }
    public int? FileSize { get; set; }
}