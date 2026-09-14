using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Constants;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Enums;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Direct unit tests for WrInspectionReportSchemaConverter.ToForm's date parsing, isolated
/// from PDF extraction - a MatchesResult with a single "Date" match is enough to exercise the
/// NormaliseOrdinalDateSuffixes fix without needing a real or dummy PDF fixture.
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
    // Real values pulled from the WR51 sample-set CSV - a PDF kerning/export artefact renders a
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

    private static MatchesResult BuildMatchesResultWithSourceOfSupply(string rawText)
    {
        var documentLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(DocumentLineColumn.TextToWords(rawText, null))],
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
                    LabelGroupName = WrInspectionReportFieldNames.SourceOfSupply,
                    MatchedLabelName = WrInspectionReportFieldNames.SourceOfSupply
                }
            ]
        };
    }

    // Real WR51 documents mark LicenceProvisions/Maintenance/ReadingsTaken checkboxes with
    // whatever tick/cross glyph the originating export toolchain produced - plain Unicode
    // symbols, or one of several Wingdings-style Private Use Area codepoints that render
    // visually as a tick but aren't the same character. GetInOrderStatus has to recognise each
    // one explicitly; a codepoint missing from this list silently resolves to Blank/Unknown
    // instead of a real verdict, with no build-time signal that anything is wrong (confirmed
    // real for U+F0D6 on wr51__nw0680001028r01__332683fa-... - see InOrderPossibilities for the
    // full glyph-frequency evidence behind this list).
    [Theory]
    [InlineData("✓", InOrderStatus.InOrder)]
    [InlineData("✔", InOrderStatus.InOrder)]
    [InlineData("√", InOrderStatus.InOrder)]
    [InlineData("🗸", InOrderStatus.InOrder)]
    [InlineData("", InOrderStatus.InOrder)]
    [InlineData("", InOrderStatus.InOrder)]
    [InlineData("", InOrderStatus.InOrder)]
    [InlineData("", InOrderStatus.InOrder)]
    [InlineData("", InOrderStatus.InOrder)]
    [InlineData("X", InOrderStatus.NotInOrder)]
    [InlineData("☒", InOrderStatus.NotInOrder)]
    [InlineData("×", InOrderStatus.NotInOrder)]
    public void WhenLicenceProvisionsFieldHasTickOrCrossGlyph_ThenResolvesToExpectedStatus(
        string rawGlyph,
        InOrderStatus expectedStatus)
    {
        // Arrange
        var matchesResult = BuildMatchesResultWithSourceOfSupply(rawGlyph);

        // Act
        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        // Assert
        Assert.Equal(expectedStatus, form.LicenceProvisions.SourceOfSupply);
    }

    private static MatchesResult BuildMatchesResultWithFormSentTo(string rawText)
    {
        var documentLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(DocumentLineColumn.TextToWords(rawText, null))],
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
                    LabelGroupName = WrInspectionReportFieldNames.FormSentTo,
                    MatchedLabelName = WrInspectionReportFieldNames.FormSentTo
                }
            ]
        };
    }

    // "Form sent to:" and "Date:" share the same physical line - RuleFormSentTo's own end
    // marker requires "Date" to start its own column, which only happens when "Form sent to:"
    // is blank (a wide gap before "Date:" triggers the column split). A real value filled in
    // close enough to "Date:" leaves both in one column with nothing to bound the match, so
    // "Date: <whatever>" rides along as part of the captured text - confirmed real on
    // sw0480020004__5e8bbcc0-...: "Form sent to: NA – response to operator by email Date: 4
    // January 2018". SplitFormSentToAndDate cuts the raw text at "Date" as a converter-level
    // fix, without touching the shared column-splitting matching engine.
    [Theory]
    [InlineData(
        "NA – response to operator by email Date: 4 January 2018",
        "NA – response to operator by email")]
    [InlineData("NA – response to operator by email", "NA – response to operator by email")] // no "Date" present - untouched
    [InlineData("", "")]
    public void WhenFormSentToRunsIntoDateOnSameLine_ThenTruncatesAtDate(string rawText, string expected)
    {
        // Arrange
        var matchesResult = BuildMatchesResultWithFormSentTo(rawText);

        // Act
        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        // Assert
        Assert.Equal(expected, form.Metadata.FormSentTo);
    }

    // The same column collision that strands "Date: 4 January 2018" inside FormSentTo also
    // stops RuleDate's own "Date:" label (also column-start-anchored) from ever matching on
    // this line at all - so truncating FormSentTo alone would silently make the date
    // disappear from the form entirely, rather than just move it back into its rightful
    // Metadata.Date field. Confirmed real on the same sw0480020004__5e8bbcc0-... document -
    // zero "Date" labelGroupName matches at all before this fix.
    [Fact]
    public void WhenFormSentToRunsIntoDateAndNoSeparateDateMatchExists_ThenDateIsRecoveredFromFormSentTo()
    {
        // Arrange - no separate "Date" LabelGroupResult at all, matching the real document
        var matchesResult = BuildMatchesResultWithFormSentTo(
            "NA – response to operator by email Date: 4 January 2018");

        // Act
        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        // Assert
        Assert.Equal("NA – response to operator by email", form.Metadata.FormSentTo);
        Assert.Equal("4 January 2018", form.Metadata.Date.RawDate);
        Assert.Equal(new DateOnly(2018, 1, 4), form.Metadata.Date.Date);
    }

    [Fact]
    public void WhenSeparateDateMatchAlreadyExists_ThenFormSentToFallbackIsNotUsed()
    {
        // Arrange - a real, separate "Date" match should always win over the FormSentTo
        // fallback, which only exists to cover the case where RuleDate found nothing at all.
        var formSentToLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(DocumentLineColumn.TextToWords(
                "NA – response to operator by email Date: 4 January 2018", null))],
            0,
            0,
            0,
            0);

        var dateLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(DocumentLineColumn.TextToWords("10/02/2026", null))],
            0,
            0,
            0,
            0);

        var matchesResult = new MatchesResult
        {
            Matches =
            [
                new LabelGroupResult
                {
                    Text = [formSentToLine],
                    LabelGroupName = WrInspectionReportFieldNames.FormSentTo,
                    MatchedLabelName = WrInspectionReportFieldNames.FormSentTo
                },
                new LabelGroupResult
                {
                    Text = [dateLine],
                    LabelGroupName = "Date",
                    MatchedLabelName = "Date"
                }
            ]
        };

        // Act
        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        // Assert
        Assert.Equal(new DateOnly(2026, 2, 10), form.Metadata.Date.Date);
    }
}
