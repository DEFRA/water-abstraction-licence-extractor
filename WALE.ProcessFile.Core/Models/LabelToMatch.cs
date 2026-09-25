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
    
    // A next-line candidate is rejected outright
    // (not narrowed to a column at all) when its own first/leftmost column starts with one of
    // these texts - i.e. that whole row visibly belongs to a different, identifiable field
    // (its own leading label), not a genuine continuation of this one. Distinct from
    // IgnoreBlockIfContains, which rejects based on the already-narrowed picked column's own
    // content and so can't tell "this field's genuine compound answer happens to contain the
    // rejected text" apart from "this is really a different field's row" - checking the row's
    // own leading label first avoids that ambiguity
    public IReadOnlyList<string>? LimitToExcludeNextLineIfFirstColumnStartsWith { get; init; }

    // Bounds WalkSameLineColumns' same-line walk by the X-position of the nearest other known
    // field's own column, built once per document (see PdfDataExtractorService.
    // BuildLabelPositionIndex) - stops the walk wandering into a sibling field's column when
    // nothing else bounds it. Opt-in (defaults to false/no-op): depends on the caller supplying
    // that per-document position index.
    public bool LimitToBoundSameLineWalkByOtherLabelPositions { get; init; }

    // When a label group has multiple sibling labels and a match returns empty text, allow
    // the engine to try the next alternate instead of settings the group as matched.
    public bool RequireTextToBePresent { get; init; }

    // Like RequireTextToBePresent, but requires a genuinely complete date (day, month and
    // year) rather than any non-blank text, so an incomplete date fragment (e.g. a bare day
    // number, textually indistinguishable from a bare 2-digit year) doesn't permanently block
    // a later, more complete alternate.
    public bool RequireCompleteDateToClaimGroup { get; init; }

    public LayoutExtractor LayoutExtractor { get; init; } = LayoutExtractor.Default;

    public LayoutExtractorTableLookupType LayoutExtractorTableLookupType { get; init; }
        = LayoutExtractorTableLookupType.Default;

    public LayoutExtractorTableShape LayoutExtractorTableShape { get; init; } = LayoutExtractorTableShape.Default;

    // ApplicableToMost's Format=="Text" branch builds its result purely from the label's own
    // line - NextLines(n) alone has no effect there, since nextLines is fetched upstream but
    // never consulted. Opt in to append the already-fetched nextLines onto the result, for a
    // value that continues on the line below with no other field sharing that row.
    public bool AllowValueToWrapToNextLine { get; init; }

    // GetTextBetween stops at its end-tag on the label's own first line (e.g. a same-row
    // "Meter make: <value> Serial number: <value>" layout) - correct when nothing past the
    // end-tag belongs to the field, but wrong when the value genuinely wraps onto a further
    // line with no signal telling it apart from an unrelated field's row. Opt in to keep
    // scanning past that first-line match instead, relying on TextEnd to find the real boundary
    // further down. Left off by default: a blanket version of this broke most other fields, for
    // which stopping at the first line is the common, correct shape.
    public bool AllowValueToWrapPastSameLineEndTag { get; init; }

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
            LimitToBoundSameLineWalkByOtherLabelPositions = LimitToBoundSameLineWalkByOtherLabelPositions,
            RequireTextToBePresent = RequireTextToBePresent,
            RequireCompleteDateToClaimGroup = RequireCompleteDateToClaimGroup,
            LayoutExtractor = LayoutExtractor,
            LayoutExtractorTableLookupType = LayoutExtractorTableLookupType,
            LayoutExtractorTableShape = LayoutExtractorTableShape,
            AllowValueToWrapToNextLine = AllowValueToWrapToNextLine,
            AllowValueToWrapPastSameLineEndTag = AllowValueToWrapPastSameLineEndTag
        };
    }
}