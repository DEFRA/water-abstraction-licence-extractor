using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models;

public class LabelGroupResult
{
    public IReadOnlyList<DocumentLine>? Text { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MatchedPosition MatchedPosition { get; set; }

    public bool IsOcr { get; init; }

    public int LineNumber { get; set; }
    
    public int CharPosition { get; set; }
    
    public int PageNumber { get; init; }

    public string? ServiceName { get; init; }
    
    public string? LabelGroupName { get; set; }

    [JsonIgnore]
    public LabelToMatch? MatchedLabel
    {
        get;
        set
        {
            if (value != null)
            {
                MatchedLabelName = value.Name;
                MatchedLabelRelatedName = value.RelatedName;
                MatchedLabelPosition = value.Position;
                MatchedLabelTextFirstLine = value.Text?.FirstOrDefault()?.Text;
            }
            
            field = value;
        }
    }

    public string? MatchedLabelName
    {
        get => MatchedLabel?.Name ?? field;
        set
        {
            if (value == null || MatchedLabel != null)
            {
                field = null;
                return;
            }
            
            field = value;
        }
    }
    
    public string? MatchedLabelRelatedName
    {
        get => MatchedLabel?.RelatedName ?? field;
        set
        {
            if (value == null || MatchedLabel != null)
            {
                field = null;
                return;
            }
            
            field = value;
        }
    }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LabelPosition? MatchedLabelPosition
    {
        get => MatchedLabel?.Position ?? field;
        set
        {
            if (value == null || MatchedLabel != null)
            {
                field = null;
                return;
            }
            
            field = value;
        }
    }
    
    public string? MatchedLabelTextFirstLine
    {
        get => MatchedLabel?.TextToMatch?.FirstOrDefault()?.Text ?? field;
        set
        {
            if (value == null || MatchedLabel != null)
            {
                field = null;
                return;
            }
            
            field = value;
        }
    }

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
                case ConfidenceType.Static:
                    return MatchedLabel.ConfidenceIfMatched;
                case ConfidenceType.OcrConfidencePassthrough:
                    return GetAverageConfidence();
                case ConfidenceType.OcrConfidencePassthroughMinusNPerLine:
                    return GetAverageConfidence() - ((Text?.Count ?? 0) * MatchedLabel.OcrConfidenceMinusNPerLine);
                case ConfidenceType.OcrConfidenceMultiplied:
                    return (MatchedLabel.ConfidenceIfMatched / 100.0) * (GetAverageConfidence() ?? 1);
                case ConfidenceType.OcrConfidenceMultipliedMinusNPerLine:
                    var returnFull = (MatchedLabel.ConfidenceIfMatched / 100.0) * (GetAverageConfidence() ?? 1);
                    return returnFull - ((Text?.Count ?? 0) * MatchedLabel.OcrConfidenceMinusNPerLine);
                case ConfidenceType.NotSet:
                    return null;
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

    public List<LabelGroupResult> AlternativeMatches { get; set; } = [];
    
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
            SubResults = SubResults.Select(x => x.Clone()).ToList(),
            AlternativeMatches = AlternativeMatches.Select(x => x.Clone()).ToList()
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