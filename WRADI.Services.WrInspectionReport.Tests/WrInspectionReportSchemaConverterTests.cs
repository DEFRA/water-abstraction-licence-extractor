using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Converters;

namespace WRADI.Services.WrInspectionReport.Tests;

/// A MatchesResult with a single "Date" match is enough; no PDF fixture needed.
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
    // Real values from the WR51 CSV.
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
    // The ordinal stripping must not disturb a format that already parsed.
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
        // A bare year is incomplete; the fix must not make it parse to an arbitrary day/month.
        // Arrange
        var matchesResult = BuildMatchesResultWithFormDate("2026");

        // Act
        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        // Assert
        Assert.Null(form.Metadata.Date.Date);
    }
}