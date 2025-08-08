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
        // TODO swap to source generator

        return new LabelGroupResult
        {
            Text = Text?.ToList(),
            MatchType = MatchType,
            IsOcr = IsOcr,
            LineNumber = LineNumber,
            PageNumber = PageNumber,
            ServiceName = ServiceName,
            LabelGroupName = LabelGroupName,
            MatchedLabel = MatchedLabel?.Clone(),
            SubResults = SubResults.Select(x => x.Clone()).ToList()
        };
    }

    public LabelGroupResult Clone(LabelToMatch label)
    {
        var labelGroupResult = Clone();
        labelGroupResult.MatchedLabel = label.Clone();

        return labelGroupResult;
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
        IEnumerable<DocumentLine> text)
    {
        var labelGroupResult = Clone();
        labelGroupResult.MatchType = matchType;
        labelGroupResult.MatchedLabel = label.Clone();
        labelGroupResult.MatchedLabel.Position = position;
        labelGroupResult.Text = text.ToList();

        return labelGroupResult;
    }
    
    public LabelGroupResult Clone(IEnumerable<DocumentLine> text)
    {
        var labelGroupResult = Clone();
        labelGroupResult.Text = text.ToList();

        return labelGroupResult;
    }
}