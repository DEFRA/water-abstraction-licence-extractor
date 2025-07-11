using System.Text.Json;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Models;

public class LabelGroupResult
{
    public IReadOnlyList<DocumentLine>? Text { get; set; }

    public MatchType MatchType { get; set; }

    public bool IsOcr { get; init; }

    public int LineNumber { get; init; }
    
    public int PageNumber { get; init; }

    public string? ServiceName { get; init; }
    
    public string? LabelGroupName { get; set; }
    
    public LabelToMatch? MatchedLabel { get; set; }

    public IReadOnlyList<LabelGroupResult>? SubResults { get; set; }
    
    public LabelGroupResult Clone()
    {
        return JsonSerializer.Deserialize<LabelGroupResult>(
            JsonSerializer.Serialize(this))!;
    }
}