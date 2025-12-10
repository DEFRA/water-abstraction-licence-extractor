using Tesseract;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using MatchType = WALE.ProcessFile.Core.Enums.MatchType;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

public class TesseractAndAzureAiVisionOcrPdfTests
{
    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    
    private readonly IPdfDataExtractorService _pdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, PageSegMode.SparseTextOsd, CacheService, OutputService),
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, PageSegMode.Auto, CacheService, OutputService),
            new AzureAiVisionOcrDataExtractorService(
                TestConfig.AiVisionEndpoint,
                TestConfig.AiVisionKey,
                CacheService,
                OutputService),
        },
        CacheService,
        OutputService,
        TestConfig.PdfFolder);
    
    private readonly IPdfDataExtractorService _pdfDataExtractor3 = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, PageSegMode.SparseTextOsd, CacheService, OutputService),
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, PageSegMode.Auto, CacheService, OutputService),
            new AzureAiVisionOcrDataExtractorService(
                TestConfig.AiVisionEndpoint,
                TestConfig.AiVisionKey,
                CacheService,
                OutputService),
        },
        CacheService,
        OutputService,
        TestConfig.PdfFolder3);

    private readonly Dictionary<string, string> _fileLicenceMapping = new() {{"", ""}};    
    private readonly HashSet<string> _liveLicenceNumbers = [];
    private readonly HashSet<string> _deadLicenceNumbers = [];
    private readonly HashSet<string> _impoundmentLicenceNumbers = [];
    
    private static string PdfFolder => TestConfig.PdfFolder;
    
    private Task<MatchesResult> GetMatchesAsync(string fileName, int useExtractor = 1)
    {
        var pdfExtractor = useExtractor == 1 ? _pdfDataExtractor : _pdfDataExtractor3;
        var folder = useExtractor == 1 ? TestConfig.PdfFolder : TestConfig.PdfFolder3;
        
        return pdfExtractor.GetMatchesAsync(
            folder + fileName,
            new LookupConfiguration(
                LabelConfiguration.GetLabels(),
                _fileLicenceMapping),
            [folder + fileName],
            0);
    }
    
    private static List<LabelGroupResult> ExcludeGeneralList(List<LabelGroupResult> matches)
    {
        return matches.Where(m => m.LabelGroupName != "LinkedLicenceNumber").ToList();
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
        Assert.Equal(9, ExcludeGeneralList(resultList).Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Mersey and Weaver River Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("third day of April, 19 70", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.NotNull(nameResult);
        // Is crossed out but Azure AI can read it
        Assert.Equal("WARRINGTON, RUNCORN AND DISTRICT WATER BOARD", nameResult.Text?.First().Text);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.NotNull(abstractionLimitsResult); // Is crossed out but Azure AI can read it
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);
        
        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);
        
        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        
        Assert.Equal(13, section1Sub1.SubResults!.Count);

        var pointName = section1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel?.Name == "PointCondition")?.Text!.First().Text;
        
        Assert.Equal("(1)", pointName);
        
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
        
        perYearUnits = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("megalitres", perYearUnits?.Text?.FirstOrDefault()?.Text);

        perYearValue = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("300", perYearValue?.Text?.FirstOrDefault()?.Text); // TODO should be 1364
        
        perDayUnits = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("megalitres", perDayUnits?.Text?.FirstOrDefault()?.Text);

        perDayValue = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("1.25", perDayValue?.Text?.FirstOrDefault()?.Text); // TODO should be 5.7

        perHourUnits = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("cubic metres", perHourUnits?.Text?.FirstOrDefault()?.Text);

        perHourValue = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("52", perHourValue?.Text?.FirstOrDefault()?.Text); // TODO should be 236

        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25/68/3/91/", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractor,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
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
        Assert.Equal(8, ExcludeGeneralList(resultList).Count);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("22ND DAY OF SEPTEMBER 1986", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal(11, nameResult.LineNumber);
        // NOTE - According to companies house this is actually H.N. BUTLER FARMS LIMITED        
        Assert.Equal("H. W. Butter Farms Ltd", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("( hereinafter referred to as \"The Licence Holder\" )", nameResult.MatchedLabel!.Text?.Select(x => x.Text)!);
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
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractor,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Theory]
    [InlineData("12100004__Application Transfer Issued Licence - [1982] - (1982).pdf", "7 DAY OF OCTOBER 19 82", "07/10/1982", 4, 0, 1)]
    [InlineData("12100052__Application Formal Variation Issued Licence - [1987] - (1987).pdf", "2nd day of JUNE, 19 62", "02/06/1962", 5, 0, 1)]
    [InlineData("12100065__Application New Licence Issued - [1974] - (1974).pdf", "21st day of March 1974", "21/03/1974", 7, 0, 1)]
    [InlineData("12201014__Application New Licence Issued - [1966] - (1966).pdf", "27th day of JULY, 19 66", "27/07/1966", 7, 0, 1)]
    [InlineData("12201021__Application New Licence Issued - [1966] - (1966).pdf", "28th day of JULY, 19 6g", "28/07/1966", 6, 0, 1)]
    [InlineData("12201023__Application New Licence Issued - [1966] - (1966).pdf", "28th day of JULY, 19 66", "28/07/1966", 7, 0, 1)]
    [InlineData("12202043__abstraction license 1975.pdf", "14th day of February 1575", "14/02/1975", 6, 0, 1)]
    [InlineData("12203007__1-22-03-007 5822413.PDF", "9th day of MARCH, 1986", "09/03/1986", 6, 1, 1)]
    [InlineData("12203045__Non-Application Licence Document [Original licence] (23051966).PDF", "2 3rd day of MAY, 19 66", "23/05/1966", 7, 0, 1)]
    [InlineData("12203120__1-22-03-120 5822437.PDF", "6 September 2006", "06/09/2006", 11, 0, 1)]
    [InlineData("12205021__Original Licence 5684532.pdf", "5 DAY OF april 19 82", "05/04/1982", 6, 1, 1)]
    [InlineData("12205044__Non-Application Licence Document [Original Licence] (14101966).pdf", "14IEH day of OCTOBER, 1966", "14/10/1966", 5, 1, 1)]
    [InlineData("12301067__Application New Licence Issued - [1966] - (01081966).pdf", "1st day of AUGUST , 19 66", "01/08/1966", 6, 0, 1)]
    [InlineData("12302006__Licence Document 10031966.pdf", "day of MARCH, 1966", "01/03/1966", 6, 0, 1)]
    [InlineData("12302044__Non-Application Licence Document [Original Licence] (27.05.1966).PDF", "27th day of MAY 1966", "27/05/1966", 7, 0, 1)]
    [InlineData("12302207__1-23-02-207 5822808.PDF", "29th day of June 1976", "29/06/1976", 6, 0, 1)]
    [InlineData("12303008__Non-Application Licence Document [Original Licence] (11051966).PDF", "11 th day of NAY, 19 66", "11/05/1966", 6, 0, 1)]
    [InlineData("12303075__Non-Application Licence Document [Original Licence] (08111966).PDF", "8th day of NOVEMBER, 19 66", "08/11/1966", 7, 0, 1)]
    [InlineData("12202009__Application New Licence 1-22-02-009 5822403.PDF", "13th day of MARCH, 1967:", "13/03/1967", 7, 0, 1)]
    [InlineData("12303142__Application - Formal Variation - Issued Licence 27.07.2016 9431557.pdf", "27 July 2016", "27/07/2016", 14, 0, 1)]
    [InlineData("12405035__Permit to Abstract - 1_24_5_35 - Licence Document - 10031966.pdf", "10th day of MARCH 19 66", "10/03/1966", 5, 1, 1)]
    [InlineData("12502014__Non-Application Licence Document (20.07.2005).PDF", "2.0 JUL 2005", "20/07/2005", 13, 0, 1)]
    [InlineData("12502032__Non-Application Licence Document [Licence] (16052000).PDF", "16/5/00", "16/05/2000", 13, 0, 1)]
    [InlineData("12502102__Non-Application Licence Document [Original Licence] (27042001).PDF", "3/7/01", "03/07/2001", 13, 0, 1)]
    [InlineData("12502133__Non-Application Licence Document [Licence] (06051998).PDF", "13.5.98", "13/05/1998", 13, 0, 1)]
    [InlineData("12502141__Application type unknown Licence Issued (08.11.2005).PDF", "8 NOV 2005", "08/11/2005", 12, 0, 1)]
    [InlineData("12504120__Abstraction licence.PDF", "28/. 4/14", "28/04/2014", 13, 0, 1)]
    [InlineData("12401034__1-24-01-034 6099401.pdf", "28th dey of Hay, 1969", "28/05/1969", 6, 0, 1)]
    [InlineData("12502023__Application type unknown Licence Issued 03.05.1966.pdf", "3rd day of MAY, 19 66", "03/05/1966", 4, 0, 1)]
    [InlineData("22712270__Non-Application Licence Document (29.07.2003).PDF", "299 July'03", "29/07/2003", 14, 0, 1)]
    [InlineData("22709167__Non-Application Licence Document (27.03.1997).PDF", "2.7. MAR.1897", "27/03/1897", 12, 0, 1)]
    [InlineData("12506023__Application type unknown Licence Issued (26.01.2006).PDF", "26 JAN 2050", "26/01/2050", 13, 1, 2)] // Should be 2000 but impossible to tell in file, so fine
    [InlineData("22712298__Non-Application Licence Document (27.03.1991).PDF", "2715 day of Marl 1991", "27/03/1991", 5, 1, 1)]
    [InlineData("22709141__Non-Application Licence Document (09.08.1990).PDF", "9Th day of August 1990", "09/08/1990", 5, 0, 1)]
    [InlineData("12304001__1-23-04-001 Licence Issued - 07031966.PDF", "7th day of MARCH .19 66", "07/03/1966", 5, 1, 1)]
    //12504178R01__Application type unknown Licence Issued (01.05.2007).pdf, "299 July'03", // Stamp is incredibly faint, Tesseract doesnt read - Azure AI reads it wrong
    //22630110__Issued licence- 2-26-30-110 6075592.PDF, "299 July'03" // Skips word 'issue' in Azure AI frustratingly
    //12201021__Application New Licence Issued - [1966] - (1966).pdf, "28th day of July 1966" // Doesn't read JULY frustratingly
    public async Task When1_ThenIssueDateCorrectly(
        string filename,
        string expectedIssueDate,
        string expectedIssueDate2,
        int expectedResults,
        int expectedLinkedLicences,
        int expectedLicenceGroups)
    {
        // Act
        var resultFull = await GetMatchesAsync(filename, 3);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(expectedResults, ExcludeGeneralList(resultList).Count);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.Equal(expectedIssueDate, dateOfIssue.Text!.First().Text);
        
        var schemaData = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            [],
            [],
            [],
            [],
            _pdfDataExtractor3,
            TestConfig.PdfFolder3,
            0);

        var licence = schemaData[0].Licences[0];

        Assert.NotNull(licence.LicenceVersion.IssueDate);
        Assert.Equal(expectedIssueDate2, licence.LicenceVersion.IssueDate!.Value.ToShortDateString());
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractor3,
            TestConfig.PdfFolder,
            0);
        
        Assert.Equal(expectedLicenceGroups, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal(expectedLinkedLicences, agreedSchemaLicence.LinkedLicences.Length);
    }
    
    [Theory]
    [InlineData("22702013__2-27-02-013 6999981.PDF", "16 June 2000", "16/06/2000", 13, 0, "2/27/02/013")] // Correct
    [InlineData("22632370__2-26-32-370 6937616.PDF", "9 February 2004", "09/02/2004", 14, 1, "2/26/32/370")] // Correct
    [InlineData("22706035__2-27-06-035 6957806.PDF", "9 FEBRUARY 2004", "09/02/2004", 14, 0, "2/27/06/035")] // Correct
    [InlineData("22707039__Application New Licence Issued - [21.01.2008] - (21.01.2008).PDF", "0 1 OCT 2002", "01/10/2002", 12, 0, "2/27/07/039")] // Correct
    [InlineData("12506023__Application type unknown Licence Issued (26.01.2006).PDF", "26 JAN 2050", "26/01/2050", 13, 1, "1/25/06/023")] // Year incorrect - faint stamp, can't even read as a human
    [InlineData("22634080__Non-Application Licence Document (27.03.1997).PDF", "27 MAR 1997", "27/03/1997", 12, 0, "2/26/34/080")] // Correct
    [InlineData("22709167__Non-Application Licence Document (27.03.1997).PDF", "2.7. MAR.1897", "27/03/1897", 12, 0, "2/27/09/167")] // Incorrect - stamp is not amazing
    [InlineData("22715238__Non-Application Licence Document (05.03.2004).PDF", "5 MAR 2004", "05/03/2004", 14, 0, "2/27/15/238")] // Correct (I think - there is '-' in the stamp)
    public async Task WhenHarishSpottedNoIssueDateFiles1_ThenIssueDateCorrectly(
        string filename,
        string expectedIssueDate,
        string expectedIssueDate2,
        int expectedResults,
        int expectedLinkedLicenceCount,
        string? expectedLicenceNumber)
    {
        // Act
        var resultFull = await GetMatchesAsync(filename, 3);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(expectedResults, ExcludeGeneralList(resultList).Count);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.Equal(expectedIssueDate, dateOfIssue.Text!.First().Text);
        
        var schemaData = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            [],
            [],
            [],
            [],
            _pdfDataExtractor3,
            TestConfig.PdfFolder3,
            0);

        var licence = schemaData[0].Licences[0];

        Assert.NotNull(licence.LicenceVersion.IssueDate);
        Assert.Equal(expectedIssueDate2, licence.LicenceVersion.IssueDate!.Value.ToShortDateString());
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractor3,
            TestConfig.PdfFolder3,
            0);
        
        Assert.NotNull(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal(expectedLicenceNumber, agreedSchemaLicence.LicenceNumber);
        
        Assert.Equal(expectedLinkedLicenceCount, agreedSchemaLicence.LinkedLicences.Length);
    }
    
    [Fact(Skip = "ProblemsWithCarbonPaper")]
    public async Task GetSomeFromTesseractAndSomeFromAzureAi_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Non-Application Licence Document (08.06.1987).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(6, ExcludeGeneralList(resultList).Count);

        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("9th day of January, 1967", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
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
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);
        
        Assert.Single(abstractionLimitsResult!.SubResults!);

        var abstractionPoint1 = abstractionLimitsResult!.SubResults![0];
        Assert.NotNull(abstractionPoint1);
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);
        
        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        
        Assert.Equal(2, section1Sub1.SubResults.Count);
        // TODO fix for this
        
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
    public async Task WhenZ_B()
    {
        // Arrange
        const string filename = "22630082__Application - New - Issued Licence 12.12.08 10739186.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3);
        Assert.Equal(12, ExcludeGeneralList(resultFull.Matches!).Count);
        
        var licenceSets = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractor3,
            TestConfig.PdfFolder3,
            -1);
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("22630082-LV20081212", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("2/26/30/082", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"].ToString());
        
        Assert.Null(agreedSchemaLicence.DefinitionOfYear);
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        
        Assert.Equal("1.43M3/", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
    }
}