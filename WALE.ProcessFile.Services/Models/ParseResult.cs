namespace WALE.ProcessFile.Services.Models;

public class ParseResult
{
    public string? Filename { get; set; }
    public IReadOnlyList<LabelGroupResult>? Matches { get; set; }
}