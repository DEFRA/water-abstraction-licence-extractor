using System.Text.Json;
using WALE.ProcessFile.Services.Enums;
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

    public IReadOnlyList<LabelGroupResult> SubResults { get; set; } = new List<LabelGroupResult>();
    
    public LabelGroupResult Clone()
    {
        return JsonSerializer.Deserialize<LabelGroupResult>(
            JsonSerializer.Serialize(this))!;
    }

    public LabelGroupResult Clone(
        MatchType matchType,
        LabelPosition position,
        LabelToMatch label)
    {
        var labelGroupResult = Clone();
        labelGroupResult.MatchType = matchType;
        labelGroupResult.MatchedLabel = label.Clone();
        labelGroupResult.MatchedLabel.Position = position;

        return labelGroupResult;
    }
    
    public LabelGroupResult Clone(
        MatchType matchType,
        LabelPosition position,
        LabelToMatch label,
        IReadOnlyList<DocumentLine> text)
    {
        var labelGroupResult = Clone();
        labelGroupResult.MatchType = matchType;
        labelGroupResult.MatchedLabel = label.Clone();
        labelGroupResult.MatchedLabel.Position = position;
        labelGroupResult.Text = text;

        return labelGroupResult;
    }
    
    public LabelGroupResult Clone(IReadOnlyList<DocumentLine> text)
    {
        var labelGroupResult = Clone();
        labelGroupResult.Text = text;

        return labelGroupResult;
    }
}