namespace WALE.ProcessFile.Core.Models;

/// <summary>
/// Represents download information for files not present in inventory
/// </summary>
public class DownloadInfoOriginal
{
    public string PermitNumber { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string SitePath { get; set; } = string.Empty;
    public string LibraryAndFilePath { get; set; } = string.Empty;
    public string? FileId { get; set; } = string.Empty;
    public string? Reason { get; set; } = string.Empty;
}