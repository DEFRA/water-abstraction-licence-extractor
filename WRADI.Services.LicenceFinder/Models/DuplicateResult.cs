namespace WRADI.Services.LicenceFinder.Models;

/// <summary>
/// Represents a duplicate file detection result
/// </summary>
public class DuplicateResult
{
    public string PermitNumber { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
}