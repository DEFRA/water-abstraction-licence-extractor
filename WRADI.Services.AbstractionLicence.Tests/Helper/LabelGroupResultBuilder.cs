using WALE.ProcessFile.Core.Models;

namespace WRADI.Services.AbstractionLicence.Tests.Helper;

// The converter reads nested LabelGroupResults, so the tests have to build the same shape the
// extraction produces: a named group, results matched to a label, and text split into words.
public class LabelGroupResultBuilder
{
    private string? _labelGroupName;
    private string? _matchedLabelName;
    private IReadOnlyList<DocumentLine>? _text;
    private readonly List<LabelGroupResult> _subResults = [];

    public static LabelGroupResultBuilder A() => new();

    public LabelGroupResultBuilder Named(string labelGroupName)
    {
        _labelGroupName = labelGroupName;
        return this;
    }

    public LabelGroupResultBuilder Matching(string matchedLabelName)
    {
        _matchedLabelName = matchedLabelName;
        return this;
    }

    public LabelGroupResultBuilder Saying(params string[] lines)
    {
        _text = lines.SelectMany(TextToLines).ToList();
        return this;
    }

    public LabelGroupResultBuilder Containing(params LabelGroupResult[] subResults)
    {
        _subResults.AddRange(subResults);
        return this;
    }

    public LabelGroupResult Build() => new()
    {
        LabelGroupName = _labelGroupName,
        MatchedLabelName = _matchedLabelName,
        Text = _text,
        SubResults = _subResults
    };

    public static List<DocumentLine> TextToLines(string text) =>
    [
        new DocumentLine
        {
            Columns =
            [
                new DocumentLineColumn
                {
                    Words = text.Split(' ').Select(word => new DocumentLineWord { Text = word }).ToList()
                }
            ]
        }
    ];
}
