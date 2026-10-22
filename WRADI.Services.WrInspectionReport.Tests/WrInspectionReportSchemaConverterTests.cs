using System.Globalization;
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

    private static MatchesResult BuildMatchesResultWithInspectionDate(string rawDate)
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
                    LabelGroupName = "InspectionDate",
                    MatchedLabelName = "InspectionDate"
                }
            ]
        };
    }

    /// The cases below pass on a UK machine whether or not the parse names a format provider, so
    /// they only mean anything under the culture the API container actually runs.
    private static T UnderInvariantCulture<T>(Func<T> act)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        try
        {
            return act();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    // 433501 and the other v3 UAT documents: a day past the 12th failed to parse at all and the
    // summary fell back to the DMS upload date, and a day of 12 or less parsed transposed -
    // "12/06/2026" was stored as 6 December.
    [InlineData("20/07/2026", 2026, 7, 20)]
    [InlineData("12/06/2026", 2026, 6, 12)]
    [InlineData("07/05/2026", 2026, 5, 7)]
    [InlineData("03.08.2028", 2028, 8, 3)]
    [InlineData("11/06/26", 2026, 6, 11)]
    [InlineData("25 August 2026", 2026, 8, 25)]
    public void WhenInspectionDateIsDayFirst_ThenParsesDayFirstUnderAnyCulture(
        string rawDate,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        // Arrange
        var matchesResult = BuildMatchesResultWithInspectionDate(rawDate);

        // Act
        var form = UnderInvariantCulture(
            () => WrInspectionReportSchemaConverter.ToForm(matchesResult, null));

        // Assert
        Assert.NotNull(form.InspectionDate.DateTime);
        Assert.Equal(
            new DateTime(expectedYear, expectedMonth, expectedDay),
            form.InspectionDate.DateTime!.Value.Date);
    }

    [Theory]
    [InlineData("20/07/2026", 2026, 7, 20)]
    [InlineData("12/06/2026", 2026, 6, 12)]
    public void WhenFormDateIsDayFirst_ThenParsesDayFirstUnderAnyCulture(
        string rawDate,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        // Arrange
        var matchesResult = BuildMatchesResultWithFormDate(rawDate);

        // Act
        var form = UnderInvariantCulture(
            () => WrInspectionReportSchemaConverter.ToForm(matchesResult, null));

        // Assert
        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), form.Metadata.Date.Date);
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