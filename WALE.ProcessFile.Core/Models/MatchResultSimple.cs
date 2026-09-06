namespace WALE.ProcessFile.Core.Models;

public class MatchResultSimple
{
    public string? Filename { get; set; }
    
    public Guid FileId { get; set; }

    public string? Status { get; set; }
}