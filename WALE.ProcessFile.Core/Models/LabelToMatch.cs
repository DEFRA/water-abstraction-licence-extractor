using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Enums;

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
    public MultipleMatchBehaviour MultipleMatchBehaviour { get; init; } =
        MultipleMatchBehaviour.FindSingleInstanceOfLabelWithASingleValue;

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
    
    public bool RemoveStartOfBlockSectionsWhenMultiple { get; set; } = true;

    public bool DeDuplicateResults { get; set; }
    
    public bool GoOutsideTextBlock { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LimitTo LimitTo { get; set; } = LimitTo.WholeLine;

    public int LimitToColumnIndex { get; set; }
    
    // Rejects the whole next-line candidate, un-narrowed, when its leftmost column starts with one
    // of these: that row belongs to another field. IgnoreBlockIfContains judges the already-picked
    // column instead, so it cannot tell a compound answer containing the text from a different
    // field's row.
    public IReadOnlyList<string>? LimitToExcludeNextLineIfFirstColumnStartsWith { get; init; }

    // Column-level, where LimitToExcludeNextLineIfFirstColumnStartsWith is row-level: a row can
    // carry both an intruding field and this one's value in different columns ("Inspecting
    // Officer: X | 11/03/2026 | Time: 11:00"), so rejecting the whole row loses the value.
    public IReadOnlyList<string>? LimitToExcludeColumnStartingWith { get; init; }

    // Bounds the same-line walk by the nearest other known field's X-position, so it cannot wander
    // into a sibling's column. Opt-in: it needs the caller's per-document position index.
    public bool BoundSameLineWalkByOtherLabelPositions { get; init; }

    // When a label group has multiple sibling labels and a match returns empty text, allow
    // the engine to try the next alternate instead of settings the group as matched.
    public bool RequireTextToBePresent { get; init; }

    // Requires a complete date rather than any non-blank text, so a bare day number - textually
    // indistinguishable from a 2-digit year - cannot block a later, more complete alternate.
    public bool RequireCompleteDateToClaimGroup { get; init; }

    public LayoutExtractor LayoutExtractor { get; init; } = LayoutExtractor.Default;

    public LayoutExtractorTableLookupType LayoutExtractorTableLookupType { get; init; }
        = LayoutExtractorTableLookupType.Default;

    public LayoutExtractorTableShape LayoutExtractorTableShape { get; init; } = LayoutExtractorTableShape.Default;

    // ApplicableToMost's Format=="Text" branch fetches nextLines upstream and never consults them,
    // so NextLines(n) alone has no effect there. This appends them.
    public bool AllowValueToWrapToNextLine { get; init; }

    // GetTextBetween stops at its end-tag on the label's own first line, which is wrong where the
    // value wraps further down. This keeps scanning, relying on TextEnd for the real boundary.
    // Off by default: a blanket version broke most other fields, where stopping first is correct.
    public bool AllowValueToWrapPastSameLineEndTag { get; init; }
    
    public bool UseColonEndLogic { get; set; }

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
            RemoveStartOfBlockSectionsWhenMultiple = RemoveStartOfBlockSectionsWhenMultiple,
            DeDuplicateResults = DeDuplicateResults,
            GoOutsideTextBlock = GoOutsideTextBlock,
            LimitTo = LimitTo,
            LimitToColumnIndex = LimitToColumnIndex,
            LimitToExcludeNextLineIfFirstColumnStartsWith = LimitToExcludeNextLineIfFirstColumnStartsWith?.ToList(),
            LimitToExcludeColumnStartingWith = LimitToExcludeColumnStartingWith?.ToList(),
            BoundSameLineWalkByOtherLabelPositions = BoundSameLineWalkByOtherLabelPositions,
            RequireTextToBePresent = RequireTextToBePresent,
            RequireCompleteDateToClaimGroup = RequireCompleteDateToClaimGroup,
            LayoutExtractor = LayoutExtractor,
            LayoutExtractorTableLookupType = LayoutExtractorTableLookupType,
            LayoutExtractorTableShape = LayoutExtractorTableShape,
            AllowValueToWrapToNextLine = AllowValueToWrapToNextLine,
            AllowValueToWrapPastSameLineEndTag = AllowValueToWrapPastSameLineEndTag,
            UseColonEndLogic = UseColonEndLogic
        };
    }
}