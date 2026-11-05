using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

namespace WRADI.DocumentType.WrInspectionReport.Models.Configuration;

// Accumulates into plain fields rather than mutating a LabelToMatch: several of its properties are
// init-only, so the real object can only be assembled once, in Build().
public sealed class WrFluentRule
{
    private IReadOnlyList<TextToMatch>? _textStart;
    // Null, not an empty list, so a rule with no end bound serialises the same whichever entry
    // point built it.
    private List<TextToMatch>? _textEnd;
    private LabelPosition _position;
    private LimitTo _limitTo = LimitTo.SameColumn;
    private int _nextLinesToFetch;
    private string? _name;
    private readonly List<TextToMatch> _remove = [];
    private List<TextToMatch>? _possibilities;
    private bool _requireTextToClaimGroup;
    private bool _requireCompleteDateToClaimGroup;
    private int _previousLinesToFetch;
    private int _leewayBefore;
    private List<string>? _ignoreBlockIfContains;
    private List<string>? _excludeNextLineIfFirstColumnStartsWith;
    private List<string>? _excludeColumnStartingWith;
    private LayoutExtractor _layoutExtractor = LayoutExtractor.Default;
    private LayoutExtractorTableLookupType _layoutExtractorTableBasedExtractorType = LayoutExtractorTableLookupType.Default;
    private LayoutExtractorTableShape _layoutExtractorTableShape = LayoutExtractorTableShape.Default;
    private bool _allowValueToWrapToNextLine;
    private bool _boundSameLineWalkByOtherLabelPositions;
    private bool _allowValueToWrapPastSameLineEndTag;

    public static WrFluentRule Between(string startText, string endText)
    {
        var rule = new WrFluentRule
        {
            _textStart = [new(startText) { ColumnMustStartWith = true }],
            _textEnd = [new(endText) { LineMustStartWith = true }, new("[END_OF_BLOCK]")],
            _position = LabelPosition.TextToFindIsBetweenLabels
        };
        
        rule._remove.Add(new(startText));
        
        return rule;
    }

    // LabelToMatch.Text is an alias for TextStart, so this needs no separate field.
    public static WrFluentRule After(string text)
    {
        var rule = new WrFluentRule
        {
            _textStart = [new(text) { ColumnMustStartWith = true }],
            _position = LabelPosition.LabelIsBeforeTextToFind
        };
        
        rule._remove.Add(new(text));
        
        return rule;
    }

    // Routed via TextToFindIsBetweenLabels, not LabelIsBeforeTextToFind: After()'s generic-text
    // path discards a result with nothing left after the label, swallowing every blank tick field.
    public static WrFluentRule InOrder(string text, List<TextToMatch>? inOrderPossibilities, string? endText = null)
    {
        var rule = new WrFluentRule
        {
            _textStart = [new(text) { ColumnMustStartWith = true }, new(text.Replace(" ", string.Empty)) { ColumnMustStartWith = true }],
            _textEnd = endText != null ? [new(endText) { LineMustStartWith = true }, new("[END_OF_BLOCK]")] : [new("[END_OF_BLOCK]")],
            _position = LabelPosition.TextToFindIsBetweenLabels,
            _nextLinesToFetch = 1,
            _possibilities = inOrderPossibilities
        };
        
        rule._remove.Add(new(text));
        
        return rule;
    }

    public WrFluentRule Named(string name)
    {
        _name = name;
        return this;
    }
    
    public WrFluentRule FromLetterAndTableGrid()
    {
        _layoutExtractor = LayoutExtractor.LetterBasedAndTableBased;
        _layoutExtractorTableBasedExtractorType = LayoutExtractorTableLookupType.Grid;
        _layoutExtractorTableShape = LayoutExtractorTableShape.Unstructured;
        
        return this;
    }
    
    public WrFluentRule FromTableGrid()
    {
        _layoutExtractor = LayoutExtractor.TableBased;
        _layoutExtractorTableBasedExtractorType = LayoutExtractorTableLookupType.Grid;
        _layoutExtractorTableShape = LayoutExtractorTableShape.Unstructured;
        
        return this;
    }
    
    public WrFluentRule FromLetterAndTableFreeText()
    {
        _layoutExtractor = LayoutExtractor.LetterBasedAndTableBased;
        _layoutExtractorTableBasedExtractorType = LayoutExtractorTableLookupType.FreeText;
        _layoutExtractorTableShape = LayoutExtractorTableShape.Unstructured;
        
        return this;
    }
    
    public WrFluentRule FromTableFreeText()
    {
        _layoutExtractor = LayoutExtractor.TableBased;
        _layoutExtractorTableBasedExtractorType = LayoutExtractorTableLookupType.FreeText;
        _layoutExtractorTableShape = LayoutExtractorTableShape.Unstructured;
        
        return this;
    }
    
    /// The rule's own start text is the key. Paired with the letter-based read, so the label rules
    /// still apply with no table extractor or no table found.
    public WrFluentRule FromLetterAndKeyValueTable()
    {
        _layoutExtractor = LayoutExtractor.LetterBasedAndTableBased;
        _layoutExtractorTableBasedExtractorType = LayoutExtractorTableLookupType.KeyValue;
        _layoutExtractorTableShape = LayoutExtractorTableShape.Unstructured;

        return this;
    }

    public WrFluentRule FromText()
    {
        _layoutExtractor = LayoutExtractor.LetterBased;
        return this;
    }

    public WrFluentRule WholeLine()
    {
        _limitTo = LimitTo.WholeLine;
        return this;
    }

    public WrFluentRule NextLines(int n)
    {
        _nextLinesToFetch = n;
        return this;
    }

    public WrFluentRule RequireTextToClaimGroup()
    {
        _requireTextToClaimGroup = true;
        return this;
    }

    // See LabelToMatch.RequireCompleteDateToClaimGroup.
    public WrFluentRule RequireCompleteDateToClaimGroup()
    {
        _requireCompleteDateToClaimGroup = true;
        return this;
    }

    // For a Between()-shaped rule whose value starts on a physical line above its own label.
    public WrFluentRule PreviousLines(int n)
    {
        _previousLinesToFetch = n;
        _leewayBefore = n;
        return this;
    }

    public WrFluentRule BoundByOtherLabels()
    {
        _boundSameLineWalkByOtherLabelPositions = true;
        return this;
    }

    // See LabelToMatch.AllowValueToWrapPastSameLineEndTag.
    public WrFluentRule AllowValueToWrapPastSameLineEndTag()
    {
        _allowValueToWrapPastSameLineEndTag = true;
        return this;
    }

    public WrFluentRule Possibilities(IEnumerable<TextToMatch> p)
    {
        _possibilities = p.ToList();
        return this;
    }

    public WrFluentRule IgnoreIfContains(params string[] terms)
    {
        _ignoreBlockIfContains = terms.ToList();
        return this;
    }

    public WrFluentRule SkipNextLineWhenStartsWith(params string[] terms)
    {
        _excludeNextLineIfFirstColumnStartsWith = terms.ToList();
        return this;
    }

    public WrFluentRule SkipColumnWhenStartsWith(params string[] terms)
    {
        _excludeColumnStartingWith = terms.ToList();
        return this;
    }

    // For the After() shape only - a single same-line bound.
    public WrFluentRule EndsAt(string text)
    {
        _textEnd = [new(text) { LineMustStartWith = true }];
        return this;
    }

    // For the Between()/InOrder() shapes, appended before the trailing [END_OF_BLOCK] sentinel.
    // No positional flag, so it only bounds the same-row walk.
    public WrFluentRule AlsoEndsAt(params string[] texts)
    {
        var sentinel = _textEnd![^1];
        _textEnd = [.._textEnd.Take(_textEnd.Count - 1), ..texts.Select(t => new TextToMatch(t)), sentinel];
        
        return this;
    }

    // AlsoEndsAt with LineMustStartWith, for a marker that must anchor a whole line. Only
    // MeansOfAbstraction needs it, for its "Records" end marker.
    public WrFluentRule AlsoEndsAtLineStart(params string[] texts)
    {
        var sentinel = _textEnd![^1];
        _textEnd = [.._textEnd.Take(_textEnd.Count - 1), ..texts.Select(t => new TextToMatch(t) { LineMustStartWith = true }), sentinel];
        
        return this;
    }

    public WrFluentRule AlsoStartsWith(params string[] texts)
    {
        _textStart = [.._textStart!, ..texts.Select(t => new TextToMatch(t) { ColumnMustStartWith = true })];
        _remove.AddRange(texts.Select(t => new TextToMatch(t)));
        
        return this;
    }

    // Matched anywhere on the line, where AlsoStartsWith requires a column start - for a value
    // that does not land at a column boundary, e.g. "...Inspection Date: 26/1/2026 Time: 3pm" all
    // in one column. Keep the text specific (a trailing colon) so it misses ordinary prose.
    // Additive only: it can add matches, never remove one.
    public WrFluentRule AlsoStartsWithLoose(params string[] texts)
    {
        _textStart = [.._textStart!, ..texts.Select(t => new TextToMatch(t))];
        _remove.AddRange(texts.Select(t => new TextToMatch(t)));
        
        return this;
    }

    // Pair with NextLines(n): this fetches nothing itself, it only appends what NextLines got.
    public WrFluentRule AllowValueToWrapToNextLine()
    {
        _allowValueToWrapToNextLine = true;
        return this;
    }

    public WrFluentRule Remove(IEnumerable<TextToMatch> items)
    {
        _remove.AddRange(items);
        return this;
    }

    // "Actions"/"Summary" are valid additionalTextStarts, so they stay in TextStart, but must not
    // be stripped from the value when they recur mid-block as a genuine sub-heading.
    public WrFluentRule ExceptFromRemove(params string[] texts)
    {
        _remove.RemoveAll(r => texts.Contains(r.Text));
        return this;
    }

    public LabelToMatch Build() => new()
    {
        TextStart = _textStart,
        TextEnd = _textEnd,
        Position = _position,
        LimitTo = _limitTo,
        Format = "Text",
        PreviousLinesToFetch = _previousLinesToFetch,
        LeewayBefore = _leewayBefore,
        NextLinesToFetch = _nextLinesToFetch,
        Name = _name,
        Remove = _remove,
        Possibilities = _possibilities,
        RequireTextToBePresent = _requireTextToClaimGroup,
        RequireCompleteDateToClaimGroup = _requireCompleteDateToClaimGroup,
        IgnoreBlockIfContains = _ignoreBlockIfContains,
        LimitToExcludeNextLineIfFirstColumnStartsWith = _excludeNextLineIfFirstColumnStartsWith,
        LimitToExcludeColumnStartingWith = _excludeColumnStartingWith,
        BoundSameLineWalkByOtherLabelPositions = _boundSameLineWalkByOtherLabelPositions,
        LayoutExtractor = _layoutExtractor,
        LayoutExtractorTableLookupType = _layoutExtractorTableBasedExtractorType,
        LayoutExtractorTableShape = _layoutExtractorTableShape,
        AllowValueToWrapToNextLine = _allowValueToWrapToNextLine,
        AllowValueToWrapPastSameLineEndTag = _allowValueToWrapPastSameLineEndTag,
        UseColonEndLogic = true
    };
}