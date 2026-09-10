using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

namespace WRADI.DocumentType.WrInspectionReport.Models.Configuration;

// Fluent construction of a single LabelToMatch. One shared vocabulary (an entry point per
// rule shape, then chained modifiers) instead of three factory functions each with their own
// inconsistent parameter surface.
//
// Accumulates into plain fields rather than mutating a LabelToMatch in place - several of
// its properties (Name, NextLinesToFetch, RequireTextToClaimGroup, IgnoreBlockIfContains,
// ExcludeNextLineIfFirstColumnStartsWith, BoundSameLineWalkByOtherLabelPositions) are
// init-only, so the real object can only be assembled once, in Build().
public sealed class WrRule
{
    private IReadOnlyList<TextToMatch>? _textStart;
    // Null (not an empty list) when unset - a rule with no end bound serialises the same way
    // regardless of which entry point built it, rather than differing null-vs-empty-list.
    private List<TextToMatch>? _textEnd;
    private LabelPosition _position;
    private LimitTo _limitTo = LimitTo.SameColumn;
    private int _nextLinesToFetch;
    private string? _name;
    private readonly List<TextToMatch> _remove = [];
    private List<TextToMatch>? _possibilities;
    private bool _requireTextToClaimGroup;
    private List<string>? _ignoreBlockIfContains;
    private List<string>? _excludeNextLineIfFirstColumnStartsWith;
    private bool _boundSameLineWalkByOtherLabelPositions;

    public static WrRule Between(string startText, string endText)
    {
        var rule = new WrRule
        {
            _textStart = [new(startText) { ColumnMustStartWith = true }],
            _textEnd = [new(endText) { LineMustStartWith = true }, new("[END_OF_BLOCK]")],
            _position = LabelPosition.TextToFindIsBetweenLabels
        };
        
        rule._remove.Add(new(startText));
        
        return rule;
    }

    // "Text"/LabelIsBeforeTextToFind - the same TextStart property backs both (LabelToMatch's
    // Text property is a plain alias for TextStart), so this needs no separate field.
    public static WrRule After(string text)
    {
        var rule = new WrRule
        {
            _textStart = [new(text) { ColumnMustStartWith = true }],
            _position = LabelPosition.LabelIsBeforeTextToFind
        };
        
        rule._remove.Add(new(text));
        
        return rule;
    }

    // The LicenceProvisions grid's tick/cross/In/Not shape. Routed via
    // TextToFindIsBetweenLabels (not LabelIsBeforeTextToFind) - the generic-text path used
    // by After() discards the whole result if nothing is left after removing the label
    // text, which would silently swallow every genuinely-blank tick field.
    public static WrRule InOrder(string text, List<TextToMatch>? inOrderPossibilities, string? endText = null)
    {
        var rule = new WrRule
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

    public WrRule Named(string name)
    {
        _name = name;
        return this;
    }

    public WrRule WholeLine()
    {
        _limitTo = LimitTo.WholeLine;
        return this;
    }

    public WrRule NextLines(int n)
    {
        _nextLinesToFetch = n;
        return this;
    }

    public WrRule RequireTextToClaimGroup()
    {
        _requireTextToClaimGroup = true;
        return this;
    }

    public WrRule BoundByOtherLabels()
    {
        _boundSameLineWalkByOtherLabelPositions = true;
        return this;
    }

    public WrRule Possibilities(IEnumerable<TextToMatch> p)
    {
        _possibilities = p.ToList();
        return this;
    }

    public WrRule IgnoreIfContains(params string[] terms)
    {
        _ignoreBlockIfContains = terms.ToList();
        return this;
    }

    public WrRule SkipNextLineWhenStartsWith(params string[] terms)
    {
        _excludeNextLineIfFirstColumnStartsWith = terms.ToList();
        return this;
    }

    // For the After() shape only - a single same-line bound.
    public WrRule EndsAt(string text)
    {
        _textEnd = [new(text) { LineMustStartWith = true }];
        return this;
    }

    // For the Between()/InOrder() shapes - appends before the trailing [END_OF_BLOCK]
    // sentinel. Plain TextToMatch, no positional flag - use this when the extra end marker
    // only needs to bound the same-line/same-row walk.
    public WrRule AlsoEndsAt(params string[] texts)
    {
        var sentinel = _textEnd![^1];
        _textEnd = [.._textEnd.Take(_textEnd.Count - 1), ..texts.Select(t => new TextToMatch(t)), sentinel];
        
        return this;
    }

    // Same idea as AlsoEndsAt but with LineMustStartWith: true - for an extra end marker that
    // must anchor a whole line, not just bound a same-line walk. Only MeansOfAbstraction
    // needs this one (its "Records" end marker requires the stricter check).
    public WrRule AlsoEndsAtLineStart(params string[] texts)
    {
        var sentinel = _textEnd![^1];
        _textEnd = [.._textEnd.Take(_textEnd.Count - 1), ..texts.Select(t => new TextToMatch(t) { LineMustStartWith = true }), sentinel];
        
        return this;
    }

    public WrRule AlsoStartsWith(params string[] texts)
    {
        _textStart = [.._textStart!, ..texts.Select(t => new TextToMatch(t) { ColumnMustStartWith = true })];
        _remove.AddRange(texts.Select(t => new TextToMatch(t)));
        
        return this;
    }

    // Additive fallback alongside AlsoStartsWith (which still requires a column start): a
    // start text matched anywhere on the line, for cases where the value genuinely doesn't
    // land at a column boundary - e.g. "Time" sharing a row with "Inspection Date" as
    // "...Inspection Date: 26/1/2026 Time: 3pm" all in one column. Use specific-enough text
    // (a trailing colon, say) to avoid matching an ordinary word inside narrative prose.
    // Purely additive - existing column-start matches are untouched, so this can only add
    // new matches, never remove one.
    public WrRule AlsoStartsWithLoose(params string[] texts)
    {
        _textStart = [.._textStart!, ..texts.Select(t => new TextToMatch(t))];
        _remove.AddRange(texts.Select(t => new TextToMatch(t)));
        
        return this;
    }

    public WrRule Remove(IEnumerable<TextToMatch> items)
    {
        _remove.AddRange(items);
        return this;
    }

    // GeneralComments' own special case: "Actions"/"Summary" are valid additionalTextStarts
    // (so they must stay in TextStart) but must NOT be stripped from the captured value
    // when they recur mid-block as a genuine sub-heading - see RuleGeneralComments().
    public WrRule ExceptFromRemove(params string[] texts)
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
        PreviousLinesToFetch = 0,
        NextLinesToFetch = _nextLinesToFetch,
        Name = _name,
        Remove = _remove,
        Possibilities = _possibilities,
        RequireTextToBePresent = _requireTextToClaimGroup,
        IgnoreBlockIfContains = _ignoreBlockIfContains,
        LimitToExcludeNextLineIfFirstColumnStartsWith = _excludeNextLineIfFirstColumnStartsWith,
        LimitToBoundSameLineWalkByOtherLabelPositions = _boundSameLineWalkByOtherLabelPositions
    };
}