using Meziantou.Xunit;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tests.Helper;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

[EnableParallelization]
[Collection("First Names 1")]
public class Wr51PdfPigNoOcrPdfTests(SingletonFirstNamesFixture firstNamesFixture)
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
        return firstNamesFixture.SetupLicenceNumbersAsync(regionCode, DatabaseCacheService);
    }

    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    
    private readonly IPdfDataExtractorService _pdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            // TODO mock of an OCR service that errors if called
        },
        CacheService,
        OutputService,
        DocumentService,
        DocnetAlternativeDocumentService);
    
    private static readonly int NoneNeRegionCode = 1;
    private static readonly int NeRegionCode = 3;
    
    private static Dictionary<string, DmsFileData> FileLicenceMapping =>
        new()
        {
            { 
                FormattingHelper.StripForComparison("25 68 001 247", NoneNeRegionCode)!,
                new DmsFileData
                {
                    DestinationFileName = "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10892721.pdf",
                    FileId = GuidHelper.GetConsistentFileIdFromFilename("Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10892721.pdf"),
                    DmsPath = "Something to look for",
                    RegionId = 1
                }
            },
            {
                FormattingHelper.StripForComparison("25 68 001 248", NoneNeRegionCode)!,
                new DmsFileData
                {
                    DestinationFileName = "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10893422.pdf",
                    FileId = GuidHelper.GetConsistentFileIdFromFilename("Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10893422.pdf"),
                    DmsPath = "Something to look for",
                    RegionId = 1
                }
            },
            {
                FormattingHelper.StripForComparison("NE/026/0034/018", NeRegionCode)!,
                new DmsFileData
                {
                    DestinationFileName = "NE0260034018__Application Minor Variation Issued Licence 11.12.2019 11149535.pdf",
                    FileId = GuidHelper.GetConsistentFileIdFromFilename("NE0260034018__Application Minor Variation Issued Licence 11.12.2019 11149535.pdf"),
                    DmsPath = "Something to look for",
                    RegionId = 3
                }
            },
            {
                FormattingHelper.StripForComparison("NE/026/0034/052", NeRegionCode)!,
                new DmsFileData
                {
                    DestinationFileName = "NE0260034052__Application Apportionment Issued Licence 11.12.2019 11149440.pdf",
                    FileId = GuidHelper.GetConsistentFileIdFromFilename("NE0260034052__Application Apportionment Issued Licence 11.12.2019 11149440.pdf"),
                    DmsPath = "Something to look for",
                    RegionId = 3
                }
            }
        };
    
    private static Dictionary<string, DmsFileData> FileLicenceMappingWithout52 =>
        new()
        {
            { 
                FormattingHelper.StripForComparison("25 68 001 247", NeRegionCode)!,
                new DmsFileData
                {
                    DestinationFileName = "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10892721.pdf",
                    FileId = GuidHelper.GetConsistentFileIdFromFilename("Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10892721.pdf"),
                    DmsPath = "Something to look for",
                    RegionId = 1
                }
            },
            {
                FormattingHelper.StripForComparison("25 68 001 248", NeRegionCode)!,
                new DmsFileData
                {
                    DestinationFileName = "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10893422.pdf",
                    FileId = GuidHelper.GetConsistentFileIdFromFilename("Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10893422.pdf"),
                    DmsPath = "Something to look for",
                    RegionId = 1
                }
            },
            {
                FormattingHelper.StripForComparison("NE/026/0034/018", NeRegionCode)!,
                new DmsFileData
                {
                    DestinationFileName = "NE0260034018__Application Minor Variation Issued Licence 11.12.2019 11149535.pdf",
                    FileId = GuidHelper.GetConsistentFileIdFromFilename("NE0260034018__Application Minor Variation Issued Licence 11.12.2019 11149535.pdf"),
                    DmsPath = "Something to look for",
                    RegionId = 3
                }
            }
        };
    
    private readonly NaldLicenceStatusData _naldLicenceStatusData = new()
    {
        LiveLicences = [
            "2568001247",
            "2568001249"
        ],
        LapsedLicences = [],
        ExpiredLicences = [],
        RevokedLicences = [],
        ImpoundmentLicences = []
    };
    
    private static readonly Dictionary<string, List<NaldData>> NaldData = GetNaldData();

    private static Dictionary<string, List<NaldData>> GetNaldData()
    {
        var returnList = new Dictionary<string, List<NaldData>>
        {
            {
                "1|2568001247",
                [
                    new NaldData
                    {
                        AsrcCode = "G",
                        LicenceNumber = "25/68/001/247"
                    }
                ]
            },
            {
                "1|2568001248",
                [
                    new NaldData
                    {
                        AsrcCode = "S",
                        LicenceNumber = "25/68/001/248"
                    }
                ]
            },
            {
                "1|2568001249",
                [
                    new NaldData
                    {
                        AsrcCode = "S",
                        LicenceNumber = "25/68/001/249"
                    }
                ]
            }
        };

        return returnList;
    }

    private async Task<LookupConfiguration> LookupConfigurationAsync(
        int regionCode,
        int fileLicenceMapping,
        string pdfFolder,
        bool isAbstractionLicence = true)
    {
        return new LookupConfiguration(
            isAbstractionLicence ? WalLabelConfiguration.GetLabels() : Wr51LabelConfiguration.GetLabels(),
            fileLicenceMapping == 1 ? FileLicenceMapping : FileLicenceMappingWithout52,
            [],
            await firstNamesFixture.FirstNamesCsvTask(),
            new LocalFileService(pdfFolder),
            CacheService,
            regionCode,
            lineHeight: isAbstractionLicence ? 9 : 6);
    }
    
    private async Task<MatchesResult> GetMatchesAsync(string fileName, int regionCode, int folderNumber = 1, int fileLicenceMapping = 1)
    {
        var isWr51 = folderNumber == -99;
        
        var pdfFolder = folderNumber == 1 ? TestConfig.PdfFolder : TestConfig.PdfFolder2;
        if (folderNumber == 3) pdfFolder = TestConfig.PdfFolder3;
        if (folderNumber == 5) pdfFolder = TestConfig.PdfFolder5;
        if (isWr51) pdfFolder = TestConfig.PdfFolderWr51;
        
        return await _pdfDataExtractor.GetMatchesAsync(
            fileName,
            new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(fileName) },
            await LookupConfigurationAsync(regionCode, fileLicenceMapping, pdfFolder, !isWr51),
            [fileName],
            0);
    }
    
    [Fact]
    public async Task WhenWR51_POCA_1_ThenGood()
    {
        // Arrange
        const string filename = "WR51__121014G8__dummy.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, GeneralConstants.UnsetRegionCode, -99);
        Assert.Equal(41, resultFull.Matches!.Count);
        
        var sourceOfSupply = resultFull.Matches[0];
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches[1];
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("in", pointOfAbstraction.Text[0].Text); // TODO should be 'In'
        
        var meansOfAbstraction = resultFull.Matches[2];
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches[3];
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches[4];
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches[5];
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches[6];
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches[7];
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches[8];
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("Not", provisionOfInformation.Text[0].Text);
        
        var specialConditions = resultFull.Matches[9];
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("N/A", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches[10];
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches[11];
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches[12];
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches[13];
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("12/101/4/G/8", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches[14];
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Ja", metWith.Text[0].Text);
        Assert.EndsWith("or", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches[15];
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("Ar", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("an", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches[16];
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.StartsWith("Ly", siteAddress.Text[0].Text);
        Assert.EndsWith("JQ", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches[17];
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Less Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches[18];
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("86", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches[19];
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Flow Measurement Coordinator", position.Text[0].Text);
        
        var time = resultFull.Matches[20];
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("11:20", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches[21];
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(2, nameAndAddress.Text.Count);
        Assert.Equal("Southern Water Service Limited, Southern House, Yeoman Road, Worthing,", nameAndAddress.Text[0].Text);
        Assert.Equal("BN13 3NX", nameAndAddress.Text[1].Text);
        
        var meterMake = resultFull.Matches[22];
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("Abstraction Flowmeter", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches[23];
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("V/", serialNumber.Text[0].Text);
        Assert.EndsWith("2", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches[24];
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("4,714,612", reading.Text[0].Text);
        
        var units = resultFull.Matches[25];
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("m3", units.Text[0].Text);
        
        var other = resultFull.Matches[26];
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches[27];
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches[28];
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("30/06/2021", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches[29];
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("Yes", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches[30];
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches[31];
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches[32];
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("Yes", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches[33];
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches[34];
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Ja", formSentTo.Text[0].Text);
        Assert.EndsWith("or", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches[35];
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("12/04/2024", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches[36];
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var generalComments = resultFull.Matches[37];
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(5, generalComments.Text.Count);
        Assert.StartsWith("Licence 12/", generalComments.Text[0].Text);
        Assert.EndsWith("single borehole.", generalComments.Text[0].Text);
        Assert.StartsWith("No RTW", generalComments.Text[4].Text);
        Assert.EndsWith("inspection.", generalComments.Text[4].Text);
        
        var maintenance = resultFull.Matches[38];
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
        Assert.Equal(3, maintenance.Text[0].Columns.Count);
        Assert.Equal("Maintenance: Yes Frequency: Daily By whom: JP", maintenance.Text[0].Text);
        Assert.Equal(3, maintenance.SubResults.Count);

        var maintenanceSubLabel = maintenance.SubResults[0];
        Assert.NotNull(maintenanceSubLabel);
        Assert.Equal("MaintenanceLineMaintenance", maintenanceSubLabel.MatchedLabelName);
        Assert.Equal("Yes", maintenanceSubLabel.Text[0].Text);
        
        var frequencySubLabel = maintenance.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("MaintenanceLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Daily", frequencySubLabel.Text[0].Text);
        
        var byWhomSubLabel = maintenance.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("MaintenanceLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("JP", byWhomSubLabel.Text[0].Text);
        
        var readingsTaken = resultFull.Matches[39];
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
        Assert.Equal(3, readingsTaken.Text[0].Columns.Count);
        Assert.Equal("Readings taken: Yes Frequency: Fortnightly By whom: MP", readingsTaken.Text[0].Text);
        Assert.Equal(3, readingsTaken.SubResults.Count);

        var readingsTakenSubLabel = readingsTaken.SubResults[0];
        Assert.NotNull(readingsTakenSubLabel);
        Assert.Equal("ReadingsTakenLineReadingsTaken", readingsTakenSubLabel.MatchedLabelName);
        Assert.Equal("Yes", readingsTakenSubLabel.Text[0].Text);
        
        frequencySubLabel = readingsTaken.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("ReadingsTakenLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Fortnightly", frequencySubLabel.Text[0].Text);
        
        byWhomSubLabel = readingsTaken.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("ReadingsTakenLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("MP", byWhomSubLabel.Text[0].Text);
        
        var inspectionDate = resultFull.Matches[40];
        Assert.NotNull(inspectionDate);
        Assert.Equal("InspectionDate", inspectionDate.LabelGroupName);
        Assert.Equal("04/03/2024", inspectionDate.Text[0].Text);
    }
    
    [Fact]
    public async Task WhenWR51_POCA_2_ThenGood()
    {
        // Arrange
        const string filename = "WR51__1343025G107__dummy.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, GeneralConstants.UnsetRegionCode, -99);
        Assert.Equal(41, resultFull.Matches!.Count);
        
        var sourceOfSupply = resultFull.Matches[0];
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches[1];
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("in", pointOfAbstraction.Text[0].Text); // TODO should be 'In'
        
        var meansOfAbstraction = resultFull.Matches[2];
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches[3];
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches[4];
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches[5];
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches[6];
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches[7];
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches[8];
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("Not", provisionOfInformation.Text[0].Text);
        
        var specialConditions = resultFull.Matches[9];
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches[10];
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches[11];
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches[12];
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches[13];
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("13/43/025/G/107", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches[14];
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Mr", metWith.Text[0].Text);
        Assert.EndsWith("ey", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches[15];
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("Be", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("re", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches[16];
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.Equal("Same as avove", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches[17];
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches[18];
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("72", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches[19];
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Owner and Farm Manager", position.Text[0].Text);
        
        var time = resultFull.Matches[20];
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("10:00", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches[21];
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(5, nameAndAddress.Text.Count);
        Assert.StartsWith("Co", nameAndAddress.Text[0].Text);
        Assert.StartsWith("Wi", nameAndAddress.Text[4].Text);
        Assert.EndsWith("e", nameAndAddress.Text[4].Text);
        
        var meterMake = resultFull.Matches[22];
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("Farmer", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches[23];
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("34", serialNumber.Text[0].Text);
        Assert.EndsWith("3", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches[24];
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("77668", reading.Text[0].Text);
        
        var units = resultFull.Matches[25];
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("cubic meters", units.Text[0].Text);
        
        var other = resultFull.Matches[26];
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches[27];
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches[28];
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("14/04/2022", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches[29];
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("Yes", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches[30];
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches[31];
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("No", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches[32];
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches[33];
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches[34];
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Mr", formSentTo.Text[0].Text);
        Assert.EndsWith("on", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches[35];
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("01/02/2017", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches[36];
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var generalComments = resultFull.Matches[37];
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(15, generalComments.Text.Count);
        Assert.StartsWith("The bore", generalComments.Text[0].Text);
        Assert.EndsWith("to", generalComments.Text[0].Text);
        Assert.StartsWith("weeks", generalComments.Text[14].Text);
        Assert.EndsWith("invoice.", generalComments.Text[14].Text);
        
        var maintenance = resultFull.Matches[38];
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
        Assert.Equal(3, maintenance.Text[0].Columns.Count);
        Assert.Equal("Maintenance: Yes Frequency: Daily By whom: JP", maintenance.Text[0].Text);
        Assert.Equal(3, maintenance.SubResults.Count);

        var maintenanceSubLabel = maintenance.SubResults[0];
        Assert.NotNull(maintenanceSubLabel);
        Assert.Equal("MaintenanceLineMaintenance", maintenanceSubLabel.MatchedLabelName);
        Assert.Equal("Yes", maintenanceSubLabel.Text[0].Text);
        
        var frequencySubLabel = maintenance.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("MaintenanceLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Daily", frequencySubLabel.Text[0].Text);
        
        var byWhomSubLabel = maintenance.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("MaintenanceLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("JP", byWhomSubLabel.Text[0].Text);
        
        var readingsTaken = resultFull.Matches[39];
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
        Assert.Equal(3, readingsTaken.Text[0].Columns.Count);
        Assert.Equal("Readings taken: Yes Frequency: Fortnightly By whom: MP", readingsTaken.Text[0].Text);
        Assert.Equal(3, readingsTaken.SubResults.Count);

        var readingsTakenSubLabel = readingsTaken.SubResults[0];
        Assert.NotNull(readingsTakenSubLabel);
        Assert.Equal("ReadingsTakenLineReadingsTaken", readingsTakenSubLabel.MatchedLabelName);
        Assert.Equal("Yes", readingsTakenSubLabel.Text[0].Text);
        
        frequencySubLabel = readingsTaken.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("ReadingsTakenLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Fortnightly", frequencySubLabel.Text[0].Text);
        
        byWhomSubLabel = readingsTaken.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("ReadingsTakenLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("MP", byWhomSubLabel.Text[0].Text);
        
        var inspectionDate = resultFull.Matches[40];
        Assert.NotNull(inspectionDate);
        Assert.Equal("InspectionDate", inspectionDate.LabelGroupName);
        Assert.Equal("31/01/2017", inspectionDate.Text[0].Text);
    }
    
    [Fact]
    public async Task WhenWR51_POCA_3_ThenGood()
    {
        // Arrange
        const string filename = "WR51__1343026G118__dummy.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, GeneralConstants.UnsetRegionCode, -99);
        Assert.Equal(41, resultFull.Matches!.Count);
        
        var sourceOfSupply = resultFull.Matches[0];
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches[1];
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("in", pointOfAbstraction.Text[0].Text); // TODO should be 'In'
        
        var meansOfAbstraction = resultFull.Matches[2];
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches[3];
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches[4];
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches[5];
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches[6];
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches[7];
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches[8];
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("in", provisionOfInformation.Text[0].Text); // TODO why not In
        
        var specialConditions = resultFull.Matches[9];
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches[10];
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches[11];
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches[12];
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches[13];
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("13/43/026/G/118", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches[14];
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Mr", metWith.Text[0].Text);
        Assert.EndsWith("gs", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches[15];
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("Be", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("re", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches[16];
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.StartsWith("Wi", siteAddress.Text[0].Text);
        Assert.EndsWith("on", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches[17];
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("CR", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches[18];
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("01", telephoneNumber.Text[0].Text);
        Assert.EndsWith("62", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches[19];
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Farm Owner", position.Text[0].Text);
        
        var time = resultFull.Matches[20];
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("10:00", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches[21];
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(1, nameAndAddress.Text.Count);
        Assert.StartsWith("Wi", nameAndAddress.Text[0].Text);
        Assert.EndsWith("on", nameAndAddress.Text[0].Text);
        
        var meterMake = resultFull.Matches[22];
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("Kent", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches[23];
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("13", serialNumber.Text[0].Text);
        Assert.EndsWith("7", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches[24];
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("1154546", reading.Text[0].Text);
        
        var units = resultFull.Matches[25];
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("cubic meter", units.Text[0].Text);
        
        var other = resultFull.Matches[26];
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches[27];
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches[28];
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("21/08/2019", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches[29];
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("No", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches[30];
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches[31];
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches[32];
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches[33];
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches[34];
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Mr", formSentTo.Text[0].Text);
        Assert.EndsWith("on", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches[35];
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("12/09/2016", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches[36];
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var generalComments = resultFull.Matches[37];
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(12, generalComments.Text.Count);
        Assert.StartsWith("There are", generalComments.Text[0].Text);
        Assert.EndsWith("away", generalComments.Text[0].Text);
        Assert.StartsWith("purpose", generalComments.Text[11].Text);
        Assert.EndsWith("chickens.", generalComments.Text[11].Text);
        
        var maintenance = resultFull.Matches[38];
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
        Assert.Equal(3, maintenance.Text[0].Columns.Count);
        Assert.Equal("Maintenance: No Frequency: Monthly By whom: JP", maintenance.Text[0].Text);
        Assert.Equal(3, maintenance.SubResults.Count);

        var maintenanceSubLabel = maintenance.SubResults[0];
        Assert.NotNull(maintenanceSubLabel);
        Assert.Equal("MaintenanceLineMaintenance", maintenanceSubLabel.MatchedLabelName);
        Assert.Equal("No", maintenanceSubLabel.Text[0].Text);
        
        var frequencySubLabel = maintenance.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("MaintenanceLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Monthly", frequencySubLabel.Text[0].Text);
        
        var byWhomSubLabel = maintenance.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("MaintenanceLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("JP", byWhomSubLabel.Text[0].Text);
        
        var readingsTaken = resultFull.Matches[39];
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
        Assert.Equal(3, readingsTaken.Text[0].Columns.Count);
        Assert.Equal("Readings taken: Yes Frequency: Daily By whom: MP", readingsTaken.Text[0].Text);
        Assert.Equal(3, readingsTaken.SubResults.Count);

        var readingsTakenSubLabel = readingsTaken.SubResults[0];
        Assert.NotNull(readingsTakenSubLabel);
        Assert.Equal("ReadingsTakenLineReadingsTaken", readingsTakenSubLabel.MatchedLabelName);
        Assert.Equal("Yes", readingsTakenSubLabel.Text[0].Text);
        
        frequencySubLabel = readingsTaken.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("ReadingsTakenLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Daily", frequencySubLabel.Text[0].Text);
        
        byWhomSubLabel = readingsTaken.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("ReadingsTakenLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("MP", byWhomSubLabel.Text[0].Text);
        
        var inspectionDate = resultFull.Matches[40];
        Assert.NotNull(inspectionDate);
        Assert.Equal("InspectionDate", inspectionDate.LabelGroupName);
        Assert.Equal("26/06/2024", inspectionDate.Text[0].Text);
    }
    
    [Fact]
    public async Task WhenWR51_POCA_4_ThenGood()
    {
        // Arrange
        const string filename = "WR51__1343026S047__dummy.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, GeneralConstants.UnsetRegionCode, -99);
        Assert.Equal(41, resultFull.Matches!.Count);
        
        var sourceOfSupply = resultFull.Matches[0];
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches[1];
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("in", pointOfAbstraction.Text[0].Text); // TODO should be 'In'
        
        var meansOfAbstraction = resultFull.Matches[2];
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches[3];
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches[4];
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches[5];
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches[6];
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches[7];
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches[8];
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("in", provisionOfInformation.Text[0].Text); // TODO why not In
        
        var specialConditions = resultFull.Matches[9];
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches[10];
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches[11];
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches[12];
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches[13];
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("13/43/026/S/047", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches[14];
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Mr", metWith.Text[0].Text);
        Assert.EndsWith("gs", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches[15];
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("Be", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("re", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches[16];
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.Equal("Same as above", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches[17];
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Highly Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches[18];
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("97", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches[19];
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Farm Owner", position.Text[0].Text);
        
        var time = resultFull.Matches[20];
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("10:00", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches[21];
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(6, nameAndAddress.Text.Count);
        Assert.StartsWith("San", nameAndAddress.Text[0].Text);
        Assert.EndsWith("JZ", nameAndAddress.Text[5].Text);
        
        var meterMake = resultFull.Matches[22];
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("Zenner", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches[23];
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("34", serialNumber.Text[0].Text);
        Assert.EndsWith("6", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches[24];
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("45545", reading.Text[0].Text);
        
        var units = resultFull.Matches[25];
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("cubic meter", units.Text[0].Text);
        
        var other = resultFull.Matches[26];
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches[27];
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches[28];
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("21/08/2019", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches[29];
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("No", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches[30];
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches[31];
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches[32];
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches[33];
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches[34];
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("To", formSentTo.Text[0].Text);
        Assert.EndsWith("gs", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches[35];
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("07/02/2017", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches[36];
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var generalComments = resultFull.Matches[37];
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(16, generalComments.Text.Count);
        Assert.StartsWith("Abstraction", generalComments.Text[0].Text);
        Assert.EndsWith("sources", generalComments.Text[0].Text);
        Assert.StartsWith("them", generalComments.Text[15].Text);
        Assert.EndsWith("payment.", generalComments.Text[15].Text);
        
        var maintenance = resultFull.Matches[38];
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
        Assert.Equal(3, maintenance.Text[0].Columns.Count);
        Assert.Equal("Maintenance: No Frequency: Monthly By whom: JP", maintenance.Text[0].Text);
        Assert.Equal(3, maintenance.SubResults.Count);

        var maintenanceSubLabel = maintenance.SubResults[0];
        Assert.NotNull(maintenanceSubLabel);
        Assert.Equal("MaintenanceLineMaintenance", maintenanceSubLabel.MatchedLabelName);
        Assert.Equal("No", maintenanceSubLabel.Text[0].Text);
        
        var frequencySubLabel = maintenance.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("MaintenanceLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Monthly", frequencySubLabel.Text[0].Text);
        
        var byWhomSubLabel = maintenance.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("MaintenanceLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("JP", byWhomSubLabel.Text[0].Text);
        
        var readingsTaken = resultFull.Matches[39];
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
        Assert.Equal(3, readingsTaken.Text[0].Columns.Count);
        Assert.Equal("Readings taken: Yes Frequency: Daily By whom: MP", readingsTaken.Text[0].Text);
        Assert.Equal(3, readingsTaken.SubResults.Count);

        var readingsTakenSubLabel = readingsTaken.SubResults[0];
        Assert.NotNull(readingsTakenSubLabel);
        Assert.Equal("ReadingsTakenLineReadingsTaken", readingsTakenSubLabel.MatchedLabelName);
        Assert.Equal("Yes", readingsTakenSubLabel.Text[0].Text);
        
        frequencySubLabel = readingsTaken.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("ReadingsTakenLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Daily", frequencySubLabel.Text[0].Text);
        
        byWhomSubLabel = readingsTaken.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("ReadingsTakenLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("MP", byWhomSubLabel.Text[0].Text);
        
        var inspectionDate = resultFull.Matches[40];
        Assert.NotNull(inspectionDate);
        Assert.Equal("InspectionDate", inspectionDate.LabelGroupName);
        Assert.Equal("26/06/2023", inspectionDate.Text[0].Text);
    }
    
    [Fact]
    public async Task WhenWR51_POCA_5_ThenGood()
    {
        // Arrange
        const string filename = "WR51__114222246__dummy.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, GeneralConstants.UnsetRegionCode, -99);
        Assert.Equal(41, resultFull.Matches!.Count);
        
        var sourceOfSupply = resultFull.Matches[0];
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches[1];
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("in", pointOfAbstraction.Text[0].Text); // TODO should be 'In'
        
        var meansOfAbstraction = resultFull.Matches[2];
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches[3];
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches[4];
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches[5];
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches[6];
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches[7];
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches[8];
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("in", provisionOfInformation.Text[0].Text); // TODO why not In
        
        var specialConditions = resultFull.Matches[9];
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches[10];
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches[11];
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches[12];
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches[13];
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("11/42/22.2/46", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches[14];
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Ti", metWith.Text[0].Text);
        Assert.EndsWith("am", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches[15];
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("St", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("rt", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches[16];
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.Equal("Same as above", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches[17];
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Highly Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches[18];
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("97", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches[19];
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Farm Owner", position.Text[0].Text);
        
        var time = resultFull.Matches[20];
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("12:10", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches[21];
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(1, nameAndAddress.Text.Count);
        Assert.StartsWith("J G", nameAndAddress.Text[0].Text);
        Assert.EndsWith("DF", nameAndAddress.Text[0].Text);
        
        var meterMake = resultFull.Matches[22];
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("Zenner", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches[23];
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("34", serialNumber.Text[0].Text);
        Assert.EndsWith("6", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches[24];
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("45545", reading.Text[0].Text);
        
        var units = resultFull.Matches[25];
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("cubic meter", units.Text[0].Text);
        
        var other = resultFull.Matches[26];
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches[27];
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches[28];
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("21/08/2019", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches[29];
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("No", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches[30];
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches[31];
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches[32];
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches[33];
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches[34];
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Ti", formSentTo.Text[0].Text);
        Assert.EndsWith("am", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches[35];
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("26/06/2023", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches[36];
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var generalComments = resultFull.Matches[37];
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(4, generalComments.Text.Count);
        Assert.StartsWith("This", generalComments.Text[0].Text);
        Assert.EndsWith("taken.", generalComments.Text[0].Text);
        Assert.StartsWith("the", generalComments.Text[3].Text);
        Assert.EndsWith("volume.", generalComments.Text[3].Text);
        
        var maintenance = resultFull.Matches[38];
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
        Assert.Equal(3, maintenance.Text[0].Columns.Count);
        Assert.Equal("Maintenance: No Frequency: Monthly By whom: JP", maintenance.Text[0].Text);
        Assert.Equal(3, maintenance.SubResults.Count);

        var maintenanceSubLabel = maintenance.SubResults[0];
        Assert.NotNull(maintenanceSubLabel);
        Assert.Equal("MaintenanceLineMaintenance", maintenanceSubLabel.MatchedLabelName);
        Assert.Equal("No", maintenanceSubLabel.Text[0].Text);
        
        var frequencySubLabel = maintenance.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("MaintenanceLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Monthly", frequencySubLabel.Text[0].Text);
        
        var byWhomSubLabel = maintenance.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("MaintenanceLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("JP", byWhomSubLabel.Text[0].Text);
        
        var readingsTaken = resultFull.Matches[39];
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
        Assert.Equal(3, readingsTaken.Text[0].Columns.Count);
        Assert.Equal("Readings taken: Yes Frequency: Daily By whom: MP", readingsTaken.Text[0].Text);
        Assert.Equal(3, readingsTaken.SubResults.Count);

        var readingsTakenSubLabel = readingsTaken.SubResults[0];
        Assert.NotNull(readingsTakenSubLabel);
        Assert.Equal("ReadingsTakenLineReadingsTaken", readingsTakenSubLabel.MatchedLabelName);
        Assert.Equal("Yes", readingsTakenSubLabel.Text[0].Text);
        
        frequencySubLabel = readingsTaken.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("ReadingsTakenLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Daily", frequencySubLabel.Text[0].Text);
        
        byWhomSubLabel = readingsTaken.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("ReadingsTakenLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("MP", byWhomSubLabel.Text[0].Text);
        
        var inspectionDate = resultFull.Matches[40];
        Assert.NotNull(inspectionDate);
        Assert.Equal("InspectionDate", inspectionDate.LabelGroupName);
        Assert.Equal("26/06/2023", inspectionDate.Text[0].Text);
    }
    
    [Fact]
    public async Task WhenWR51_POCA_6_ThenGood()
    {
        // Arrange
        const string filename = "WR51__1041521202__dummy.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, GeneralConstants.UnsetRegionCode, -99);
        Assert.Equal(41, resultFull.Matches!.Count);
        
        var sourceOfSupply = resultFull.Matches[0];
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches[1];
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("in", pointOfAbstraction.Text[0].Text); // TODO should be 'In'
        
        var meansOfAbstraction = resultFull.Matches[2];
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches[3];
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches[4];
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches[5];
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches[6];
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches[7];
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches[8];
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("in", provisionOfInformation.Text[0].Text); // TODO why not In
        
        var specialConditions = resultFull.Matches[9];
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches[10];
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches[11];
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches[12];
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches[13];
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("10/41/521202", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches[14];
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Ed", metWith.Text[0].Text);
        Assert.EndsWith("es", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches[15];
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("Ja", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("ll", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches[16];
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.StartsWith("Spri", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches[17];
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Highly Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches[18];
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("0 7", telephoneNumber.Text[0].Text); // TODO why the spaces
        Assert.EndsWith("97", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches[19];
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
//        Assert.Equal(2, position.Text.Count); // TODO
        Assert.Equal("Head of Irrigation and Senior Farm", position.Text[0].Text);
//        Assert.Equal("Manager respectively", position.Text[1].Text);
        
        var time = resultFull.Matches[20];
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("10:00am", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches[21];
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(1, nameAndAddress.Text.Count);
        Assert.StartsWith("Ha", nameAndAddress.Text[0].Text);
        Assert.EndsWith("UJ", nameAndAddress.Text[0].Text);
        
        var meterMake = resultFull.Matches[22];
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("No meter on site", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches[23];
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.Equal("N/A", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches[24];
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("N/A", reading.Text[0].Text);
        
        var units = resultFull.Matches[25];
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("N/A", units.Text[0].Text);
        
        var other = resultFull.Matches[26];
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches[27];
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches[28];
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("10/06/2020", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches[29];
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("No", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches[30];
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches[31];
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches[32];
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches[33];
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches[34];
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Ri", formSentTo.Text[0].Text);
        Assert.EndsWith("es", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches[35];
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("11/12/2024", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches[36];
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var generalComments = resultFull.Matches[37];
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(19, generalComments.Text.Count);
        Assert.StartsWith("Licence", generalComments.Text[0].Text);
        Assert.EndsWith("per", generalComments.Text[0].Text);
        Assert.StartsWith("There", generalComments.Text[18].Text);
        Assert.EndsWith("time.", generalComments.Text[18].Text);
        
        var maintenance = resultFull.Matches[38];
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
        Assert.Equal(3, maintenance.Text[0].Columns.Count);
        Assert.Equal("Maintenance: No Frequency: Monthly By whom: JP", maintenance.Text[0].Text);
        Assert.Equal(3, maintenance.SubResults.Count);

        var maintenanceSubLabel = maintenance.SubResults[0];
        Assert.NotNull(maintenanceSubLabel);
        Assert.Equal("MaintenanceLineMaintenance", maintenanceSubLabel.MatchedLabelName);
        Assert.Equal("No", maintenanceSubLabel.Text[0].Text);
        
        var frequencySubLabel = maintenance.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("MaintenanceLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Monthly", frequencySubLabel.Text[0].Text);
        
        var byWhomSubLabel = maintenance.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("MaintenanceLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("JP", byWhomSubLabel.Text[0].Text);
        
        var readingsTaken = resultFull.Matches[39];
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
        Assert.Equal(3, readingsTaken.Text[0].Columns.Count);
        Assert.Equal("Readings taken: Yes Frequency: Daily By whom: MP", readingsTaken.Text[0].Text);
        Assert.Equal(3, readingsTaken.SubResults.Count);

        var readingsTakenSubLabel = readingsTaken.SubResults[0];
        Assert.NotNull(readingsTakenSubLabel);
        Assert.Equal("ReadingsTakenLineReadingsTaken", readingsTakenSubLabel.MatchedLabelName);
        Assert.Equal("Yes", readingsTakenSubLabel.Text[0].Text);
        
        frequencySubLabel = readingsTaken.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("ReadingsTakenLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Daily", frequencySubLabel.Text[0].Text);
        
        byWhomSubLabel = readingsTaken.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("ReadingsTakenLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("MP", byWhomSubLabel.Text[0].Text);
        
        var inspectionDate = resultFull.Matches[40];
        
        Assert.NotNull(inspectionDate);
        Assert.Equal("InspectionDate", inspectionDate.LabelGroupName);
        Assert.Equal("06/12/2024", inspectionDate.Text[0].Text);
    }
    
   [Fact]
    public async Task WhenWR51_POCA_7_ThenGood()
    {
        // Arrange
        const string filename = "WR51__1041531327__dummy.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, GeneralConstants.UnsetRegionCode, -99);
        Assert.Equal(41, resultFull.Matches!.Count);
        
        var sourceOfSupply = resultFull.Matches[0];
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches[1];
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("in", pointOfAbstraction.Text[0].Text); // TODO should be 'In'
        
        var meansOfAbstraction = resultFull.Matches[2];
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches[3];
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches[4];
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches[5];
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches[6];
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches[7];
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches[8];
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("in", provisionOfInformation.Text[0].Text); // TODO why not In
        
        var specialConditions = resultFull.Matches[9];
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches[10];
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches[11];
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches[12];
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches[13];
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("11/42/22.2/46", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches[14];
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Ti", metWith.Text[0].Text);
        Assert.EndsWith("am", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches[15];
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("St", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("rt", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches[16];
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.Equal("Same as above", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches[17];
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Highly Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches[18];
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("97", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches[19];
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Farm Owner", position.Text[0].Text);
        
        var time = resultFull.Matches[20];
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("12:10", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches[21];
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(1, nameAndAddress.Text.Count);
        Assert.StartsWith("J G", nameAndAddress.Text[0].Text);
        Assert.EndsWith("DF", nameAndAddress.Text[0].Text);
        
        var meterMake = resultFull.Matches[22];
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("Zenner", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches[23];
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("34", serialNumber.Text[0].Text);
        Assert.EndsWith("6", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches[24];
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("45545", reading.Text[0].Text);
        
        var units = resultFull.Matches[25];
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("cubic meter", units.Text[0].Text);
        
        var other = resultFull.Matches[26];
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches[27];
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches[28];
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("21/08/2019", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches[29];
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("No", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches[30];
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches[31];
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches[32];
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches[33];
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches[34];
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Ti", formSentTo.Text[0].Text);
        Assert.EndsWith("am", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches[35];
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("26/06/2023", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches[36];
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var generalComments = resultFull.Matches[37];
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(4, generalComments.Text.Count);
        Assert.StartsWith("This", generalComments.Text[0].Text);
        Assert.EndsWith("taken.", generalComments.Text[0].Text);
        Assert.StartsWith("the", generalComments.Text[3].Text);
        Assert.EndsWith("volume.", generalComments.Text[3].Text);
        
        var maintenance = resultFull.Matches[38];
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
        Assert.Equal(3, maintenance.Text[0].Columns.Count);
        Assert.Equal("Maintenance: No Frequency: Monthly By whom: JP", maintenance.Text[0].Text);
        Assert.Equal(3, maintenance.SubResults.Count);

        var maintenanceSubLabel = maintenance.SubResults[0];
        Assert.NotNull(maintenanceSubLabel);
        Assert.Equal("MaintenanceLineMaintenance", maintenanceSubLabel.MatchedLabelName);
        Assert.Equal("No", maintenanceSubLabel.Text[0].Text);
        
        var frequencySubLabel = maintenance.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("MaintenanceLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Monthly", frequencySubLabel.Text[0].Text);
        
        var byWhomSubLabel = maintenance.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("MaintenanceLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("JP", byWhomSubLabel.Text[0].Text);
        
        var readingsTaken = resultFull.Matches[39];
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
        Assert.Equal(3, readingsTaken.Text[0].Columns.Count);
        Assert.Equal("Readings taken: Yes Frequency: Daily By whom: MP", readingsTaken.Text[0].Text);
        Assert.Equal(3, readingsTaken.SubResults.Count);

        var readingsTakenSubLabel = readingsTaken.SubResults[0];
        Assert.NotNull(readingsTakenSubLabel);
        Assert.Equal("ReadingsTakenLineReadingsTaken", readingsTakenSubLabel.MatchedLabelName);
        Assert.Equal("Yes", readingsTakenSubLabel.Text[0].Text);
        
        frequencySubLabel = readingsTaken.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("ReadingsTakenLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Daily", frequencySubLabel.Text[0].Text);
        
        byWhomSubLabel = readingsTaken.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("ReadingsTakenLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("MP", byWhomSubLabel.Text[0].Text);
        
        var inspectionDate = resultFull.Matches[40];
        Assert.NotNull(inspectionDate);
        Assert.Equal("InspectionDate", inspectionDate.LabelGroupName);
        Assert.Equal("26/06/2023", inspectionDate.Text[0].Text);
    }
    
   [Fact]
    public async Task WhenWR51_POCA_8_ThenGood()
    {
        // Arrange
        const string filename = "WR51__1041531329__dummy.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, GeneralConstants.UnsetRegionCode, -99);
        Assert.Equal(41, resultFull.Matches!.Count);
        
        var sourceOfSupply = resultFull.Matches[0];
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches[1];
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("in", pointOfAbstraction.Text[0].Text); // TODO should be 'In'
        
        var meansOfAbstraction = resultFull.Matches[2];
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches[3];
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches[4];
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches[5];
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches[6];
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches[7];
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches[8];
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("in", provisionOfInformation.Text[0].Text); // TODO why not In
        
        var specialConditions = resultFull.Matches[9];
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches[10];
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches[11];
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches[12];
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches[13];
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("11/42/22.2/46", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches[14];
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Ti", metWith.Text[0].Text);
        Assert.EndsWith("am", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches[15];
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("St", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("rt", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches[16];
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.Equal("Same as above", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches[17];
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Highly Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches[18];
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("97", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches[19];
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Farm Owner", position.Text[0].Text);
        
        var time = resultFull.Matches[20];
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("12:10", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches[21];
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(1, nameAndAddress.Text.Count);
        Assert.StartsWith("J G", nameAndAddress.Text[0].Text);
        Assert.EndsWith("DF", nameAndAddress.Text[0].Text);
        
        var meterMake = resultFull.Matches[22];
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("Zenner", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches[23];
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("34", serialNumber.Text[0].Text);
        Assert.EndsWith("6", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches[24];
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("45545", reading.Text[0].Text);
        
        var units = resultFull.Matches[25];
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("cubic meter", units.Text[0].Text);
        
        var other = resultFull.Matches[26];
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches[27];
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches[28];
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("21/08/2019", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches[29];
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("No", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches[30];
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches[31];
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches[32];
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches[33];
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches[34];
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Ti", formSentTo.Text[0].Text);
        Assert.EndsWith("am", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches[35];
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("26/06/2023", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches[36];
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var generalComments = resultFull.Matches[37];
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(4, generalComments.Text.Count);
        Assert.StartsWith("This", generalComments.Text[0].Text);
        Assert.EndsWith("taken.", generalComments.Text[0].Text);
        Assert.StartsWith("the", generalComments.Text[3].Text);
        Assert.EndsWith("volume.", generalComments.Text[3].Text);
        
        var maintenance = resultFull.Matches[38];
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
        Assert.Equal(3, maintenance.Text[0].Columns.Count);
        Assert.Equal("Maintenance: No Frequency: Monthly By whom: JP", maintenance.Text[0].Text);
        Assert.Equal(3, maintenance.SubResults.Count);

        var maintenanceSubLabel = maintenance.SubResults[0];
        Assert.NotNull(maintenanceSubLabel);
        Assert.Equal("MaintenanceLineMaintenance", maintenanceSubLabel.MatchedLabelName);
        Assert.Equal("No", maintenanceSubLabel.Text[0].Text);
        
        var frequencySubLabel = maintenance.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("MaintenanceLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Monthly", frequencySubLabel.Text[0].Text);
        
        var byWhomSubLabel = maintenance.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("MaintenanceLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("JP", byWhomSubLabel.Text[0].Text);
        
        var readingsTaken = resultFull.Matches[39];
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
        Assert.Equal(3, readingsTaken.Text[0].Columns.Count);
        Assert.Equal("Readings taken: Yes Frequency: Daily By whom: MP", readingsTaken.Text[0].Text);
        Assert.Equal(3, readingsTaken.SubResults.Count);

        var readingsTakenSubLabel = readingsTaken.SubResults[0];
        Assert.NotNull(readingsTakenSubLabel);
        Assert.Equal("ReadingsTakenLineReadingsTaken", readingsTakenSubLabel.MatchedLabelName);
        Assert.Equal("Yes", readingsTakenSubLabel.Text[0].Text);
        
        frequencySubLabel = readingsTaken.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("ReadingsTakenLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Daily", frequencySubLabel.Text[0].Text);
        
        byWhomSubLabel = readingsTaken.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("ReadingsTakenLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("MP", byWhomSubLabel.Text[0].Text);
        
        var inspectionDate = resultFull.Matches[40];
        Assert.NotNull(inspectionDate);
        Assert.Equal("InspectionDate", inspectionDate.LabelGroupName);
        Assert.Equal("26/06/2023", inspectionDate.Text[0].Text);
    }

   [Fact]
    public async Task WhenWR51_POCA_9_ThenGood()
    {
        // Arrange
        const string filename = "WR51__2569020001__dummy.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, GeneralConstants.UnsetRegionCode, -99);
        Assert.Equal(41, resultFull.Matches!.Count);
        
        var sourceOfSupply = resultFull.Matches[0];
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches[1];
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("in", pointOfAbstraction.Text[0].Text); // TODO should be 'In'
        
        var meansOfAbstraction = resultFull.Matches[2];
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches[3];
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches[4];
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches[5];
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches[6];
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches[7];
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches[8];
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("in", provisionOfInformation.Text[0].Text); // TODO why not In
        
        var specialConditions = resultFull.Matches[9];
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches[10];
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches[11];
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches[12];
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches[13];
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("11/42/22.2/46", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches[14];
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Ti", metWith.Text[0].Text);
        Assert.EndsWith("am", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches[15];
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("St", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("rt", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches[16];
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.Equal("Same as above", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches[17];
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Highly Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches[18];
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("97", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches[19];
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Farm Owner", position.Text[0].Text);
        
        var time = resultFull.Matches[20];
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("12:10", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches[21];
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(1, nameAndAddress.Text.Count);
        Assert.StartsWith("J G", nameAndAddress.Text[0].Text);
        Assert.EndsWith("DF", nameAndAddress.Text[0].Text);
        
        var meterMake = resultFull.Matches[22];
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("Zenner", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches[23];
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("34", serialNumber.Text[0].Text);
        Assert.EndsWith("6", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches[24];
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("45545", reading.Text[0].Text);
        
        var units = resultFull.Matches[25];
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("cubic meter", units.Text[0].Text);
        
        var other = resultFull.Matches[26];
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches[27];
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches[28];
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("21/08/2019", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches[29];
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("No", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches[30];
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches[31];
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches[32];
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches[33];
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches[34];
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Ti", formSentTo.Text[0].Text);
        Assert.EndsWith("am", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches[35];
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("26/06/2023", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches[36];
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var generalComments = resultFull.Matches[37];
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(4, generalComments.Text.Count);
        Assert.StartsWith("This", generalComments.Text[0].Text);
        Assert.EndsWith("taken.", generalComments.Text[0].Text);
        Assert.StartsWith("the", generalComments.Text[3].Text);
        Assert.EndsWith("volume.", generalComments.Text[3].Text);
        
        var maintenance = resultFull.Matches[38];
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
        Assert.Equal(3, maintenance.Text[0].Columns.Count);
        Assert.Equal("Maintenance: No Frequency: Monthly By whom: JP", maintenance.Text[0].Text);
        Assert.Equal(3, maintenance.SubResults.Count);

        var maintenanceSubLabel = maintenance.SubResults[0];
        Assert.NotNull(maintenanceSubLabel);
        Assert.Equal("MaintenanceLineMaintenance", maintenanceSubLabel.MatchedLabelName);
        Assert.Equal("No", maintenanceSubLabel.Text[0].Text);
        
        var frequencySubLabel = maintenance.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("MaintenanceLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Monthly", frequencySubLabel.Text[0].Text);
        
        var byWhomSubLabel = maintenance.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("MaintenanceLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("JP", byWhomSubLabel.Text[0].Text);
        
        var readingsTaken = resultFull.Matches[39];
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
        Assert.Equal(3, readingsTaken.Text[0].Columns.Count);
        Assert.Equal("Readings taken: Yes Frequency: Daily By whom: MP", readingsTaken.Text[0].Text);
        Assert.Equal(3, readingsTaken.SubResults.Count);

        var readingsTakenSubLabel = readingsTaken.SubResults[0];
        Assert.NotNull(readingsTakenSubLabel);
        Assert.Equal("ReadingsTakenLineReadingsTaken", readingsTakenSubLabel.MatchedLabelName);
        Assert.Equal("Yes", readingsTakenSubLabel.Text[0].Text);
        
        frequencySubLabel = readingsTaken.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("ReadingsTakenLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Daily", frequencySubLabel.Text[0].Text);
        
        byWhomSubLabel = readingsTaken.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("ReadingsTakenLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("MP", byWhomSubLabel.Text[0].Text);
        
        var inspectionDate = resultFull.Matches[40];
        Assert.NotNull(inspectionDate);
        Assert.Equal("InspectionDate", inspectionDate.LabelGroupName);
        Assert.Equal("26/06/2023", inspectionDate.Text[0].Text);
    }
    
    [Fact]
    public async Task WhenWR51_POCA_10_ThenGood()
    {
        // Arrange
        const string filename = "WR51__SO0420031002__dummy.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, GeneralConstants.UnsetRegionCode, -99);
        Assert.Equal(41, resultFull.Matches!.Count);
        
        var sourceOfSupply = resultFull.Matches[0];
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches[1];
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("in", pointOfAbstraction.Text[0].Text); // TODO should be 'In'
        
        var meansOfAbstraction = resultFull.Matches[2];
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches[3];
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches[4];
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches[5];
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches[6];
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches[7];
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches[8];
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("in", provisionOfInformation.Text[0].Text); // TODO why not In
        
        var specialConditions = resultFull.Matches[9];
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches[10];
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches[11];
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches[12];
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches[13];
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("SO/042/0031/002", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches[14];
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Pe", metWith.Text[0].Text);
        Assert.EndsWith("ie", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches[15];
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("St", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("rt", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches[16];
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.StartsWith("Avi", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches[17];
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Highly Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches[18];
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("97", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches[19];
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Estates and Fisheries Managers", position.Text[0].Text);
        
        var time = resultFull.Matches[20];
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("10:00am", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches[21];
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(2, nameAndAddress.Text.Count);
        Assert.StartsWith("St", nameAndAddress.Text[0].Text);
        Assert.EndsWith("AQ", nameAndAddress.Text[1].Text);
        
        var meterMake = resultFull.Matches[22];
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        //Assert.Equal(2, meterMake.Text.Count); // TODO fix
        Assert.Equal("No meter – means of measurement", meterMake.Text[0].Text);
        //Assert.Equal("under Fish Farm RPS", meterMake.Text[1].Text);
        
        var serialNumber = resultFull.Matches[23];
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.Equal("N/A", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches[24];
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("N/A", reading.Text[0].Text);
        
        var units = resultFull.Matches[25];
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("N/A", units.Text[0].Text);
        
        var other = resultFull.Matches[26];
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches[27];
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches[28];
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("10/06/2020", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches[29];
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("No", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches[30];
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches[31];
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches[32];
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches[33];
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches[34];
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Av", formSentTo.Text[0].Text);
        Assert.EndsWith("ry", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches[35];
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("09/04/2025", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches[36];
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var generalComments = resultFull.Matches[37];
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(10, generalComments.Text.Count);
        Assert.StartsWith("Licence", generalComments.Text[0].Text);
        Assert.EndsWith("license", generalComments.Text[0].Text);
        Assert.StartsWith("Action", generalComments.Text[9].Text);
        Assert.EndsWith("returns.", generalComments.Text[9].Text);
        
        var maintenance = resultFull.Matches[38];
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
        Assert.Equal(3, maintenance.Text[0].Columns.Count);
        Assert.Equal("Maintenance: No Frequency: Monthly By whom: JP", maintenance.Text[0].Text);
        Assert.Equal(3, maintenance.SubResults.Count);

        var maintenanceSubLabel = maintenance.SubResults[0];
        Assert.NotNull(maintenanceSubLabel);
        Assert.Equal("MaintenanceLineMaintenance", maintenanceSubLabel.MatchedLabelName);
        Assert.Equal("No", maintenanceSubLabel.Text[0].Text);
        
        var frequencySubLabel = maintenance.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("MaintenanceLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Monthly", frequencySubLabel.Text[0].Text);
        
        var byWhomSubLabel = maintenance.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("MaintenanceLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("JP", byWhomSubLabel.Text[0].Text);
        
        var readingsTaken = resultFull.Matches[39];
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
        Assert.Equal(3, readingsTaken.Text[0].Columns.Count);
        Assert.Equal("Readings taken: Yes Frequency: Daily By whom: MP", readingsTaken.Text[0].Text);
        Assert.Equal(3, readingsTaken.SubResults.Count);

        var readingsTakenSubLabel = readingsTaken.SubResults[0];
        Assert.NotNull(readingsTakenSubLabel);
        Assert.Equal("ReadingsTakenLineReadingsTaken", readingsTakenSubLabel.MatchedLabelName);
        Assert.Equal("Yes", readingsTakenSubLabel.Text[0].Text);
        
        frequencySubLabel = readingsTaken.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("ReadingsTakenLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Daily", frequencySubLabel.Text[0].Text);
        
        byWhomSubLabel = readingsTaken.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("ReadingsTakenLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("MP", byWhomSubLabel.Text[0].Text);
        
        var inspectionDate = resultFull.Matches[40];
        Assert.NotNull(inspectionDate);
        Assert.Equal("InspectionDate", inspectionDate.LabelGroupName);
        Assert.Equal("06/12/2024", inspectionDate.Text[0].Text);
    }
}