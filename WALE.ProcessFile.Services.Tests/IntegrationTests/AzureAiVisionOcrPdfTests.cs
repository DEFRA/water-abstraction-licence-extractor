using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

public class AzureAiVisionOcrPdfTests
{
    private readonly IPdfDataExtractorService _pdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new AzureAiVisionOcrDataExtractorService(
                TestConfig.AiVisionEndpoint,
                TestConfig.AiVisionKey)
        },
        TestConfig.PdfFolder);
    
    private readonly Dictionary<string, string> _fileLicenceMapping = new() {{"", ""}};

    private string PdfFolder => TestConfig.PdfFolder;
    private const bool UseCache = true;
    
    [Fact]
    public async Task Handsigned_WhenNearPreviousLineIsCompany_ThenFoundCorrect_Ish()
    {
        // Arrange
        const string filename = "Non-Application Licence Document (22.09.1986).PDF";
        
        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
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
        Assert.Equal(6, abstractionLimitsResult.Text?.Count);

        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        
        Assert.Equal(6, section1Sub1.SubResults!.Count);
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("36000", perDayValue?.Text?.FirstOrDefault()?.Text);

        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("1500", perHourValue?.Text?.FirstOrDefault()?.Text);
        
        var inTotalUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "InTotalUnits");
        Assert.Equal("gallons", inTotalUnits?.Text?.FirstOrDefault()?.Text);

        var inTotalValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "InTotalValue");
        Assert.Equal("500000", inTotalValue?.Text?.FirstOrDefault()?.Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("11/42/28.2/7", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        // TODO - other 2 things
    }
    
    [Fact]
    public async Task VeryFaintText_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Old 6078942.PDF";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(5, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("MID CHESHIRE WATER BOARD", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResult.MatchedLabel!.Text!, StringComparer.InvariantCultureIgnoreCase);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearNextLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(17, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);
        
        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        Assert.Equal(6, section1Sub1.SubResults!.Count);
        
        // This file incorrectly gets results that have been crossed out
        var perYearUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("million gallons", perYearUnits?.Text?.FirstOrDefault()?.Text);

        var perYearValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("1.095", perYearValue?.Text?.FirstOrDefault()?.Text); // Should actually be 1,095
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("million gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("3.5", perDayValue?.Text?.FirstOrDefault()?.Text);

        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("thousand gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("210", perHourValue?.Text?.FirstOrDefault()?.Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25/68/1/158", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        // TODO - other 2 things
    }

    [Fact]
    public async Task X_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Issued Licence - 01081966.PDF";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(4, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("SHERBORNE SCHOOL", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("authority hereby licence", nameResult.MatchedLabel!.Text!, StringComparer.InvariantCultureIgnoreCase);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearNextLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(11, abstractionLimitsResult.Text?.Count);        
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);
        
        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        Assert.Equal(8, section1Sub1.SubResults!.Count);        
        
        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("1200", perHourValue?.Text?.FirstOrDefault()?.Text);

        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("13400", perDayValue?.Text?.FirstOrDefault()?.Text);

        var perMonthUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerMonthUnits");
        Assert.Equal("gallons", perMonthUnits?.Text?.FirstOrDefault()?.Text);

        var perMonthValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerMonthValue");
        Assert.Equal("134000", perMonthValue?.Text?.FirstOrDefault()?.Text);

        var perYearUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("gallons", perYearUnits?.Text?.FirstOrDefault()?.Text);

        var perYearValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("667000", perYearValue?.Text?.FirstOrDefault()?.Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("16/52/2/371", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        // TODO - other 2 things
    }
    
    [Fact]
    public async Task Succession_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Non-Application Licence Document (08.06.1987).PDF";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
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
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        
        Assert.Equal(4, section1Sub1.SubResults!.Count);
        
        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("1500", perHourValue?.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        // Surprisingly the OCR really struggles with this document
        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("5183", perDayValue?.Text?.FirstOrDefault()?.Text); // Should actually be 5600    
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/22/271", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        // TODO - other 2 things
    }
    
    [Fact]
    public async Task WhenNearPreviousLineIsCompany_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "14460030853 licence effective 24.07.2005.PDF";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(5, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr T M C Davey", nameResult.Text?[0]?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("14/46/03/0853", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(10, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        Assert.Equal(5, section1Sub1.SubResults!.Count);
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("cubic metres", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("77", perDayValue?.Text?.FirstOrDefault()?.Text);
        
        var perYearUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("cubic metres", perYearUnits?.Text?.FirstOrDefault()?.Text);

        var perYearValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("5116", perYearValue?.Text?.FirstOrDefault()?.Text); // This is actually from 1 april to 30 sept per year
        
        // TODO - other 2 things
    }
    
    [Fact]
    public async Task WhenIsOldCrossedOut_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Old 6082700.PDF";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
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
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("million gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("1.25", perDayValue?.Text?.FirstOrDefault()?.Text);        
        
        var perYearUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("million gallons", perYearUnits?.Text?.FirstOrDefault()?.Text);

        var perYearValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("300", perYearValue?.Text?.FirstOrDefault()?.Text);
        
        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("thousand gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("52", perHourValue?.Text?.FirstOrDefault()?.Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25/68/3/91", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        // TODO - other 2 things
    }
    
    [Fact]
    public async Task Z1_X2_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "14460030852 licence effective 24.07.2005.PDF";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(5, resultList.Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr T M C Davey", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.True(abstractionLimitsSection.IsOcr);
        Assert.Equal(7, abstractionLimitsSection.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, licenceNumberResult.MatchedLabel!.Position);        
        Assert.Equal("14/46/03/0852", licenceNumberResult.Text!.FirstOrDefault()?.Text);        
    }
    
    [Fact]
    public async Task Z2_X3_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "1-21-00-010 5822315.PDF";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(4, resultList.Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("A A C McArthur", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["Licensee"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearNextLineIsCompany, nameResult.MatchType);
        
        /*var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.True(abstractionLimitsSection.IsOcr);
        Assert.Equal(7, abstractionLimitsSection.Text?.Count);*/
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, licenceNumberResult.MatchedLabel!.Position);        
        Assert.Equal("21/0/10", licenceNumberResult.Text!.FirstOrDefault()?.Text);        
    }
}