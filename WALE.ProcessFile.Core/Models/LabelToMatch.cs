using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models;

public class LabelToMatch
{
    public IReadOnlyList<TextToMatch>? TextStart
    {
        get;
        set
        {
            field = value;
            TextToMatch = value?
                .Where(t => !t.SingleLinePerItem)
                .ToList();
        }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public IReadOnlyList<TextToMatch>? Text
    {
        get => TextStart;
        set => TextStart = value;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public IReadOnlyList<TextToMatch>? TextToMatch { get; private set; }

    public bool MatchAllText { get; init; }
    public IReadOnlyList<string>? IgnoreBlockIfContains { get; init; }
    public IReadOnlyList<string>? IgnoreMatchIfContains { get; init; }
    public IReadOnlyList<string>? SkipLineWhenContains { get; init; }    
    public IReadOnlyList<TextToMatch>? Remove { get; set; }
    public IReadOnlyList<TextToMatch>? TextEnd { get; set; }
    public IReadOnlyList<string>? MustContain { get; set; }
    public int? MinimumSubMatches { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MultipleServiceMatchBehaviour MultipleServiceMatchBehaviour { get; init; } =
        MultipleServiceMatchBehaviour.UseLastServiceResult;
    public bool CanGoOverPageBoundary { get; init; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LabelPosition Position { get; set; }
    public string? RelatedCategoryName { get; init; }
    public string? RelatedName { get; init; }
    public int LeewayBefore { get; init; } // TODO can likely get rid of this now ordering is sorted
    public IReadOnlyList<LabelToMatch>? SubLabels { get; set; }
    public string Format { get; set; } = "Text";
    public bool IncludeStartLabelText { get; init; }
    public bool IncludeEndLabelText { get; init; }
    public bool IncludeWholeLine { get; init; }
    public string? Name { get; init; }
    public string? CategoryName { get; init; }
    public IReadOnlyList<TextToMatch>? Possibilities { get; set; }
    public int PreviousLinesToFetch { get; init; } = 2;
    public int NextLinesToFetch { get; init; } = 4;
    public bool DoNotTrimLines { get; init; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MultipleMatchBehaviour MultipleMatchBehaviour { get; init; } = MultipleMatchBehaviour.FindSingleInstanceOfLabelWithASingleValue;
    public bool FindMultipleOnSingleLine { get; init; }
        
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public bool Completed { get; set; }
    public bool AutoCorrect { get; init; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ConfidenceType ConfidenceType { get; init; } = ConfidenceType.NotSet;

    public int NoOcrConfidence { get; init; } = 100;
    
    public double? ConfidenceIfMatched { get; init; }

    public double OcrConfidenceMinusNPerLine { get; init; } = 1;
    public IReadOnlyList<int> SkipLineNumbers { get; set; } = [];
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LimitTo LimitTo { get; set; } = LimitTo.WholeLine;

    public int LimitToColumnIndex { get; set; }

    public LabelToMatch Clone()
    {
        // TODO swap to a source generator

        return new LabelToMatch
        {
            TextStart = TextStart,
            MatchAllText = MatchAllText,
            IgnoreMatchIfContains = IgnoreMatchIfContains?.ToList(),
            IgnoreBlockIfContains = IgnoreBlockIfContains?.ToList(),
            SkipLineWhenContains = SkipLineWhenContains?.ToList(),
            Remove = Remove?.ToList(),
            TextEnd = TextEnd?.ToList(),
            MustContain = MustContain?.ToList(),
            MinimumSubMatches = MinimumSubMatches,
            MultipleServiceMatchBehaviour = MultipleServiceMatchBehaviour,
            Position = Position,
            RelatedCategoryName = RelatedCategoryName,
            RelatedName = RelatedName,
            LeewayBefore = LeewayBefore,
            SubLabels = SubLabels?.Select(s => s.Clone()).ToList(),
            Format = Format,
            IncludeStartLabelText = IncludeStartLabelText,
            IncludeEndLabelText = IncludeEndLabelText,            
            IncludeWholeLine = IncludeWholeLine,
            Name = Name,
            CategoryName = CategoryName,
            Possibilities = Possibilities?.ToList(),
            PreviousLinesToFetch = PreviousLinesToFetch,
            NextLinesToFetch = NextLinesToFetch,
            MultipleMatchBehaviour = MultipleMatchBehaviour,
            FindMultipleOnSingleLine = FindMultipleOnSingleLine,
            Completed = false,
            DoNotTrimLines = DoNotTrimLines,
            AutoCorrect = AutoCorrect,
            SkipLineNumbers = SkipLineNumbers,
            ConfidenceIfMatched = ConfidenceIfMatched,
            OcrConfidenceMinusNPerLine = OcrConfidenceMinusNPerLine,
            ConfidenceType = ConfidenceType,
            NoOcrConfidence = NoOcrConfidence,
            LimitTo = LimitTo,
            LimitToColumnIndex = LimitToColumnIndex
        };
    }    
}