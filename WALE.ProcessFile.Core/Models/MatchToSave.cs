namespace WALE.ProcessFile.Core.Models;

// Not the ValueTuple this replaced: System.Text.Json ignores fields unless IncludeFields is set,
// so the tuple serialised as {} and /Extractor/Match/SaveMultiple silently wrote empty rows.
public class MatchToSave
{
    public int MatchesResultId { get; set; }

    public string? LabelName { get; set; }

    public string? LabelGroupName { get; set; }

    public LabelGroupResult? Data { get; set; }
}
