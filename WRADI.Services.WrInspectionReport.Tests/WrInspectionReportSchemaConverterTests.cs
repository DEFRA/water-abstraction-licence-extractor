using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Converters;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Direct unit tests for WrInspectionReportSchemaConverter.ToForm's date parsing, isolated
/// from PDF extraction - a MatchesResult with a single "Date" match is enough to exercise the
/// NormaliseOrdinalDateSuffixes fix (see analysis/07-label-matching-and-debugging.md) without
/// needing a real or dummy PDF fixture.
/// </summary>
public class WrInspectionReportSchemaConverterTests
{
    private static MatchesResult BuildMatchesResultWithFormDate(string rawDate)
    {
        var documentLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(DocumentLineColumn.TextToWords(rawDate, null))],
            0,
            0,
            0,
            0);

        return new MatchesResult
        {
            Matches =
            [
                new LabelGroupResult
                {
                    Text = [documentLine],
                    LabelGroupName = "Date",
                    MatchedLabelName = "Date"
                }
            ]
        };
    }

    [Theory]
    // Real values pulled from the WR51 corpus CSV - a PDF kerning/export artefact renders a
    // stray space before the ordinal suffix ("10 th" instead of "10th"), which neither form
    // (glued or spaced) parses via DateOnly.TryParse until the whole suffix is stripped.
    [InlineData("10 th February 2026", 2026, 2, 10)]
    [InlineData("20 th January 2026", 2026, 1, 20)]
    [InlineData("6 th March 2026", 2026, 3, 6)]
    [InlineData("4 th March 2026", 2026, 3, 4)]
    [InlineData("18 th March 2026", 2026, 3, 18)]
    [InlineData("21 st March 2026", 2026, 3, 21)]
    [InlineData("2 nd April 2026", 2026, 4, 2)]
    [InlineData("3 rd May 2026", 2026, 5, 3)]
    public void WhenFormDateHasSpacedOrdinalSuffix_ThenParsesCorrectly(
        string rawDate,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        // Arrange
        var matchesResult = BuildMatchesResultWithFormDate(rawDate);

        // Act
        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        // Assert
        Assert.Equal(rawDate, form.Metadata.Date.RawDate);
        Assert.NotNull(form.Metadata.Date.Date);
        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), form.Metadata.Date.Date);
    }

    [Theory]
    // Formats that already worked, or that the fix also happens to normalise without a space
    // present - either way, the ordinal-suffix stripping must not disturb the correct result.
    [InlineData("10th February 2026", 2026, 2, 10)] // glued ordinal - "th" still gets stripped
    [InlineData("10/02/2026", 2026, 2, 10)]
    [InlineData("13.5.98", 1998, 5, 13)]
    public void WhenFormDateAlreadyParsesOrHasNoSpaceBeforeSuffix_ThenStillParsesCorrectly(
        string rawDate,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        // Arrange
        var matchesResult = BuildMatchesResultWithFormDate(rawDate);

        // Act
        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        // Assert
        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), form.Metadata.Date.Date);
    }

    [Fact]
    public void WhenFormDateIsJustAYear_ThenStillDoesNotParse()
    {
        // A bare year ("2026", no day/month) is a genuinely incomplete date - the ordinal-suffix
        // fix must not paper over this by making it parse to some arbitrary day/month.
        // Arrange
        var matchesResult = BuildMatchesResultWithFormDate("2026");

        // Act
        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        // Assert
        Assert.Null(form.Metadata.Date.Date);
    }
}
