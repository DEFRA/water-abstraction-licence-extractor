using WALE.ProcessFile.Models.Enums;

namespace WALE.ProcessFile.Models;

public class LabelGroupResult
{
    public IReadOnlyList<DocumentLine>? Text { get; set; }

    public MatchedPosition MatchedPosition { get; set; }

    public bool IsOcr { get; init; }

    public int LineNumber { get; set; }
    
    public int CharPosition { get; set; }
    
    public int PageNumber { get; init; }

    public string? ServiceName { get; init; }
    
    public string? LabelGroupName { get; set; }
    
    public LabelToMatch? MatchedLabel { get; set; }

    public double? Confidence
    {
        get
        {
            if (MatchedLabel == null)
            {
                return null;
            }

            switch (MatchedLabel.ConfidenceType)
            {
                case ConfidenceType.NotSet:
                    return null;
                case ConfidenceType.Static:
                    return MatchedLabel.ConfidenceIfMatched;
                case ConfidenceType.OcrConfidencePassthrough:
                    if (Text == null || Text.Count == 0 || MatchedLabel == null)
                    {
                        return null;
                    }

                    return GetAverageConfidence();
                case ConfidenceType.OcrConfidenceMultiplied:
                    if (Text == null || Text.Count == 0 || MatchedLabel == null)
                    {
                        return null;
                    }

                    var averageConfidence = GetAverageConfidence();
                    return (MatchedLabel.ConfidenceIfMatched / 100.0) * averageConfidence;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private double? GetAverageConfidence()
    {
        if (Text == null || Text.Count == 0 || MatchedLabel == null)
        {
            return null;
        }
                    
        var totalConfidence = Text.Sum(t => t.OcrConfidence
            ?? MatchedLabel.NoOcrConfidence);

        return totalConfidence / Text.Count;
    }   
    
    public IReadOnlyList<LabelGroupResult> SubResults { get; set; } = new List<LabelGroupResult>();
    
    public LabelGroupResult Clone()
    {
        // TODO swap to source generator

        return new LabelGroupResult
        {
            Text = Text?.ToList(),
            MatchedPosition = MatchedPosition,
            IsOcr = IsOcr,
            LineNumber = LineNumber,
            CharPosition = CharPosition,
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
        MatchedPosition matchedPosition,
        LabelPosition position,
        LabelToMatch label)
    {
        var labelGroupResult = Clone();
        labelGroupResult.MatchedPosition = matchedPosition;
        labelGroupResult.MatchedLabel = label.Clone();
        labelGroupResult.MatchedLabel.Position = position;

        return labelGroupResult;
    }
    
    public LabelGroupResult Clone(
        MatchedPosition matchedPosition,
        LabelPosition position,
        LabelToMatch label,
        IEnumerable<DocumentLine> text)
    {
        var labelGroupResult = Clone();
        labelGroupResult.MatchedPosition = matchedPosition;
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