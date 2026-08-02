namespace WRADI.Core.AbstractionLicence.Models;

public class VersionFile
{
    public string? PermitNumber { get; set; }
    public string? FullPath { get; set; }
    public string? SitePath { get; set; }
    public string? LibraryAndFilePath { get; set; }
    public int? RegionId { get; set; }
    public string? FileName { get; set; }
    public Guid? FileId { get; set; }
    public int? FileSize { get; set; }
}