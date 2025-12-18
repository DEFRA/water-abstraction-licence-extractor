namespace WALE.Tools.Models;

public class TemplateFinderInput
{
    public string PermitNumber { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string NaldIssueNumber { get; set; }
    public string SignatureDate { get; set; } = string.Empty;
    public string? DateOfIssue { get; set; } = string.Empty;
    public string? FileName { get; set; } = string.Empty;
    
    public string? Header { get; set; } = string.Empty;
    
    public int NumberOfPages { get; set; }
    
    public string TemplateType { get; set; }
    
    public string? Template { get; set; } = string.Empty;
}
