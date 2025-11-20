using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Configuration;
using WALE.ProcessFile.Models.Enums;
using WALE.ProcessFile.Models.Interfaces;
using WALE.ProcessFile.Services.AwsTextract;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using MatchType = WALE.ProcessFile.Models.Enums.MatchType;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

public class AwsTextractOcrPdfTests
{
    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");

    private readonly IPdfDataExtractorService _pdfDataExtractorCombined = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new AwsTextractOcrDataExtractorService(
                TestConfig.AwsAccessKey,
                TestConfig.AwsSecretKey,
                CacheService,
                OutputService)
        },
        CacheService,
        OutputService,
        TestConfig.PdfFolder);

    private static string PdfFolder => TestConfig.PdfFolder;

    private readonly Dictionary<string, string> _fileLicenceMapping = new() { { "", "" } };

    private Task<MatchesResult> GetMatchesAsync(string fileName)
    {
        return _pdfDataExtractorCombined.GetMatchesAsync(
            PdfFolder + fileName,
            new LookupConfiguration(
                LabelConfiguration.GetLabels(),
                _fileLicenceMapping),
            [PdfFolder + fileName],
            0);
    }

    [Fact]
    public async Task WhenA_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "14460030853 licence effective 24.07.2005.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(11, resultList.Count);
        // Tesseract struggles to read licence number in header and abstraction limits
        // in this document. Azure AI does read them

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(18, records.Text!.Count);

        var points = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);

        var licenceNumber = resultList.Single(result => result.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("14/46/03/0852", licenceNumber.Text?.FirstOrDefault()?.Text); // TODO should be 14/46/03/0853

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);

        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");

        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr T M C Davey", nameResult.Text?[0]?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchType.MatchIsEitherSideOfLabel, nameResult.MatchType);

        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");

        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);

        Assert.NotNull(abstractionLimitsResult.SubResults);
        Assert.Single(abstractionLimitsResult.SubResults);
        Assert.Equal(16, abstractionLimitsResult.LineNumber);

        var abstractionLimitsSection1 = abstractionLimitsResult.SubResults[0];
        Assert.Equal(8, abstractionLimitsSection1.Text!.Count);

        Assert.NotNull(abstractionLimitsSection1.SubResults);
        Assert.Single(abstractionLimitsSection1.SubResults!);

        var section1Sub1 = abstractionLimitsSection1.SubResults![0];
        Assert.Equal(8, section1Sub1.SubResults!.Count);

        var linkedLicences = section1Sub1.SubResults.Where(x => x.MatchedLabel?.Name == "LinkedLicenceNumber");
        Assert.Single(linkedLicences);

        var linkedLicenceFilenames =
            section1Sub1.SubResults.Where(x => x.MatchedLabel?.Name == "LinkedLicenceFilename");
        Assert.Empty(linkedLicenceFilenames);

        var perDay = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()
            ?.Text;
        Assert.Equal("77", perDay);

        var perDayUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()
            ?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        var perYear1 = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()
            ?.Text;
        Assert.Equal("5116", perYear1);

        var perYearUnits1 = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()
            ?.Text;
        Assert.Equal("cubic metres", perYearUnits1);

        var perYear2 = section1Sub1.SubResults
            .LastOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()
            ?.Text;
        Assert.Equal("5116", perYear2);

        var perYearUnits2 = section1Sub1.SubResults
            .LastOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()
            ?.Text;
        Assert.Equal("cubic metres", perYearUnits2);

        // See notes RE licence
    }
}