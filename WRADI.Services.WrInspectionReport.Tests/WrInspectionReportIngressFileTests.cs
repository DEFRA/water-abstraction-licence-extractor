using WALE.ProcessFile.Core.Helpers;
using WRADI.Services.ProcessFile.WrInspectionReport;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// The ingress key a discovered WR51 document is fetched under. An unprefixed key finds almost
/// nothing in the real bucket.
/// </summary>
public class WrInspectionReportIngressFileTests
{
    private const string Permit = "0328170007";
    private const string FileId = "f6a9d693-703e-4d15-8211-a6690c1e425e";

    [Fact]
    public void WhenBuildingAnIngressFileName_ThenItIsPrefixedAndLowercased()
    {
        // Act
        var result = WrInspectionReportIngressFile.BuildFileName("0328220092R01", FileId.ToUpper());

        // Assert
        Assert.Equal($"wr51__0328220092r01__{FileId}.pdf", result);
    }

    /// <summary>
    /// Why the prefix is safe to add: ExtractFileId takes the last "__" part, so three parts read
    /// the same as two.
    /// </summary>
    [Fact]
    public void WhenExtractingTheFileIdFromAPrefixedName_ThenItStillResolves()
    {
        // Arrange
        var fileName = WrInspectionReportIngressFile.BuildFileName(Permit, FileId);

        // Act
        var fileId = FileHelper.ExtractFileId(fileName);

        // Assert
        Assert.Equal(Guid.Parse(FileId), fileId);
    }

    /// <summary>
    /// Why the permit must stay on the queue message: ExtractPermitNumber reads up to the first
    /// "__", which on a prefixed name is the prefix.
    /// </summary>
    [Fact]
    public void WhenExtractingThePermitFromAPrefixedName_ThenItReturnsThePrefixNotThePermit()
    {
        // Arrange
        var fileName = WrInspectionReportIngressFile.BuildFileName(Permit, FileId);

        // Act
        var permitNumber = FileHelper.ExtractPermitNumber(fileName);

        // Assert
        Assert.Equal("wr51", permitNumber);
        Assert.NotEqual(Permit, permitNumber);
    }
}
