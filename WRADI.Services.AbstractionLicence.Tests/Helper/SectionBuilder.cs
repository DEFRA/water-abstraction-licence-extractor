using WALE.ProcessFile.Core.Models;

namespace WRADI.Services.AbstractionLicence.Tests.Helper;

public class SectionBuilder
{
    private string[] _lines = [];

    public static SectionBuilder A() => new();

    public SectionBuilder WithLines(params string[] lines)
    {
        _lines = lines;
        return this;
    }

    // A DocumentLineWord refuses to hold a space, so a line goes in word by word and the column
    // joins them back with one.
    public LabelGroupResult Build() => new()
    {
        Text = _lines
            .Select(line => new DocumentLine
            {
                Columns =
                [
                    new DocumentLineColumn(line
                        .Split(' ')
                        .Select(word => new DocumentLineWord(
                            word,
                            null,
                            DocumentLineWordCoordinates.NotKnown(),
                            null))
                        .ToList())
                ]
            })
            .ToList()
    };
}
