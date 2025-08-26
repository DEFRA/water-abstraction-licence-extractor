using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Enums.OutputSchema;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
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
    
    private Task<MatchesResult> GetMatchesAsync(string fileName)
    {
        return _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + fileName,
            new LookupConfiguration(
                LabelConfiguration.GetLabels(),
                _fileLicenceMapping,
                "Output/",
                "Cache/"),
            [PdfFolder + fileName]);
    }
    
    [Fact]
    public async Task Handsigned_WhenNearPreviousLineIsCompany_ThenFoundCorrect_Ish()
    {
        // Arrange
        const string filename = "Non-Application Licence Document (22.09.1986).PDF";
        
        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(6, resultList.Count);

        /*var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("x", issuerResult.Text?.FirstOrDefault()?.Text);*/
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("22ND DAY OF SEPTEMBER 1986", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        // NOTE - According to companies house this is actual H.N. BUTLER FARMS LTD        
        Assert.Equal("H. W. Butter Farms Ltd", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("( hereinafter referred to as \"The Licence Holder\" )", nameResult.MatchedLabel!.Text!.Select(x => x.Text));
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
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(7, resultList.Count);

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Mersey and Weaver River Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("twenty-third day of .O.C", dateOfIssue.Text?.FirstOrDefault()?.Text); // TODO this isn't right
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("MID CHESHIRE WATER BOARD", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResult.MatchedLabel!.Text?.Select(x => x.Text)!, StringComparer.InvariantCultureIgnoreCase);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(9, abstractionLimitsResult.Text?.Count);
        
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
        Assert.Equal("25/68/1/158/", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        // TODO - other 2 things
    }

    [Fact]
    public async Task X_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Issued Licence - 01081966.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(6, resultList.Count);

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("National Rivers Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("First", dateOfIssue.Text?.FirstOrDefault()?.Text); // TODO wrong
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("SHERBORNE SCHOOL", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("authority hereby licence", nameResult.MatchedLabel!.Text?.Select(x => x.Text), StringComparer.InvariantCultureIgnoreCase);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearNextLineIsMatch, nameResult.MatchType);
        
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
        Assert.Equal(8, section1Sub1.SubResults!.Count);        
        
        var perHourUnits = section1Sub1.SubResults?
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("1200", perHourValue?.Text?.FirstOrDefault()?.Text);

        var perDayUnits = section1Sub1.SubResults?
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("13400", perDayValue?.Text?.FirstOrDefault()?.Text);

        var perMonthUnits = section1Sub1.SubResults?
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerMonthUnits");
        Assert.Equal("gallons", perMonthUnits?.Text?.FirstOrDefault()?.Text);

        var perMonthValue = section1Sub1.SubResults?
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerMonthValue");
        Assert.Equal("134000", perMonthValue?.Text?.FirstOrDefault()?.Text);

        var perYearUnits = section1Sub1.SubResults?
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
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
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        //Assert.Equal(5, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("H.H. Henderson & C. Wentworth-Stanley", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["Succession to licence", "as amended by"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
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
        
        Assert.Equal(4, section1Sub1.SubResults.Count);
        
        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("5183", perHourValue?.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        // Surprisingly the OCR really struggles with this document (TODO fix for this)
        //var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        //Assert.Equal("5183", perDayValue?.Text?.FirstOrDefault()?.Text); // Should actually be 5600    
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/22/271", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        // TODO - other 2 things
    }

    [Fact]
    public async Task WhenZ_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "6.5.4_Application_New_Issued_Licence_20.08.2014.pdf";
        
        // Act
        var resultFull = await GetMatchesAsync(filename);
        
        var dateOfIssue = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        
        var agreedSchemaLicenceGroup = SchemaConverter.ToLicenceGroup(resultFull);
        Assert.Single(agreedSchemaLicenceGroup.Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Equal(new DateTime(2014, 08, 20), agreedSchemaLicence.LicenceVersion.IssueDate);
    }

    [Fact]
    public async Task WhenNearPreviousLineIsCompany_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "14460030853 licence effective 24.07.2005.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(10, resultList.Count);

        var dateOfIssue = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        
        var dateOfOriginalIssue = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "DateOfOriginalIssue");
        Assert.NotNull(dateOfOriginalIssue);
        
        var dateEffective = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "DateEffective");
        Assert.NotNull(dateEffective);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr T M C Davey", nameResult.Text?[0]?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.MatchIsEitherSideOfLabel, nameResult.MatchType);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("14/46/03/0853", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(9, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        Assert.Equal(6, section1Sub1.SubResults!.Count);
        
        Assert.Equal("1 January and ending on 31 December", section1Sub1.SubResults.Last().Text!.Single().Text);        
        
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
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(8, resultList.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Mersey and Weaver River Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("third day of April 19 70", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.NotNull(nameResult);
        // Is crossed out but Azure AI can read it
        Assert.Equal("WARRINGTON, RUNCORN AND DISTRICT WATER BOARD", nameResult.Text?.First().Text);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.NotNull(abstractionLimitsResult); // Is crossed out but Azure AI can read it
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Equal(2, abstractionLimitsSections.Count); // TODO it should be 1 section, with one crossed out

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);
        
        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        Assert.Equal(7, section1Sub1.SubResults!.Count);
        
        var pointName = section1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel?.Name == "PointCondition")?.Text!.First().Text;
        
        Assert.Equal("(1)", pointName);
        
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
        Assert.Equal("25/68/3/91/", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        // TODO - other 2 things
    }
    
    [Fact]
    public async Task Z1_X2_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "14460030852 licence effective 24.07.2005.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(10, resultList.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("The Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr T M C Davey", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.MatchIsEitherSideOfLabel, nameResult.MatchType);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.True(abstractionLimitsSection.IsOcr);
        Assert.Equal(11, abstractionLimitsSection.Text?.Count);
        
        Assert.Single(abstractionLimitsSection.SubResults!);
        Assert.Equal(11, abstractionLimitsSection.SubResults![0].Text!.Count);
        
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
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("28 DAY OF March 1984", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var issuer = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuer);
        Assert.StartsWith("Northumbrian Water Authority", issuer.Text?.FirstOrDefault()?.Text);
        
        // Assert
//        Assert.Equal(4, resultList.Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("A A C McArthur", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["Licensee"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
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
    
    [Fact]
    public async Task Z3_X3_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "08-36-19-S-0101 5826949.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(11, resultList.Count);

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("The Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var pointResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        
        Assert.NotNull(pointResult);
        Assert.True(pointResult.IsOcr);
    }
    
    [Fact(Skip = "DebuggingImageIssue")]
    public async Task ScannedFileUploaded_ThenFindXuncorn_DebuggingTest()
    {
        // Arrange
        const string filename = "Licence - Old 6082700.PDF";

        if (File.Exists("Licence - Old 6082700/PdfPig/Text/cache-metadata.json"))
        {
            File.Delete("Licence - Old 6082700/PdfPig/Text/cache-metadata.json");
        }        
        
        if (File.Exists("Licence - Old 6082700/PdfPig/Images/cache-metadata.json"))
        {
            File.Delete("Licence - Old 6082700/PdfPig/Images/cache-metadata.json");
        }

        if (File.Exists("Licence - Old 6082700/PdfPig/Images/page-1-image-1.bmp"))
        {
            File.Delete("Licence - Old 6082700/PdfPig/Images/page-1-image-1.bmp");
        }

        if (File.Exists("Licence - Old 6082700/AzureAiVisionOcr/Text/ocr-page-1-image-1.json"))
        {
            File.Delete("Licence - Old 6082700/AzureAiVisionOcr/Text/ocr-page-1-image-1.json");
        }

        // Act
        var resultFull = await GetMatchesAsync(filename);

        // Assert
        var allText = string.Join(' ', resultFull.Pages[0].Providers[1].Text!);
        Assert.Contains("UNCORN", allText);
    }
    
    [Fact]
    public async Task When_YorkshireWaterCompany1_ThenY()
    {
        // Arrange
        const string filename = "2-26-32-126 6937559.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        
        var dateOfIssue = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        
        var dateOfOriginalIssue = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "DateOfOriginalIssue");
        Assert.NotNull(dateOfOriginalIssue);
        
        var dateEffective = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "DateEffective");
        Assert.NotNull(dateEffective);
        
        var points = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        Assert.Equal(4, points.Text!.Count);
        Assert.Equal("2. POINT OF ABSTRACTION", points.Text![0].Text);
        Assert.Equal("and \"F\" on the map", points.Text![3].Text);

        var pointPurposeGroup = points.SubResults
            .Where(psr => psr.MatchedLabel?.Name == "PointPurposeGroup")
            .ToList();

        Assert.Single(pointPurposeGroup);
        
        var pointsSubs = pointPurposeGroup[0].SubResults
            .Where(psr => psr.MatchedLabel?.Name == "Point")
            .ToList();
        
        Assert.Equal(4, pointsSubs.Count);
        
        var purpose = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Purpose");
        Assert.NotNull(purpose);
        
        Assert.Equal(2, purpose.Text!.Count);
        Assert.Equal("4. PURPOSE OF ABSTRACTION", purpose.Text![0].Text);
        Assert.Equal("Water undertaking", purpose.Text![1].Text);
        
        var abstractionLimitsResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(18, abstractionLimitsResult.Text?.Count);
        Assert.Equal("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED DURING THE", abstractionLimitsResult.Text![0].Text);

        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Equal(5, abstractionLimitsSections.Count);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults[0];
        
        var pointName = section1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel?.Name == "PointCondition")?.Text!.First().Text;
        
        Assert.Equal("(1)", pointName);
        
        Assert.Equal(4, section1Sub1.Text?.Count);
        Assert.Equal(5, section1Sub1.SubResults.Count);

        var units1 = section1Sub1.SubResults[1];
        Assert.Equal("cubic metres", units1.Text![0].Text);
        Assert.Equal("PerDayUnits", units1.MatchedLabel!.Name);
        Assert.Equal(31, units1.LineNumber);
        
        var units2 = section1Sub1.SubResults[2];
        Assert.Equal("cubic metres", units2.Text![0].Text);
        Assert.Equal("PerYearUnits", units2.MatchedLabel!.Name);
        Assert.Equal(32, units2.LineNumber);
        
        var value1 = section1Sub1.SubResults[3];
        Assert.Equal("45460.92", value1.Text![0].Text);
        Assert.Equal("PerDayValue", value1.MatchedLabel!.Name);
        
        var value2 = section1Sub1.SubResults[4];
        Assert.Equal("13638276", value2.Text![0].Text);
        Assert.Equal("PerYearValue", value2.MatchedLabel!.Name);
        
        abstractionLimitsSection = abstractionLimitsSections[4];
        var section4Sub1 = abstractionLimitsSection.SubResults[0];
        
        var value3 = section4Sub1.SubResults[3];
        Assert.Equal("100000", value3.Text![0].Text);
        Assert.Equal("PerDayValue", value3.MatchedLabel!.Name);
        
        var value4 = section4Sub1.SubResults[4];
        Assert.Equal("32850000", value4.Text![0].Text);
        Assert.Equal("PerYearValue", value4.MatchedLabel!.Name);
        
        var agreedSchemaLicenceGroup = SchemaConverter.ToLicenceGroup(resultFull);
        Assert.Single(agreedSchemaLicenceGroup.Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Equal(filename, agreedSchemaLicence.Filename);
        Assert.Equal("2/26/32/126", agreedSchemaLicence.LicenceNumber);
        Assert.StartsWith("YORKSHIRE W", agreedSchemaLicence.LicenceHolder);
        Assert.Equal(new DateTime(2005, 07, 20), agreedSchemaLicence.LicenceVersion.IssueDate);
        Assert.Equal(new DateTime(1966, 01, 27), agreedSchemaLicence.LicenceVersion.OriginalIssueDate);
        Assert.Equal(new DateTime(2005, 02, 02), agreedSchemaLicence.LicenceVersion.EffectiveDate);
        Assert.Null(agreedSchemaLicence.LicenceVersion.ExpiryDate);
        Assert.Equal("22632126-LV20050202", agreedSchemaLicence.Id);
        Assert.Equal("LV20050202", agreedSchemaLicence.LicenceVersion.LicenceVersionId);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates);
        
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits.Length);
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits[0].Units);        
        Assert.Equal(100000, agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits[1].Units);        
        Assert.Equal(32850000, agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits[1].Value);
        
        Assert.Equal(8, agreedSchemaLicence.AbstractionLimits.Individual.Length);
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Individual[0].Units);
        Assert.Equal(LimitPeriodType.PerDay, agreedSchemaLicence.AbstractionLimits.Individual[0].PeriodType);
        Assert.Equal(45460.92, agreedSchemaLicence.AbstractionLimits.Individual[0].Value);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual[0].Points!);
        Assert.Equal("(1)", agreedSchemaLicence.AbstractionLimits.Individual[0].Points![0].Id);
        
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Individual[1].Units);
        Assert.Equal(13638276, agreedSchemaLicence.AbstractionLimits.Individual[1].Value);
        Assert.Equal(LimitPeriodType.PerYear, agreedSchemaLicence.AbstractionLimits.Individual[1].PeriodType);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual[1].Points!);
        Assert.Equal("(1)", agreedSchemaLicence.AbstractionLimits.Individual[1].Points![0].Id);
        
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Individual[2].Units);
        Assert.Equal(68191, agreedSchemaLicence.AbstractionLimits.Individual[2].Value);
        Assert.Equal(LimitPeriodType.PerDay, agreedSchemaLicence.AbstractionLimits.Individual[2].PeriodType);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual[2].Points!);
        Assert.Equal("(2)", agreedSchemaLicence.AbstractionLimits.Individual[2].Points![0].Id);
        
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Individual[3].Units);
        Assert.Equal(18184368, agreedSchemaLicence.AbstractionLimits.Individual[3].Value);
        Assert.Equal(LimitPeriodType.PerYear, agreedSchemaLicence.AbstractionLimits.Individual[3].PeriodType);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual[3].Points!);
        Assert.Equal("(2)", agreedSchemaLicence.AbstractionLimits.Individual[3].Points![0].Id);
        
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Individual[4].Units);
        Assert.Equal(45461, agreedSchemaLicence.AbstractionLimits.Individual[4].Value);
        Assert.Equal(LimitPeriodType.PerDay, agreedSchemaLicence.AbstractionLimits.Individual[4].PeriodType);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual[4].Points!);
        Assert.Equal("(3)", agreedSchemaLicence.AbstractionLimits.Individual[4].Points![0].Id);
        
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Individual[5].Units);
        Assert.Equal(16593236, agreedSchemaLicence.AbstractionLimits.Individual[5].Value);
        Assert.Equal(LimitPeriodType.PerYear, agreedSchemaLicence.AbstractionLimits.Individual[5].PeriodType);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual[5].Points!);
        Assert.Equal("(3)", agreedSchemaLicence.AbstractionLimits.Individual[5].Points![0].Id);
        
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Individual[6].Units);
        Assert.Equal(15911, agreedSchemaLicence.AbstractionLimits.Individual[6].Value);
        Assert.Equal(LimitPeriodType.PerDay, agreedSchemaLicence.AbstractionLimits.Individual[6].PeriodType);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual[6].Points!);
        Assert.Equal("(4)", agreedSchemaLicence.AbstractionLimits.Individual[6].Points![0].Id);
        
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Individual[7].Units);
        Assert.Equal(5819000, agreedSchemaLicence.AbstractionLimits.Individual[7].Value);
        Assert.Equal(LimitPeriodType.PerYear, agreedSchemaLicence.AbstractionLimits.Individual[7].PeriodType);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual[7].Points!);
        Assert.Equal("(4)", agreedSchemaLicence.AbstractionLimits.Individual[7].Points![0].Id);
        
        Assert.Equal(4, agreedSchemaLicence.Points.Length);
        Assert.Equal("(1)", agreedSchemaLicence.Points[0].Id);
        Assert.Equal("TA 0417 2942", agreedSchemaLicence.Points[0].Description);
        Assert.Equal("(2)", agreedSchemaLicence.Points[1].Id);
        Assert.Equal("TA 0472 3425", agreedSchemaLicence.Points[1].Description);
        Assert.Equal("(3)", agreedSchemaLicence.Points[2].Id);
        Assert.Equal("TA 0677 3514", agreedSchemaLicence.Points[2].Description); // TODO should be TA 0677 3514 & TA 0678 3508
        Assert.Equal("(4)", agreedSchemaLicence.Points[3].Id);
        Assert.Equal("TA 0269 3303 & TA 0268 3302 marked \"A\", \"B\", \"C\", \"D\", \"E and \"F\" on the map", // TODO E should have the quotes around it
            agreedSchemaLicence.Points[3].Description);
        
        // TODO 1 for each point
        
        Assert.Single(agreedSchemaLicence.Purposes);
        // TODO filling in purpose
    }
    
    [Fact]
    public async Task When_YorkshireWaterCompany2_ThenY()
    {
        // Arrange
        const string filename = "2-27-29-012 7003124.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        
        var licenceNumberResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("27/29/12", licenceNumberResult.Text?.FirstOrDefault()?.Text); // TODO should be 2/27/29/12
        
        var company = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Equal("SCARBOROUGH CORPORATION", company?.Text?.Single().Text);
        
        var abstractionLimitsResult = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        //Assert.Equal(5, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults[0];
        
        Assert.Equal(4, section1Sub1.SubResults.Count);
        
        var perDayUnits = section1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("million gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("4.5", perDayValue?.Text?.FirstOrDefault()?.Text);
        
        var perYearUnits = section1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("million gallons", perYearUnits?.Text?.FirstOrDefault()?.Text);

        var perYearValue = section1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("1721", perYearValue?.Text?.FirstOrDefault()?.Text); // TODO should be 1721
        
        // TODO - Should have 2 per day entries
        
        var points = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        Assert.Equal(3, points.Text!.Count); // TODO should be 5
        Assert.Equal("Source of supply and authorised place(s) of abstraction", points.Text![0].Text);
        //Assert.StartsWith("Delete the existing", points.Text![1].Text);
        //Assert.Equal("the following :", points.Text![2].Text); // TODO work out what is hapening here
        Assert.Equal("NZ 886 088 River Esk at Ruswarp", points.Text![1].Text);
        Assert.Equal("NZ 873 082 River Esk at Briggswath", points.Text![2].Text);

        var pointPurposeGroup = points.SubResults.Single();
        Assert.NotNull(pointPurposeGroup);

        var pointsSubs = pointPurposeGroup.SubResults;
        
        Assert.Equal(2, pointsSubs.Count);
        
        var purpose = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Purpose");
        Assert.NotNull(purpose);
        
        Assert.Equal(2, purpose.Text!.Count);
        
        var agreedSchemaLicenceGroup = SchemaConverter.ToLicenceGroup(resultFull);
        Assert.Single(agreedSchemaLicenceGroup.Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Equal("27/29/12", agreedSchemaLicence.LicenceNumber);        
        Assert.Equal(filename, agreedSchemaLicence.Filename);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits);
        Assert.Empty(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual.Length); // TODO should be 3
        
//        Assert.Equal("2/27/29/12", agreedSchemaLicence.LicenceNumber);
 //       Assert.Equal("Lakeminster Park Limited", agreedSchemaLicence.LicenceHolder);
  //      Assert.Equal(new DateTime(2012, 08, 16), agreedSchemaLicence.LicenceVersion.IssueDate);
     //   Assert.Equal(new DateTime(1993, 06, 23), agreedSchemaLicence.LicenceVersion.OriginalIssueDate);
 //       Assert.Equal(new DateTime(2012, 08, 16), agreedSchemaLicence.LicenceVersion.EffectiveDate);
        Assert.Equal("272912-LVUNKNOWN", agreedSchemaLicence.Id);
        Assert.Equal("LVUNKNOWN", agreedSchemaLicence.LicenceVersion.LicenceVersionId);

        Assert.Equal(2, agreedSchemaLicence.Points.Length);
        Assert.Single(agreedSchemaLicence.Purposes);
    }
}