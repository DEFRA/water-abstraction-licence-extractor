namespace WALE.ProcessFile.Core.Models;

public class MatchesResult
{
    public string? Filename { get; set; }
    
    public Guid? FileId { get; set; }
    
    public int RegionCode { get; set; }

    public string? Status { get; set; }
    
    public List<LabelGroupResult>? Matches { get; set; }
    
    public int NumberOfPages { get; set; }
    
    public bool ScannedFile { get; set; }
    
    public List<string> ServicesUsed { get; set; } = [];
    
    public IReadOnlyList<PdfPage> Pages { get; set; } = [];
    
    public string? ErrorMessage { get; set; }

    public Dictionary<string, object?>? AdditionalInformation { get; set; } = [];
}