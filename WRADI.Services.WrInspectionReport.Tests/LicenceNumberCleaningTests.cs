using WRADI.DocumentType.WrInspectionReport.Converters;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Direct unit tests for WrInspectionReportSchemaConverter.CleanLicenceNumbers - pure string
/// logic, no MatchesResult/PDF fixture needed. Pins the delimiter set and the "drop pieces with
/// no digits" heuristic found while tracing real corpus values (see
/// WrInspectionReport.LicenceNumberCleaned's own comment for the full rationale and the
/// DEFRA water-abstraction-licence-finder precedent this canonical form matches).
/// </summary>
public class LicenceNumberCleaningTests
{
    [Fact]
    public void WhenSingleLicenceNumber_ThenOnePieceStripped()
    {
        var result = WrInspectionReportSchemaConverter.CleanLicenceNumbers("7/34/06/*G/0027");

        Assert.Equal(["73406G0027"], result);
    }

    [Fact]
    public void WhenAmpersandDelimited_ThenBothPiecesReturned()
    {
        var result = WrInspectionReportSchemaConverter.CleanLicenceNumbers("03/28/70/0023 & 03/28/70/0024");

        Assert.Equal(["0328700023", "0328700024"], result);
    }

    [Fact]
    public void WhenAndDelimited_ThenBothPiecesReturned()
    {
        var result = WrInspectionReportSchemaConverter.CleanLicenceNumbers("2/27/05/032 and 2/27/05/011");

        Assert.Equal(["22705032", "22705011"], result);
    }

    [Fact]
    public void WhenCommaDelimited_ThenAllPiecesReturned()
    {
        var result = WrInspectionReportSchemaConverter.CleanLicenceNumbers(
            "2/27/09/081, NE/27/0009/012, NE/027/0009/013");

        Assert.Equal(["22709081", "NE270009012", "NE0270009013"], result);
    }

    [Fact]
    public void WhenPieceHasNoDigitsAtAll_ThenThatPieceIsDropped()
    {
        // Real corpus case: "26 71 314 004 Brennand and Whitendale" - "Whitendale" is a
        // reservoir/site name caught by the "and" split, not a second licence number. No
        // licence number is ever digit-free, so this heuristic drops it rather than emitting a
        // false match key.
        var result = WrInspectionReportSchemaConverter.CleanLicenceNumbers(
            "26 71 314 004 Brennand and Whitendale");

        Assert.Equal(["2671314004BRENNAND"], result);
    }

    [Fact]
    public void WhenNullOrWhitespace_ThenEmptyList()
    {
        Assert.Empty(WrInspectionReportSchemaConverter.CleanLicenceNumbers(null));
        Assert.Empty(WrInspectionReportSchemaConverter.CleanLicenceNumbers("   "));
    }

    [Fact]
    public void WhenDuplicatePiecesAfterCleaning_ThenDeduplicated()
    {
        var result = WrInspectionReportSchemaConverter.CleanLicenceNumbers("28/39/23/0090 and 28-39-23-0090");

        Assert.Equal(["2839230090"], result);
    }
}
