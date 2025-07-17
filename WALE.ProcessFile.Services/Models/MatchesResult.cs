namespace WALE.ProcessFile.Services.Models;

public class MatchesResult
{
    public string? Filename { get; set; }
    public List<LabelGroupResult>? Matches { get; set; }
    public int NumberOfPages { get; set; }
    public bool ScannedFile { get; set; }
    public List<string> ServicesUsed { get; set; } = [];
    public List<PdfPage> Pages { get; set; } = [];
}