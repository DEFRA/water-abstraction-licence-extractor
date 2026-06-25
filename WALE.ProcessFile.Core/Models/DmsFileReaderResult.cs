namespace WALE.ProcessFile.Core.Models;

public class DmsFileReaderResult
{
    public string? Status { get; set; }
    public string? LicenceNumber { get; set; }
    public string? PermitNumber { get; set; }
    public string? FileName { get; set; }
    public string? OriginalFileName { get; set; }
    public Guid FileId { get; set; }
    public DateTime? DateOfIssue { get; set; }
    public int? NumberOfPages { get; set; }
    public string? PrimaryType { get; set; }
    public string? SecondaryType { get; set; }
    public string? FileType { get; set; }
    public double? Confidence { get; set; }
    public string? IdentifiedByRule { get; set; }
    public string? MatchedTerms { get; set; }
    public long? FileSize { get; set; }
    public string? ErrorMessage { get; set; }
}