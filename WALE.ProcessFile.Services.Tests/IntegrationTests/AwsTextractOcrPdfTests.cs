using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tests.Helper;
using MatchType = WALE.ProcessFile.Core.Enums.MatchType;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

[Collection("AWS Textract 1")]
public class AwsTextractOcrPdfTests(SingletonAwsTextractFixture textractFixture)
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

    private readonly IPdfDataExtractorService _pdfDataExtractor1 = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
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
            textractFixture.Instance
        },
        CacheService,
        OutputService,
        DocumentService,
        TestConfig.PdfFolder3);

    private readonly Dictionary<string, DmsFileData> _fileLicenceMapping = new() { { "", new DmsFileData() } };

    private async Task<LookupConfiguration> LookupConfigurationAsync(int regionCode)
    {
        return new LookupConfiguration(
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            await textractFixture.FirstNamesCsvTask(),
            regionCode);
    }

    private async Task<MatchesResult> GetMatchesAsync(string fileName, int regionCode, int number = 1)
    {
        var pdfFolder = number == 1 ? TestConfig.PdfFolder : TestConfig.PdfFolder3;
        var pdfService = number == 1 ? _pdfDataExtractor1 : _pdfDataExtractor3;
        
        return await pdfService.GetMatchesAsync(
            pdfFolder + fileName,
            await LookupConfigurationAsync(regionCode),
            [pdfFolder + fileName],
            0);
    }
    
    [Fact]
    public async Task WhenA_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "14460030853 licence effective 24.07.2005.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(12, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(8, records.Text!.Count);

        var points = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);

        var licenceNumber = resultList.Single(result => result.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("14/46/03/0853", licenceNumber.Text?.FirstOrDefault()?.Text); // NOTE - Tesseract gets this wrong

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);

        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");

        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr T MC Davey", nameResult.Text?[0]?.Text);
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

        var linkedLicences = section1Sub1.SubResults.Where(x => x.MatchedLabel?.Name == "LinkedLicenceNumber").ToList();
        Assert.Single(linkedLicences);
        Assert.Equal("14/46/03/0852", linkedLicences[0].Text!.First().Text);

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
        
        var agreedSchemaLicenceGroup = (await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            [],
            new NaldLicenceStatusData(),
            [],
            _pdfDataExtractor1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1))).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);

        Assert.Equal("14/46/03/0852", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }
    
    [Theory]
    [InlineData("12100004__Application Transfer Issued Licence - [1982] - (1982).pdf", "7 DAY OF OCTOBER 19 82", "07/10/1982", 4, 0)] // Works in Tesseract+AI too
    [InlineData("22630110__Issued licence- 2-26-30-110 6075592.PDF", "29/10/02", "29/10/2002", 14, 1)] // Does better then Azure AI Vison - that skips word 'issue'
    [InlineData("12201021__Application New Licence Issued - [1966] - (1966).pdf", "28th day of JULY, 19 65", "28/07/1965", 6, 0)] // Does better then Azure AI Vison - that skips word 'JULY'
    // EXAMPLE OF IMPOSSIBLE ONE "12504178R01__Application type unknown Licence Issued (01.05.2007).pdf", "299 July'03", "", 10 // Stamp is incredibly faint, Tesseract doesnt read - Azure AI reads it wrong
    public async Task When1_ThenIssueDateCorrectly(string filename, string expectedIssueDate, string expectedIssueDate2, int expectedResults, int expectedLinkedLicenceLength)
    {
        // Act
        await SetupLicenceNumbersAsync(3);
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(expectedResults, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.Equal(expectedIssueDate, dateOfIssue.Text!.First().Text);
        
        var schemaData = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            [],
            new NaldLicenceStatusData(),
            [],
            _pdfDataExtractor3,
            TestConfig.PdfFolder3,
            0,
            await LookupConfigurationAsync(3));

        var licence = schemaData[0].Licences[0];
        Assert.Equal(expectedLinkedLicenceLength, licence.LinkedLicences.Length);

        Assert.NotNull(licence.LicenceVersion.IssueDate);
        Assert.Equal(expectedIssueDate2, licence.LicenceVersion.IssueDate!.Value.ToShortDateString());
    }
}