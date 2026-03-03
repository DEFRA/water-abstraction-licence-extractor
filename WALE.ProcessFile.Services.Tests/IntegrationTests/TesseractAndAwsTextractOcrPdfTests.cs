using Meziantou.Xunit;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Tesseract;
using WALE.ProcessFile.Services.Tests.Helper;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

// These tests are slow as we are limited to one scan per second from AWS Textract by default
[Collection("AWS Textract 2")]
[EnableParallelization]
public class TesseractAndAwsTextractOcrPdfTests(SingletonAwsTextractFixture textractFixture)
    : IClassFixture<SingletonAwsTextractFixture>
{
    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider =
        new(TestConfig.PostgresHost,
            TestConfig.PostgresPort,
            TestConfig.PostgresDbName,
            TestConfig.PostgresUsername,
            TestConfig.PostgresPassword);
    
    private static IDatabaseReadService ReadService =>
        new PostgresReadService(NpgsqlDataSourceProvider);

    private static readonly ICacheService DatabaseCacheService =
        new DatabaseCacheService(ReadService, null!);
    
    private Task SetupLicenceNumbersAsync(short regionCode)
    {
        return textractFixture.SetupLicenceNumbersAsync(regionCode, DatabaseCacheService);
    }

    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    
    private readonly IPdfDataExtractorService _pdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, PageSegMode.SparseTextOsd, CacheService, OutputService, TestConfig.DotnetPath, TestConfig.TesseractExeName, TestConfig.TesseractExeDirectory),
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, PageSegMode.Auto, CacheService, OutputService, TestConfig.DotnetPath, TestConfig.TesseractExeName, TestConfig.TesseractExeDirectory),
            textractFixture.Instance
        },
        CacheService,
        OutputService,
        DocumentService,
        TestConfig.PdfFolder);
    
    private readonly IPdfDataExtractorService _pdfDataExtractor3 = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, PageSegMode.SparseTextOsd, CacheService, OutputService, TestConfig.DotnetPath, TestConfig.TesseractExeName, TestConfig.TesseractExeDirectory),
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, PageSegMode.Auto, CacheService, OutputService, TestConfig.DotnetPath, TestConfig.TesseractExeName, TestConfig.TesseractExeDirectory),
            textractFixture.Instance
        },
        CacheService,
        OutputService,
        DocumentService,
        TestConfig.PdfFolder3);

    private readonly Dictionary<string, DmsFileData> _fileLicenceMapping = new() {{"", new DmsFileData()}};    
    private readonly NaldLicenceStatusData _naldLicenceStatusData = new()
    {
        LiveLicences = [],
        DeadLicences = [],
        ImpoundmentLicences = []
    };
    
    private readonly Dictionary<string, List<NaldData>> _naldData = [];

    private async Task<LookupConfiguration> LookupConfigurationAsync()
    {
        return new(
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            await textractFixture.FirstNamesCsvTask(),
            3);
    }

    private async Task<MatchesResult> GetMatchesAsync(string fileName, int useExtractor = 1)
    {
        var pdfExtractor = useExtractor == 1 ? _pdfDataExtractor : _pdfDataExtractor3;
        var folder = useExtractor == 1 ? TestConfig.PdfFolder : TestConfig.PdfFolder3;
        
        return await pdfExtractor.GetMatchesAsync(
            folder + fileName,
            await LookupConfigurationAsync(),
            [folder + fileName],
            0);
    }
    
    [Fact]
    public async Task GetSomeFromTesseractAndSomeFromAwsTextract_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
        const string filename = "Non-Application Licence Document (08.06.1987).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("9th day of January, 1967", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("H.H. Henderson & C. Wentworth-Stanley", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel!.Position);
        Assert.Equal(MatchedPosition.OnOrNearPreviousLine, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(9, abstractionLimitsResult.Text?.Count);
        
        Assert.Single(abstractionLimitsResult!.SubResults!);

        var abstractionPoint1 = abstractionLimitsResult!.SubResults![0];
        Assert.NotNull(abstractionPoint1);
        Assert.Equal(9, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);
        
        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        
        Assert.Equal(4, section1Sub1.SubResults.Count);
        // TODO fix for this
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("5600", perDayValue?.Text?.FirstOrDefault()?.Text); // This is better then Azure AI Vision

        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("1500", perHourValue?.Text?.FirstOrDefault()?.Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/22/271", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractor,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync());
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenIsOldCrossedOut_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
        const string filename = "Licence - Old 6082700.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(9, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Mersey and Weaver River Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("third day of April, 19 70", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.NotNull(nameResult); // Should be, WARRINGTON, RUNCORN AND DISTRICT WATER BOARD" - Is crossed out but Azure AI can read it
        Assert.Equal("WARRINGTON RUNCORN AND DISTRICT WATER BOARD", nameResult.Text!.First().Text);
        
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
        
        Assert.Equal(12, section1Sub1.SubResults!.Count);
        
        // NOTE - it does a bad job getting these subresults
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25/68/3/91", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractor,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync());
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("25/68/5/9", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal("25/69/3/91", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Equal("25/68/3/76", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
    }
    
    [Fact]
    public async Task Handsigned_WhenNearPreviousLineIsCompany_ThenFoundCorrect_Ish()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
        const string filename = "Non-Application Licence Document (22.09.1986).PDF";
        
        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        //Assert.Equal(6, GeneralHelper.ExcludeGeneralList(resultList).Count);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("22ND DAY OF SEPTEMBER 1986", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var points = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        Assert.StartsWith("22ND DAY OF SEPTEMBER 1986", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResult); // AzureAi finds this - AWS finds 'Fams' (with 88% confidence and doesnt find word 'Ltd')
        
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
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractor,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync());
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("11/42/28.2/49", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
    }
    
    [Theory]
    [InlineData("12100004__Application Transfer Issued Licence - [1982] - (1982).pdf", "7 DAY OF OCTOBER 19 82", "07/10/1982", 4, 0, "1/21/00/004")]
    [InlineData("12100052__Application Formal Variation Issued Licence - [1987] - (1987).pdf", "2nd day of JUNE, 1967", "02/06/1967", 6, 0, "1/21/00/052")]
    [InlineData("12100065__Application New Licence Issued - [1974] - (1974).pdf", "21st day of March 1974", "21/03/1974", 6, 0, "1/21/00/065")]
    [InlineData("12201014__Application New Licence Issued - [1966] - (1966).pdf", "27th day of JULY, 19 66", "27/07/1966", 7, 0, "1/22/01/014")]
    [InlineData("12201021__Application New Licence Issued - [1966] - (1966).pdf", "28th day of JULY, 19 6g", "28/07/1966", 6, 0, "1/22/01/021")]
    [InlineData("12201023__Application New Licence Issued - [1966] - (1966).pdf", "28th day of JULY, 19 66", "28/07/1966", 6, 0, "1/22/01/023")]
    [InlineData("12202043__abstraction license 1975.pdf", "14th day of February 1975", "14/02/1975", 6, 0, "1/22/02/043")]
    [InlineData("12203007__1-22-03-007 5822413.PDF", "9th day of MARCH, 1986", "09/03/1986", 6, 0, "1/22/03/007")]
    [InlineData("12203045__Non-Application Licence Document [Original licence] (23051966).PDF", "23rd day of MAY, 19 66", "23/05/1966", 7, 0, "1/22/03/045")]
    [InlineData("12203120__1-22-03-120 5822437.PDF", "6 September 2006", "06/09/2006", 11, 0, "1/22/03/120")]
    [InlineData("12205021__Original Licence 5684532.pdf", "5 DAY OF april 19 82", "05/04/1982", 6, 0, "1/22/05/021")]
    [InlineData("12205044__Non-Application Licence Document [Original Licence] (14101966).pdf", "14IEH day of OCTOBER, 1966", "14/10/1966", 5, 0, "1/22/05/044")]
    [InlineData("12301067__Application New Licence Issued - [1966] - (01081966).pdf", "1st day of AUGUST, 19 66", "01/08/1966", 7, 0, "1/23/01/067")]
    [InlineData("12302006__Licence Document 10031966.pdf", "10TH day of MARCH, 1966", "10/03/1966", 6, 0, "1/23/02/006")]
    [InlineData("12302044__Non-Application Licence Document [Original Licence] (27.05.1966).PDF", "27th day of MAY, 1966", "27/05/1966", 7, 0, "1/23/02/044")]
    [InlineData("12302207__1-23-02-207 5822808.PDF", "29th day of June 1976", "29/06/1976", 5, 0, "1/23/02/207")]
    [InlineData("12303008__Non-Application Licence Document [Original Licence] (11051966).PDF", "11th day of MAY, 19 66", "11/05/1966", 6, 0, "1/23/03/008")]
    [InlineData("12303075__Non-Application Licence Document [Original Licence] (08111966).PDF", "8th day of NOVEMBER, 19 66", "08/11/1966", 7, 0, "1/23/03/075")]
    [InlineData("12202009__Application New Licence 1-22-02-009 5822403.PDF", "13th day of MARCH, 1967", "13/03/1967", 7, 0, "1/22/02/009")]
    [InlineData("12303142__Application - Formal Variation - Issued Licence 27.07.2016 9431557.pdf", "27 July 2016", "27/07/2016", 14, 0, "1/23/03/142")]
    [InlineData("12405035__Permit to Abstract - 1_24_5_35 - Licence Document - 10031966.pdf", "10th day of MARCH, 19 66K", "10/03/1966", 5, 0, "1/24/05/035")] // TODO the K shouldnt be there
    [InlineData("12502014__Non-Application Licence Document (20.07.2005).PDF", "2.0 JUL 2005", "20/07/2005", 13, 0, "1/25/02/014")]
    [InlineData("12502032__Non-Application Licence Document [Licence] (16052000).PDF", "16/5/00", "16/05/2000", 13, 0, "1/25/02/032")]
    [InlineData("12502102__Non-Application Licence Document [Original Licence] (27042001).PDF", "3/7/01", "03/07/2001", 13, 0, "1/25/02/102")]
    [InlineData("12502133__Non-Application Licence Document [Licence] (06051998).PDF", "13.5.98", "13/05/1998", 12, 0, "1/25/02/133")]
    [InlineData("12502141__Application type unknown Licence Issued (08.11.2005).PDF", "8 NOV 2005", "08/11/2005", 14, 0, "1/25/02/141")]
    [InlineData("12504120__Abstraction licence.PDF", "28/4/99", "28/04/1999", 12, 0, "1/25/04/120")] // TODO looks a bit wrong
    [InlineData("12401034__1-24-01-034 6099401.pdf", "28th day of May, 1969", "28/05/1969", 6, 0, "1/24/01/034")]
    [InlineData("12502023__Application type unknown Licence Issued 03.05.1966.pdf", "3rd day of MAY, 19 666", "03/05/1966", 7, 0, "1/25/02/023")]
    [InlineData("22712270__Non-Application Licence Document (29.07.2003).PDF", "29th July 03", "29/07/2003", 14, 0, "2/27/12/270")]
    [InlineData("22709167__Non-Application Licence Document (27.03.1997).PDF", "27 MAR 1897", "27/03/1897", 11, 0, "2/27/09/167")]
    [InlineData("12506023__Application type unknown Licence Issued (26.01.2006).PDF", "26 JAN 2006", "26/01/2006", 15, 0, "1/25/06/023")] // Should be 2000 but impossible to tell in file, so fine
    [InlineData("22712298__Non-Application Licence Document (27.03.1991).PDF", "2715 day of Marl 1991", "27/03/1991", 5, 0, "2/27/12/298")]
    [InlineData("22709141__Non-Application Licence Document (09.08.1990).PDF", "9th day of Aug 1990", "09/08/1990", 4, 0, "2/27/09/141")]
    [InlineData("12304001__1-23-04-001 Licence Issued - 07031966.PDF", "7th day of MARCH, 19 66", "07/03/1966", 6, 0, "1/23/04/001")]
    //12504178R01__Application type unknown Licence Issued (01.05.2007).pdf, "299 July'03", // Stamp is incredibly faint, Tesseract doesnt read - Azure AI reads it wrong
    //22630110__Issued licence- 2-26-30-110 6075592.PDF, "299 July'03" // Skips word 'issue' in Azure AI frustratingly
    //12201021__Application New Licence Issued - [1966] - (1966).pdf, "28th day of July 1966" // Doesn't read JULY frustratingly
    public async Task When1_ThenIssueDateCorrectly(
        string filename,
        string expectedIssueDate,
        string expectedIssueDate2,
        int expectedResults,
        int expectedLinkedLicenceCount,
        string? expectedLicenceNumber)
    {
        // Act
        await SetupLicenceNumbersAsync(3);
        
        var resultFull = await GetMatchesAsync(filename, 3);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(expectedResults, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.Equal(expectedIssueDate, dateOfIssue.Text!.First().Text);
        
        var schemaData = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            [],
            _naldLicenceStatusData,
            [],
            _pdfDataExtractor3,
            TestConfig.PdfFolder3,
            0,
            await LookupConfigurationAsync());

        var licence = schemaData[0].Licences[0];
        Assert.Equal(expectedLicenceNumber, licence.LicenceNumber?.Value);

        Assert.NotNull(licence.LicenceVersion.IssueDate);
        Assert.Equal(expectedIssueDate2, licence.LicenceVersion.IssueDate!.Value.ToShortDateString());
        
        Assert.Equal(expectedLinkedLicenceCount, licence.LinkedLicences.Length);
    }
    
    [Fact]
    public async Task AAA3_B4_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
        const string filename = "12203045__Non-Application Licence Document [Original licence] (23051967).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(7, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Northumbrian River Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.Equal("23rd day of MAY, 19 66", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, licenceNumberResult.MatchedLabel!.Position);        
        Assert.Equal("1/22/3/45", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractor3,
            TestConfig.PdfFolder3,
            0,
            await LookupConfigurationAsync());
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Equal("12203045-LVUNKNOWN", agreedSchemaLicenceGroup[0].LicenceSetId);
        Assert.Equal("045", agreedSchemaLicenceGroup[0].ShortLicenceSetId);
        
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
        Assert.Equal(new DateTime(1966, 05, 23), agreedSchemaLicence.LicenceVersion.IssueDate);
    }
}