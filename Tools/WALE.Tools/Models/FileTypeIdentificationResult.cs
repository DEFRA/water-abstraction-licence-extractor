namespace WALE.Tools.Models;

/// <summary>
/// Data model for file type identification CSV export
/// </summary>
public class FileTypeIdentificationResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string IdentifiedByRule { get; set; } = string.Empty;
    public string MatchedTerms { get; set; } = string.Empty;
    
    public string? DateOfIssue { get; set; }
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
}