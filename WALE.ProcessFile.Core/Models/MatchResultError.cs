namespace WALE.ProcessFile.Core.Models;

public class MatchResultError
{
    public int MatchesResultId { get; set; }

    public Guid FileId { get; set; }

    public string? Filename { get; set; }

    public string? Status { get; set; }

    public string? Error { get; set; }

    public DateTime? DateTimeUtc { get; set; }
}
