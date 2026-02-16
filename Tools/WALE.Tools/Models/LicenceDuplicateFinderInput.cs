namespace WALE.Tools.Models;

public class LicenceDuplicateFinderInput
{
    public string? PermitNumber { get; set; }
    public string? FileName { get; set; }
    
    public string? FileUrl { get; set; }
    
    public string? FileSize { get; set; }
}