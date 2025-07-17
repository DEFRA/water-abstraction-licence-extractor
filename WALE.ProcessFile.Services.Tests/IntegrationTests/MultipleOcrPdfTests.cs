using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

public class MultipleOcrPdfTests
{
    private readonly IPdfDataExtractorService _pdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath),
            new AzureAiVisionOcrDataExtractorService(
                TestConfig.AiVisionEndpoint,
                TestConfig.AiVisionKey)
        },
        TestConfig.PdfFolder);

    private readonly Dictionary<string, string> _fileLicenceMapping = new() {{"", ""}};    
    private static string PdfFolder => TestConfig.PdfFolder;
    private const bool UseCache = true;
    
    [Fact]
    public async Task GetSomeFromTesseractAndSomeFromAzureAi_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Non-Application Licence Document (08.06.1987).PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(5, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("H.H. Henderson & C. Wentworth-Stanley", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["Succession to licence", "as amended by"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(12, abstractionLimitsResult.Text?.Count);
        
        Assert.Single(abstractionLimitsResult!.SubResults!);

        var abstractionPoint1 = abstractionLimitsResult!.SubResults![0];
        Assert.NotNull(abstractionPoint1);
        Assert.Equal(12, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);
        
        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        
        Assert.Equal(4, section1Sub1.SubResults!.Count);
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("5183", perDayValue?.Text?.FirstOrDefault()?.Text); // Should be 5600, bad OCR

        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("1500", perHourValue?.Text?.FirstOrDefault()?.Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/22/271", licenceNumberResult.Text?.FirstOrDefault()?.Text);
    }
    
    [Fact]
    public async Task WhenIsOldCrossedOut_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Old 6082700.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(5, resultList.Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.NotNull(nameResult);
        // Is crossed out but Azure AI can read it
        Assert.Equal("WARRINGTON RUNCORN AND DISTRICT WATER BOARD", nameResult.Text?.First().Text);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.NotNull(abstractionLimitsResult); // Is crossed out but Azure AI can read it
        Assert.Equal(11, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);
        
        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);
        
        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        
        Assert.Equal(6, section1Sub1.SubResults!.Count);

        var perYearUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("million gallons", perYearUnits?.Text?.FirstOrDefault()?.Text);

        var perYearValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("300", perYearValue?.Text?.FirstOrDefault()?.Text);      
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("million gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("1.25", perDayValue?.Text?.FirstOrDefault()?.Text);

        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("thousand gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("52", perHourValue?.Text?.FirstOrDefault()?.Text);

        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25/68/3/91", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }
    
    [Fact]
    public async Task Handsigned_WhenNearPreviousLineIsCompany_ThenFoundCorrect_Ish()
    {
        // Arrange
        const string filename = "Non-Application Licence Document (22.09.1986).PDF";
        
        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(5, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        // NOTE - According to companies house this is actual H.N. BUTLER FARMS LIMITED        
        Assert.Equal("H. W. Butter Farms Ltd", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("( hereinafter referred to as \"The Licence Holder\" )", nameResult.MatchedLabel!.Text!);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(3, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);
        
        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        
        Assert.Equal(6, section1Sub1.SubResults!.Count);

        var inTotalUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "InTotalUnits");
        Assert.Equal("gallons", inTotalUnits?.Text?.FirstOrDefault()?.Text);

        var inTotalValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "InTotalValue");
        Assert.Equal("500000", inTotalValue?.Text?.FirstOrDefault()?.Text);      
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("36000", perDayValue?.Text?.FirstOrDefault()?.Text);

        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("1500", perHourValue?.Text?.FirstOrDefault()?.Text);        
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("11/42/28.2/7", licenceNumberResult.Text?.FirstOrDefault()?.Text);
    }
}