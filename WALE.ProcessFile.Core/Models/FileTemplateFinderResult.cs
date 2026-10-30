namespace WALE.ProcessFile.Core.Models;

/// <summary>
/// Represents the result of license file matching process
/// </summary>
public class FileTemplateFinderResult
{
    public string PermitNumber { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? NaldIssueNumber { get; set; }
    public int? NaldIncrementNumber { get; set; }
    public string? SignatureDate { get; set; } = string.Empty;
    public string? DateOfIssue { get; set; } = string.Empty;
    public string? FileName { get; set; } = string.Empty;
    public int? NumberOfPages { get; set; } 
    public string? PrimaryTemplateType { get; set; } 
    public string? SecondaryTemplateType { get; set; } 
}