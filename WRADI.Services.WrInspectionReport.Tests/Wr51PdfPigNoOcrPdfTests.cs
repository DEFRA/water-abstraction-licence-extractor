using System.Text.Json;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Services;
using WRADI.Services.WrInspectionReport.Tests.Helper;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Ported from origin/task/wr51s (the pre-rewrite WR51 branch), which had 10 hand-crafted
/// "dummy" WR51 documents with known, verified expected values - unlike the real 789-file
/// corpus (WRADI.Services.WrInspectionReport.Tests), which has no ground truth and can only
/// measure coverage percentages. These are exact-value regression tests against fixed,
/// synthetic content, so a failure here means a real behaviour change, not corpus noise.
///
/// The dummy files use "dummy" instead of a real DMS GUID in their filename
/// (WR51__&lt;licence&gt;__dummy.pdf), so FileHelper.ExtractFileId can't parse an id from
/// them - GuidHelper.GetConsistentFileIdFromFilename derives a stable one instead, exactly
/// as the original branch did (confirmed byte-for-byte identical: the original's expected
/// FileId for WR51__121014G8__dummy.pdf still matches what this produces).
///
/// Assertions here were translated from the original branch's raw MatchesResult.Matches
/// list (positional index into an implementation-specific ordering) to lookups by
/// LabelGroupName, and re-verified against the current WrInspectionReportLabelConfiguration
/// rule set (a full rewrite this session - see analysis docs) rather than carried over
/// blind. Where current output still matches the original hand-verified ground truth, the
/// assertion is kept as-is. Where it doesn't, the assertion still reflects the correct
/// answer (per the original) - it is expected to fail, and that failure is the point: it's
/// a real, known gap (see summary in the PR/commit this was added in), not a mistranslation.
/// </summary>
public class Wr51PdfPigNoOcrPdfTests
{
    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    private static readonly IMessageQueueService MessageQueueService = new ApiMessageQueueService(new HttpClient());

    private static LookupConfiguration BuildLookupConfiguration(string pdfFolder)
    {
        return new LookupConfiguration(
            WrInspectionReportLabelConfiguration.GetLabels(),
            [],
            new LocalFileService(pdfFolder),
            CacheService,
            OutputService,
            new NullLicenceNumberService(),
            new DmsLookupService(),
            GeneralConstants.UnsetRegionCode,
            DateTime.Now,
            lineHeight: 6,
            minimumRowsForDigital: 30,
            useAnchoredLineGrouping: true);
    }

    private static IPdfDataExtractorService BuildPdfDataExtractor()
    {
        return new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService>(),
            CacheService,
            OutputService,
            DocumentService,
            DocnetAlternativeDocumentService,
            MessageQueueService);
    }

    private static async Task<(MatchesResult MatchesResult, DmsFileData DmsFileData)> GetMatchesAsync(string filename)
    {
        var pdfFolder = TestConfig.PdfFolder;
        var pdfDataExtractor = BuildPdfDataExtractor();

        try
        {
            var dmsFileData = new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(filename) };

            var (stopExecution, _, matchesResult) = await pdfDataExtractor.GetMatchesAsync(
                filename,
                dmsFileData,
                BuildLookupConfiguration(pdfFolder),
                [filename],
                processRunId: -99);

            Assert.False(stopExecution, $"Extraction reported StopExecution for {filename}");
            Assert.NotNull(matchesResult);

            return (matchesResult, dmsFileData);
        }
        finally
        {
            pdfDataExtractor.Dispose();
        }
    }

    [Fact]
    public async Task WhenWR51_POCA_1_ThenGood()
    {
        // Arrange
        const string filename = "WR51__121014G8__dummy.pdf";

        // Act
        var (matchesResult, dmsFileData) = await GetMatchesAsync(filename);
        var resultFull = matchesResult;
        
        var sourceOfSupply = resultFull.Matches!.First(m => m.LabelGroupName == "SourceOfSupply");
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "PointOfAbstraction");
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("In", pointOfAbstraction.Text[0].Text);
        
        var meansOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfAbstraction");
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches!.First(m => m.LabelGroupName == "Purposes");
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches!.First(m => m.LabelGroupName == "Period");
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches!.First(m => m.LabelGroupName == "Quantities");
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfMeasurement");
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches!.First(m => m.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches!.First(m => m.LabelGroupName == "ProvisionOfInformation");
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("Not", provisionOfInformation.Text[0].Text);
        
        var specialConditions = resultFull.Matches!.First(m => m.LabelGroupName == "SpecialConditions");
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("N/A", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches!.First(m => m.LabelGroupName == "Land");
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches!.First(m => m.LabelGroupName == "ChargingFactors");
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches!.First(m => m.LabelGroupName == "OtherProvisions");
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches!.First(m => m.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("12/101/4/G/8", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches!.First(m => m.LabelGroupName == "MetWith");
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Ja", metWith.Text[0].Text);
        Assert.EndsWith("or", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches!.First(m => m.LabelGroupName == "InspectingOfficer");
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("Ar", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("an", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches!.First(m => m.LabelGroupName == "SiteAddress");
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.StartsWith("Ly", siteAddress.Text[0].Text);
        Assert.EndsWith("JQ", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionClass");
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Less Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches!.First(m => m.LabelGroupName == "TelephoneNumber");
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.Single(telephoneNumber.Text!);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("86", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches!.First(m => m.LabelGroupName == "Position");
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Flow Measurement Coordinator", position.Text[0].Text);
        
        var time = resultFull.Matches!.First(m => m.LabelGroupName == "Time");
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("11:20", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches!.First(m => m.LabelGroupName == "NameAndAddress");
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(2, nameAndAddress.Text.Count);
        Assert.StartsWith("Sout", nameAndAddress.Text[0].Text);
        Assert.EndsWith("ing,", nameAndAddress.Text[0].Text);
        Assert.Equal("BN13 3NX", nameAndAddress.Text[1].Text);
        
        var meterMake = resultFull.Matches!.First(m => m.LabelGroupName == "MeterMake");
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("Abstraction Flowmeter", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches!.First(m => m.LabelGroupName == "SerialNumber");
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("V/", serialNumber.Text[0].Text);
        Assert.EndsWith("2", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches!.First(m => m.LabelGroupName == "Reading");
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("4,714,612", reading.Text[0].Text);
        
        var units = resultFull.Matches!.First(m => m.LabelGroupName == "Units");
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("m3", units.Text[0].Text);
        
        var other = resultFull.Matches!.First(m => m.LabelGroupName == "Other");
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches!.First(m => m.LabelGroupName == "CertificatesOfRecords");
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches!.First(m => m.LabelGroupName == "DateOfCertification");
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("30/06/2021", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches!.First(m => m.LabelGroupName == "Calibration");
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("Yes", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches!.First(m => m.LabelGroupName == "Conformance");
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches!.First(m => m.LabelGroupName == "FlowVerification");
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches!.First(m => m.LabelGroupName == "MeterVerification");
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("Yes", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches!.First(m => m.LabelGroupName == "WhereKept");
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches!.First(m => m.LabelGroupName == "FormSentTo");
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Ja", formSentTo.Text[0].Text);
        Assert.EndsWith("or", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches!.First(m => m.LabelGroupName == "Date");
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("12/04/2024", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentTemplateVersion");
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var documentHeader = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentHeader");
        Assert.NotNull(documentHeader);
        Assert.Equal("DocumentHeader", documentHeader.LabelGroupName);
        Assert.Single(documentHeader.Text);
        Assert.Equal("51", documentHeader.Text[0].Text);
        
        var generalComments = resultFull.Matches!.First(m => m.LabelGroupName == "GeneralComments");
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(5, generalComments.Text.Count);
        Assert.StartsWith("Licence 12/", generalComments.Text[0].Text);
        Assert.EndsWith("single borehole.", generalComments.Text[0].Text);
        Assert.StartsWith("No RTW", generalComments.Text[4].Text);
        Assert.EndsWith("inspection.", generalComments.Text[4].Text);
        
        var maintenance = resultFull.Matches!.First(m => m.LabelGroupName == "MaintenanceLine");
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
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
        
        var readingsTaken = resultFull.Matches!.First(m => m.LabelGroupName == "ReadingsTakenLine");
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
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
        
        var inspectionDate = resultFull.Matches[41];
        Assert.NotNull(inspectionDate);
        Assert.Single(inspectionDate.Text!);
        Assert.Equal("InspectionDate", inspectionDate.LabelGroupName);
        Assert.Equal("04/03/2024", inspectionDate.Text[0].Text);

        var converted = WrInspectionReportSchemaConverter.ToForm(matchesResult, dmsFileData);
        Assert.NotNull(converted);
        Assert.NotNull(converted.Metadata);
        Assert.Equal("2026_07_10_v1", converted.Metadata.DocumentTemplateVerison);
        Assert.Equal("WR51__121014G8__dummy.pdf", converted.Metadata.Filename);
        Assert.Equal(Guid.Parse("d60c3360-e810-cd19-d1de-406cbb5a938e"), converted.Metadata.FileId);
        Assert.Equal(false, converted.Metadata.IsScan);
        Assert.Equal(InOrderStatus.InOrder, converted.LicenceProvisions.SourceOfSupply);
        Assert.Equal(InOrderStatus.NotInOrder, converted.LicenceProvisions.Purposes);
        Assert.Equal(InOrderStatus.InOrder, converted.LicenceProvisions.PointOfAbstraction);
        Assert.Equal(InOrderStatus.NotApplicable, converted.LicenceProvisions.SpecialConditions);
        Assert.Equal(InOrderStatus.NotInOrder, converted.LicenceProvisions.ChargingFactors);
        Assert.Equal(InOrderStatus.InOrder, converted.LicenceProvisions.Land);
        Assert.Equal(InOrderStatus.InOrder, converted.LicenceProvisions.MeansOfAbstraction);
        Assert.Equal(InOrderStatus.InOrder, converted.LicenceProvisions.MeansOfMeasurement);
        Assert.Equal(InOrderStatus.NotApplicable, converted.LicenceProvisions.OtherProvisions);
        Assert.Equal(InOrderStatus.InOrder, converted.LicenceProvisions.Period);
        Assert.Equal(InOrderStatus.NotInOrder, converted.LicenceProvisions.ProvisionOfInformation);
        Assert.Equal(InOrderStatus.InOrder, converted.LicenceProvisions.Quantities);
        Assert.Equal(InOrderStatus.NotInOrder, converted.LicenceProvisions.Records);
        Assert.NotNull(converted.MeasurementDetails.Maintenance);
        Assert.Equal("Yes", converted.MeasurementDetails.Maintenance.Maintenance);
        Assert.Equal("Daily", converted.MeasurementDetails.Maintenance.Frequency);
        Assert.Equal("JP", converted.MeasurementDetails.Maintenance.ByWhom);
        Assert.NotNull(converted.MeasurementDetails.ReadingsTaken);
        Assert.Equal("Yes", converted.MeasurementDetails.ReadingsTaken.ReadingsTaken);
        Assert.Equal("Fortnightly", converted.MeasurementDetails.ReadingsTaken.Frequency);
        Assert.Equal("MP", converted.MeasurementDetails.ReadingsTaken.ByWhom);
        Assert.Equal("On Site", converted.MeasurementDetails.WhereKept);
        Assert.Equal(480, converted.GeneralComments?.Length);
        Assert.StartsWith("Licence", converted.GeneralComments);
        Assert.EndsWith("inspection.", converted.GeneralComments);
        Assert.StartsWith("Ja", converted.Metadata.FormSentTo);
        Assert.EndsWith("or", converted.Metadata.FormSentTo);
        Assert.Equal(new DateOnly(2024, 4, 12), converted.Metadata.Date.Date); 
        Assert.Equal("12/04/2024", converted.Metadata.Date.RawDate);
        Assert.Equal("Abstraction Flowmeter", converted.MeasurementDetails.MeterMake);
        Assert.StartsWith("V", converted.MeasurementDetails.SerialNumber);
        Assert.EndsWith("2", converted.MeasurementDetails.SerialNumber);
        Assert.Equal("4,714,612", converted.MeasurementDetails.Reading);
        Assert.Equal("m3", converted.MeasurementDetails.Units);
        Assert.Equal("N/A", converted.MeasurementDetails.Other);
        Assert.Equal("N/A", converted.MeasurementDetails.CertificatesOrRecordsAvailableFor);
        Assert.Equal(new DateOnly(2021, 6, 30), converted.MeasurementDetails.DateOfCertificateOrRecord.Date);
        Assert.Equal("30/06/2021", converted.MeasurementDetails.DateOfCertificateOrRecord.RawDate);
        Assert.Equal("Yes", converted.MeasurementDetails.Calibration);
        Assert.Equal("No", converted.MeasurementDetails.Conformance);
        Assert.Equal("Yes", converted.MeasurementDetails.FlowVerification);
        Assert.Equal("Yes", converted.MeasurementDetails.MeterVerification);
        Assert.StartsWith("12", converted.LicenceNumber);
        Assert.EndsWith("8", converted.LicenceNumber);
        Assert.Equal("Less Critical", converted.InspectionClass);
        Assert.StartsWith("Sou", converted.Address.NameAndAddress);
        Assert.EndsWith("NX", converted.Address.NameAndAddress);
        Assert.StartsWith("07", converted.Address.TelephoneNumber);
        Assert.EndsWith("86", converted.Address.TelephoneNumber);
        Assert.StartsWith("Lyn", converted.Address.SiteAddress);
        Assert.EndsWith("JQ", converted.Address.SiteAddress);
        Assert.StartsWith("Ja", converted.MetWith.Name);
        Assert.EndsWith("or", converted.MetWith.Name);
        Assert.Equal("Flow Measurement Coordinator", converted.MetWith.Position);
        Assert.StartsWith("Ar", converted.InspectingOfficer);
        Assert.EndsWith("an", converted.InspectingOfficer);
        Assert.Empty(converted.Images);

        var expectedDateTime = new DateTime(2024, 3, 4);
        expectedDateTime = expectedDateTime.AddHours(11);
        expectedDateTime = expectedDateTime.AddMinutes(20);
            
        Assert.Equal(expectedDateTime, converted.InspectionDate.DateTime);
        Assert.Equal("04/03/2024", converted.InspectionDate.RawDate);
        Assert.Equal("11:20", converted.InspectionDate.RawTime);
    }
    
    [Fact]
    public async Task WhenWR51_POCA_2_ThenGood()
    {
        // Arrange
        const string filename = "WR51__1343025G107__dummy.pdf";

        // Act
        var (matchesResult, dmsFileData) = await GetMatchesAsync(filename);
        var resultFull = matchesResult;
        
        var sourceOfSupply = resultFull.Matches!.First(m => m.LabelGroupName == "SourceOfSupply");
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "PointOfAbstraction");
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("In", pointOfAbstraction.Text[0].Text);
        
        var meansOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfAbstraction");
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches!.First(m => m.LabelGroupName == "Purposes");
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches!.First(m => m.LabelGroupName == "Period");
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches!.First(m => m.LabelGroupName == "Quantities");
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfMeasurement");
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches!.First(m => m.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches!.First(m => m.LabelGroupName == "ProvisionOfInformation");
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("Not", provisionOfInformation.Text[0].Text);
        
        var specialConditions = resultFull.Matches!.First(m => m.LabelGroupName == "SpecialConditions");
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches!.First(m => m.LabelGroupName == "Land");
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches!.First(m => m.LabelGroupName == "ChargingFactors");
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches!.First(m => m.LabelGroupName == "OtherProvisions");
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches!.First(m => m.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("13/43/025/G/107", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches!.First(m => m.LabelGroupName == "MetWith");
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Mr", metWith.Text[0].Text);
        Assert.EndsWith("ey", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches!.First(m => m.LabelGroupName == "InspectingOfficer");
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("Be", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("re", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches!.First(m => m.LabelGroupName == "SiteAddress");
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.Equal("Same as avove", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionClass");
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches!.First(m => m.LabelGroupName == "TelephoneNumber");
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("72", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches!.First(m => m.LabelGroupName == "Position");
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Owner and Farm Manager", position.Text[0].Text);
        
        var time = resultFull.Matches!.First(m => m.LabelGroupName == "Time");
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("10:00", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches!.First(m => m.LabelGroupName == "NameAndAddress");
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(5, nameAndAddress.Text.Count);
        Assert.StartsWith("Co", nameAndAddress.Text[0].Text);
        Assert.StartsWith("Wi", nameAndAddress.Text[4].Text);
        Assert.EndsWith("e", nameAndAddress.Text[4].Text);
        
        var meterMake = resultFull.Matches!.First(m => m.LabelGroupName == "MeterMake");
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("Farmer", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches!.First(m => m.LabelGroupName == "SerialNumber");
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("34", serialNumber.Text[0].Text);
        Assert.EndsWith("3", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches!.First(m => m.LabelGroupName == "Reading");
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("77668", reading.Text[0].Text);
        
        var units = resultFull.Matches!.First(m => m.LabelGroupName == "Units");
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("cubic meters", units.Text[0].Text);
        
        var other = resultFull.Matches!.First(m => m.LabelGroupName == "Other");
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches!.First(m => m.LabelGroupName == "CertificatesOfRecords");
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches!.First(m => m.LabelGroupName == "DateOfCertification");
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("14/04/2022", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches!.First(m => m.LabelGroupName == "Calibration");
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("Yes", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches!.First(m => m.LabelGroupName == "Conformance");
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches!.First(m => m.LabelGroupName == "FlowVerification");
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("No", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches!.First(m => m.LabelGroupName == "MeterVerification");
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches!.First(m => m.LabelGroupName == "WhereKept");
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches!.First(m => m.LabelGroupName == "FormSentTo");
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Mr", formSentTo.Text[0].Text);
        Assert.EndsWith("on", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches!.First(m => m.LabelGroupName == "Date");
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("01/02/2017", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentTemplateVersion");
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var documentHeader = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentHeader");
        Assert.NotNull(documentHeader);
        Assert.Equal("DocumentHeader", documentHeader.LabelGroupName);
        Assert.Single(documentHeader.Text);
        Assert.Equal("51", documentHeader.Text[0].Text);
        
        var generalComments = resultFull.Matches!.First(m => m.LabelGroupName == "GeneralComments");
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(15, generalComments.Text.Count);
        Assert.StartsWith("The bore", generalComments.Text[0].Text);
        Assert.EndsWith("to", generalComments.Text[0].Text);
        Assert.StartsWith("weeks", generalComments.Text[14].Text);
        Assert.EndsWith("invoice.", generalComments.Text[14].Text);
        
        var maintenance = resultFull.Matches!.First(m => m.LabelGroupName == "MaintenanceLine");
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
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
        
        var readingsTaken = resultFull.Matches!.First(m => m.LabelGroupName == "ReadingsTakenLine");
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
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
        
        var inspectionDate = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionDate");
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
        var (matchesResult, dmsFileData) = await GetMatchesAsync(filename);
        var resultFull = matchesResult;
        
        var sourceOfSupply = resultFull.Matches!.First(m => m.LabelGroupName == "SourceOfSupply");
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "PointOfAbstraction");
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("In", pointOfAbstraction.Text[0].Text);
        
        var meansOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfAbstraction");
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches!.First(m => m.LabelGroupName == "Purposes");
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches!.First(m => m.LabelGroupName == "Period");
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches!.First(m => m.LabelGroupName == "Quantities");
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfMeasurement");
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches!.First(m => m.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches!.First(m => m.LabelGroupName == "ProvisionOfInformation");
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("In", provisionOfInformation.Text[0].Text);
        
        var specialConditions = resultFull.Matches!.First(m => m.LabelGroupName == "SpecialConditions");
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches!.First(m => m.LabelGroupName == "Land");
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches!.First(m => m.LabelGroupName == "ChargingFactors");
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches!.First(m => m.LabelGroupName == "OtherProvisions");
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches!.First(m => m.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("13/43/026/G/118", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches!.First(m => m.LabelGroupName == "MetWith");
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Mr", metWith.Text[0].Text);
        Assert.EndsWith("gs", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches!.First(m => m.LabelGroupName == "InspectingOfficer");
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("Be", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("re", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches!.First(m => m.LabelGroupName == "SiteAddress");
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.StartsWith("Wi", siteAddress.Text[0].Text);
        Assert.EndsWith("on", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionClass");
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("CR", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches!.First(m => m.LabelGroupName == "TelephoneNumber");
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("01", telephoneNumber.Text[0].Text);
        Assert.EndsWith("62", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches!.First(m => m.LabelGroupName == "Position");
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Farm Owner", position.Text[0].Text);
        
        var time = resultFull.Matches!.First(m => m.LabelGroupName == "Time");
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("10:00", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches!.First(m => m.LabelGroupName == "NameAndAddress");
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(1, nameAndAddress.Text.Count);
        Assert.StartsWith("Wi", nameAndAddress.Text[0].Text);
        Assert.EndsWith("on", nameAndAddress.Text[0].Text);
        
        var meterMake = resultFull.Matches!.First(m => m.LabelGroupName == "MeterMake");
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("Kent", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches!.First(m => m.LabelGroupName == "SerialNumber");
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("13", serialNumber.Text[0].Text);
        Assert.EndsWith("7", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches!.First(m => m.LabelGroupName == "Reading");
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("1154546", reading.Text[0].Text);
        
        var units = resultFull.Matches!.First(m => m.LabelGroupName == "Units");
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("cubic meter", units.Text[0].Text);
        
        var other = resultFull.Matches!.First(m => m.LabelGroupName == "Other");
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches!.First(m => m.LabelGroupName == "CertificatesOfRecords");
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches!.First(m => m.LabelGroupName == "DateOfCertification");
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("21/08/2019", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches!.First(m => m.LabelGroupName == "Calibration");
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("No", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches!.First(m => m.LabelGroupName == "Conformance");
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches!.First(m => m.LabelGroupName == "FlowVerification");
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches!.First(m => m.LabelGroupName == "MeterVerification");
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches!.First(m => m.LabelGroupName == "WhereKept");
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches!.First(m => m.LabelGroupName == "FormSentTo");
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Mr", formSentTo.Text[0].Text);
        Assert.EndsWith("on", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches!.First(m => m.LabelGroupName == "Date");
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("12/09/2016", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentTemplateVersion");
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var documentHeader = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentHeader");
        Assert.NotNull(documentHeader);
        Assert.Equal("DocumentHeader", documentHeader.LabelGroupName);
        Assert.Single(documentHeader.Text);
        Assert.Equal("51", documentHeader.Text[0].Text);
        
        var generalComments = resultFull.Matches!.First(m => m.LabelGroupName == "GeneralComments");
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(12, generalComments.Text.Count);
        Assert.StartsWith("There are", generalComments.Text[0].Text);
        Assert.EndsWith("away", generalComments.Text[0].Text);
        Assert.StartsWith("purpose", generalComments.Text[11].Text);
        Assert.EndsWith("chickens.", generalComments.Text[11].Text);
        
        var maintenance = resultFull.Matches!.First(m => m.LabelGroupName == "MaintenanceLine");
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
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
        
        var readingsTaken = resultFull.Matches!.First(m => m.LabelGroupName == "ReadingsTakenLine");
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
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
        
        var inspectionDate = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionDate");
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
        var (matchesResult, dmsFileData) = await GetMatchesAsync(filename);
        var resultFull = matchesResult;
        
        var sourceOfSupply = resultFull.Matches!.First(m => m.LabelGroupName == "SourceOfSupply");
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "PointOfAbstraction");
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("In", pointOfAbstraction.Text[0].Text);
        
        var meansOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfAbstraction");
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches!.First(m => m.LabelGroupName == "Purposes");
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches!.First(m => m.LabelGroupName == "Period");
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches!.First(m => m.LabelGroupName == "Quantities");
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfMeasurement");
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches!.First(m => m.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches!.First(m => m.LabelGroupName == "ProvisionOfInformation");
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("In", provisionOfInformation.Text[0].Text);
        
        var specialConditions = resultFull.Matches!.First(m => m.LabelGroupName == "SpecialConditions");
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches!.First(m => m.LabelGroupName == "Land");
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches!.First(m => m.LabelGroupName == "ChargingFactors");
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches!.First(m => m.LabelGroupName == "OtherProvisions");
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches!.First(m => m.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("13/43/026/S/047", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches!.First(m => m.LabelGroupName == "MetWith");
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Mr", metWith.Text[0].Text);
        Assert.EndsWith("gs", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches!.First(m => m.LabelGroupName == "InspectingOfficer");
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("Be", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("re", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches!.First(m => m.LabelGroupName == "SiteAddress");
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.Equal("Same as above", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionClass");
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Highly Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches!.First(m => m.LabelGroupName == "TelephoneNumber");
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("97", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches!.First(m => m.LabelGroupName == "Position");
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Farm Owner", position.Text[0].Text);
        
        var time = resultFull.Matches!.First(m => m.LabelGroupName == "Time");
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("10:00", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches!.First(m => m.LabelGroupName == "NameAndAddress");
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(6, nameAndAddress.Text.Count);
        Assert.StartsWith("San", nameAndAddress.Text[0].Text);
        Assert.EndsWith("JZ", nameAndAddress.Text[5].Text);
        
        var meterMake = resultFull.Matches!.First(m => m.LabelGroupName == "MeterMake");
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("Zenner", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches!.First(m => m.LabelGroupName == "SerialNumber");
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("34", serialNumber.Text[0].Text);
        Assert.EndsWith("6", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches!.First(m => m.LabelGroupName == "Reading");
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("45545", reading.Text[0].Text);
        
        var units = resultFull.Matches!.First(m => m.LabelGroupName == "Units");
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("cubic meter", units.Text[0].Text);
        
        var other = resultFull.Matches!.First(m => m.LabelGroupName == "Other");
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches!.First(m => m.LabelGroupName == "CertificatesOfRecords");
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches!.First(m => m.LabelGroupName == "DateOfCertification");
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("21/08/2019", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches!.First(m => m.LabelGroupName == "Calibration");
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("No", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches!.First(m => m.LabelGroupName == "Conformance");
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches!.First(m => m.LabelGroupName == "FlowVerification");
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches!.First(m => m.LabelGroupName == "MeterVerification");
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches!.First(m => m.LabelGroupName == "WhereKept");
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches!.First(m => m.LabelGroupName == "FormSentTo");
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("To", formSentTo.Text[0].Text);
        Assert.EndsWith("gs", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches!.First(m => m.LabelGroupName == "Date");
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("07/02/2017", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentTemplateVersion");
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var documentHeader = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentHeader");
        Assert.NotNull(documentHeader);
        Assert.Equal("DocumentHeader", documentHeader.LabelGroupName);
        Assert.Single(documentHeader.Text);
        Assert.Equal("51", documentHeader.Text[0].Text);
        
        var generalComments = resultFull.Matches!.First(m => m.LabelGroupName == "GeneralComments");
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(16, generalComments.Text.Count);
        Assert.StartsWith("Abstraction", generalComments.Text[0].Text);
        Assert.EndsWith("sources", generalComments.Text[0].Text);
        Assert.StartsWith("them", generalComments.Text[15].Text);
        Assert.EndsWith("payment.", generalComments.Text[15].Text);
        
        var maintenance = resultFull.Matches!.First(m => m.LabelGroupName == "MaintenanceLine");
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
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
        
        var readingsTaken = resultFull.Matches!.First(m => m.LabelGroupName == "ReadingsTakenLine");
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
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
        
        var inspectionDate = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionDate");
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
        var (matchesResult, dmsFileData) = await GetMatchesAsync(filename);
        var resultFull = matchesResult;
        
        var sourceOfSupply = resultFull.Matches!.First(m => m.LabelGroupName == "SourceOfSupply");
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "PointOfAbstraction");
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("In", pointOfAbstraction.Text[0].Text);
        
        var meansOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfAbstraction");
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches!.First(m => m.LabelGroupName == "Purposes");
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches!.First(m => m.LabelGroupName == "Period");
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches!.First(m => m.LabelGroupName == "Quantities");
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfMeasurement");
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches!.First(m => m.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches!.First(m => m.LabelGroupName == "ProvisionOfInformation");
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("In", provisionOfInformation.Text[0].Text);
        
        var specialConditions = resultFull.Matches!.First(m => m.LabelGroupName == "SpecialConditions");
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches!.First(m => m.LabelGroupName == "Land");
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches!.First(m => m.LabelGroupName == "ChargingFactors");
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches!.First(m => m.LabelGroupName == "OtherProvisions");
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches!.First(m => m.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("11/42/22.2/46", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches!.First(m => m.LabelGroupName == "MetWith");
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Ti", metWith.Text[0].Text);
        Assert.EndsWith("am", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches!.First(m => m.LabelGroupName == "InspectingOfficer");
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("St", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("rt", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches!.First(m => m.LabelGroupName == "SiteAddress");
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.Equal("Same as above", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionClass");
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Highly Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches!.First(m => m.LabelGroupName == "TelephoneNumber");
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("97", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches!.First(m => m.LabelGroupName == "Position");
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Farm Owner", position.Text[0].Text);
        
        var time = resultFull.Matches!.First(m => m.LabelGroupName == "Time");
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("12:10", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches!.First(m => m.LabelGroupName == "NameAndAddress");
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(1, nameAndAddress.Text.Count);
        Assert.StartsWith("J G", nameAndAddress.Text[0].Text);
        Assert.EndsWith("DF", nameAndAddress.Text[0].Text);
        
        var meterMake = resultFull.Matches!.First(m => m.LabelGroupName == "MeterMake");
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("Zenner", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches!.First(m => m.LabelGroupName == "SerialNumber");
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("34", serialNumber.Text[0].Text);
        Assert.EndsWith("6", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches!.First(m => m.LabelGroupName == "Reading");
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("45545", reading.Text[0].Text);
        
        var units = resultFull.Matches!.First(m => m.LabelGroupName == "Units");
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("cubic meter", units.Text[0].Text);
        
        var other = resultFull.Matches!.First(m => m.LabelGroupName == "Other");
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches!.First(m => m.LabelGroupName == "CertificatesOfRecords");
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches!.First(m => m.LabelGroupName == "DateOfCertification");
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("21/08/2019", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches!.First(m => m.LabelGroupName == "Calibration");
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("No", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches!.First(m => m.LabelGroupName == "Conformance");
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches!.First(m => m.LabelGroupName == "FlowVerification");
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches!.First(m => m.LabelGroupName == "MeterVerification");
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches!.First(m => m.LabelGroupName == "WhereKept");
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches!.First(m => m.LabelGroupName == "FormSentTo");
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Ti", formSentTo.Text[0].Text);
        Assert.EndsWith("am", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches!.First(m => m.LabelGroupName == "Date");
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("26/06/2023", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentTemplateVersion");
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var documentHeader = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentHeader");
        Assert.NotNull(documentHeader);
        Assert.Equal("DocumentHeader", documentHeader.LabelGroupName);
        Assert.Single(documentHeader.Text);
        Assert.Equal("51", documentHeader.Text[0].Text);
        
        var generalComments = resultFull.Matches!.First(m => m.LabelGroupName == "GeneralComments");
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(4, generalComments.Text.Count);
        Assert.StartsWith("This", generalComments.Text[0].Text);
        Assert.EndsWith("taken.", generalComments.Text[0].Text);
        Assert.StartsWith("the", generalComments.Text[3].Text);
        Assert.EndsWith("volume.", generalComments.Text[3].Text);
        
        var maintenance = resultFull.Matches!.First(m => m.LabelGroupName == "MaintenanceLine");
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
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
        
        var readingsTaken = resultFull.Matches!.First(m => m.LabelGroupName == "ReadingsTakenLine");
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
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
        
        var inspectionDate = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionDate");
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
        var (matchesResult, dmsFileData) = await GetMatchesAsync(filename);
        var resultFull = matchesResult;
        
        var sourceOfSupply = resultFull.Matches!.First(m => m.LabelGroupName == "SourceOfSupply");
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "PointOfAbstraction");
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("In", pointOfAbstraction.Text[0].Text);
        
        var meansOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfAbstraction");
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches!.First(m => m.LabelGroupName == "Purposes");
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches!.First(m => m.LabelGroupName == "Period");
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches!.First(m => m.LabelGroupName == "Quantities");
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfMeasurement");
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches!.First(m => m.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches!.First(m => m.LabelGroupName == "ProvisionOfInformation");
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("In", provisionOfInformation.Text[0].Text);
        
        var specialConditions = resultFull.Matches!.First(m => m.LabelGroupName == "SpecialConditions");
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches!.First(m => m.LabelGroupName == "Land");
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches!.First(m => m.LabelGroupName == "ChargingFactors");
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches!.First(m => m.LabelGroupName == "OtherProvisions");
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches!.First(m => m.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("10/41/521202", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches!.First(m => m.LabelGroupName == "MetWith");
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Ed", metWith.Text[0].Text);
        Assert.EndsWith("es", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches!.First(m => m.LabelGroupName == "InspectingOfficer");
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("Ja", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("ll", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches!.First(m => m.LabelGroupName == "SiteAddress");
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.StartsWith("Spri", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionClass");
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Highly Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches!.First(m => m.LabelGroupName == "TelephoneNumber");
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        // This document's raw PDF text has a real kerning artefact - a stray space
        // between every digit ("0 7 7 9 4 2 1 8 2 97"), collapsed at the schema level
        // by WrInspectionReportSchemaConverter. This checks the raw match, so it needs
        // to account for the same artefact rather than the cleaned-up form.
        var rawTelephoneNumberDigitsOnly = telephoneNumber.Text[0].Text.Replace(" ", string.Empty);
        Assert.StartsWith("07", rawTelephoneNumberDigitsOnly);
        Assert.EndsWith("97", rawTelephoneNumberDigitsOnly);
        
        var position = resultFull.Matches!.First(m => m.LabelGroupName == "Position");
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal(2, position.Text.Count);
        Assert.Equal("Head of Irrigation and Senior Farm", position.Text[0].Text);
        Assert.Equal("Manager respectively", position.Text[1].Text);
        
        var time = resultFull.Matches!.First(m => m.LabelGroupName == "Time");
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("10:00am", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches!.First(m => m.LabelGroupName == "NameAndAddress");
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(1, nameAndAddress.Text.Count);
        Assert.StartsWith("Ha", nameAndAddress.Text[0].Text);
        Assert.EndsWith("UJ", nameAndAddress.Text[0].Text);
        
        var meterMake = resultFull.Matches!.First(m => m.LabelGroupName == "MeterMake");
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("No meter on site", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches!.First(m => m.LabelGroupName == "SerialNumber");
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.Equal("N/A", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches!.First(m => m.LabelGroupName == "Reading");
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("N/A", reading.Text[0].Text);
        
        var units = resultFull.Matches!.First(m => m.LabelGroupName == "Units");
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("N/A", units.Text[0].Text);
        
        var other = resultFull.Matches!.First(m => m.LabelGroupName == "Other");
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches!.First(m => m.LabelGroupName == "CertificatesOfRecords");
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches!.First(m => m.LabelGroupName == "DateOfCertification");
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("10/06/2020", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches!.First(m => m.LabelGroupName == "Calibration");
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("No", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches!.First(m => m.LabelGroupName == "Conformance");
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches!.First(m => m.LabelGroupName == "FlowVerification");
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches!.First(m => m.LabelGroupName == "MeterVerification");
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches!.First(m => m.LabelGroupName == "WhereKept");
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches!.First(m => m.LabelGroupName == "FormSentTo");
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Ri", formSentTo.Text[0].Text);
        Assert.EndsWith("es", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches!.First(m => m.LabelGroupName == "Date");
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("11/12/2024", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentTemplateVersion");
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var documentHeader = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentHeader");
        Assert.NotNull(documentHeader);
        Assert.Equal("DocumentHeader", documentHeader.LabelGroupName);
        Assert.Single(documentHeader.Text);
        Assert.Equal("51", documentHeader.Text[0].Text);
        
        var generalComments = resultFull.Matches!.First(m => m.LabelGroupName == "GeneralComments");
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(19, generalComments.Text.Count);
        Assert.StartsWith("Licence", generalComments.Text[0].Text);
        Assert.EndsWith("per", generalComments.Text[0].Text);
        Assert.StartsWith("There", generalComments.Text[18].Text);
        Assert.EndsWith("time.", generalComments.Text[18].Text);
        
        var maintenance = resultFull.Matches!.First(m => m.LabelGroupName == "MaintenanceLine");
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
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
        
        var readingsTaken = resultFull.Matches!.First(m => m.LabelGroupName == "ReadingsTakenLine");
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
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
        
        var inspectionDate = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionDate");
        
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
        var (matchesResult, dmsFileData) = await GetMatchesAsync(filename);
        var resultFull = matchesResult;
        
        var sourceOfSupply = resultFull.Matches!.First(m => m.LabelGroupName == "SourceOfSupply");
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "PointOfAbstraction");
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("In", pointOfAbstraction.Text[0].Text);
        
        var meansOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfAbstraction");
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches!.First(m => m.LabelGroupName == "Purposes");
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches!.First(m => m.LabelGroupName == "Period");
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches!.First(m => m.LabelGroupName == "Quantities");
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfMeasurement");
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches!.First(m => m.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches!.First(m => m.LabelGroupName == "ProvisionOfInformation");
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("In", provisionOfInformation.Text[0].Text);
        
        var specialConditions = resultFull.Matches!.First(m => m.LabelGroupName == "SpecialConditions");
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches!.First(m => m.LabelGroupName == "Land");
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches!.First(m => m.LabelGroupName == "ChargingFactors");
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches!.First(m => m.LabelGroupName == "OtherProvisions");
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches!.First(m => m.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("10/41/531327", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches!.First(m => m.LabelGroupName == "MetWith");
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Ke", metWith.Text[0].Text);
        Assert.EndsWith("rr", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches!.First(m => m.LabelGroupName == "InspectingOfficer");
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("Ar", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("ll", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches!.First(m => m.LabelGroupName == "SiteAddress");
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.StartsWith("Pa", siteAddress.Text[0].Text);
        Assert.EndsWith("am", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionClass");
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches!.First(m => m.LabelGroupName == "TelephoneNumber");
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.Equal(2, telephoneNumber.Text.Count);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("ma", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches!.First(m => m.LabelGroupName == "Position");
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal(2, position.Text.Count);
        Assert.Equal("Head of Irrigation and Senior Farm", position.Text[0].Text);
        Assert.Equal("Manager respectively", position.Text[1].Text);
        
        var time = resultFull.Matches!.First(m => m.LabelGroupName == "Time");
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("09:45", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches!.First(m => m.LabelGroupName == "NameAndAddress");
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(2, nameAndAddress.Text.Count);
        Assert.StartsWith("La", nameAndAddress.Text[0].Text);
        Assert.EndsWith("EH", nameAndAddress.Text[1].Text);
        
        var meterMake = resultFull.Matches!.First(m => m.LabelGroupName == "MeterMake");
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("Technidro", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches!.First(m => m.LabelGroupName == "SerialNumber");
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("R", serialNumber.Text[0].Text);
        Assert.EndsWith("8", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches!.First(m => m.LabelGroupName == "Reading");
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("51516", reading.Text[0].Text);
        
        var units = resultFull.Matches!.First(m => m.LabelGroupName == "Units");
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("m3", units.Text[0].Text);
        
        var other = resultFull.Matches!.First(m => m.LabelGroupName == "Other");
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches!.First(m => m.LabelGroupName == "CertificatesOfRecords");
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches!.First(m => m.LabelGroupName == "DateOfCertification");
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("10/06/2020", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches!.First(m => m.LabelGroupName == "Calibration");
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("No", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches!.First(m => m.LabelGroupName == "Conformance");
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("Yes", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches!.First(m => m.LabelGroupName == "FlowVerification");
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches!.First(m => m.LabelGroupName == "MeterVerification");
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches!.First(m => m.LabelGroupName == "WhereKept");
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches!.First(m => m.LabelGroupName == "FormSentTo");
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Ge", formSentTo.Text[0].Text);
        Assert.EndsWith("nd", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches!.First(m => m.LabelGroupName == "Date");
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("27/12/2023", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentTemplateVersion");
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var documentHeader = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentHeader");
        Assert.NotNull(documentHeader);
        Assert.Equal("DocumentHeader", documentHeader.LabelGroupName);
        Assert.Single(documentHeader.Text);
        Assert.Equal("51", documentHeader.Text[0].Text);
        
        var generalComments = resultFull.Matches!.First(m => m.LabelGroupName == "GeneralComments");
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(2, generalComments.Text.Count);
        Assert.StartsWith("Summer", generalComments.Text[0].Text);
        Assert.EndsWith("licence.", generalComments.Text[0].Text);
        Assert.StartsWith("Same", generalComments.Text[1].Text);
        Assert.EndsWith("licence).", generalComments.Text[1].Text);
        
        var maintenance = resultFull.Matches!.First(m => m.LabelGroupName == "MaintenanceLine");
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
        Assert.Equal("Maintenance: Yes Frequency: Monthly By whom: JP", maintenance.Text[0].Text);
        Assert.Equal(3, maintenance.SubResults.Count);

        var maintenanceSubLabel = maintenance.SubResults[0];
        Assert.NotNull(maintenanceSubLabel);
        Assert.Equal("MaintenanceLineMaintenance", maintenanceSubLabel.MatchedLabelName);
        Assert.Equal("Yes", maintenanceSubLabel.Text[0].Text);
        
        var frequencySubLabel = maintenance.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal("MaintenanceLineFrequency", frequencySubLabel.MatchedLabelName);
        Assert.Equal("Monthly", frequencySubLabel.Text[0].Text);
        
        var byWhomSubLabel = maintenance.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal("MaintenanceLineByWhom", byWhomSubLabel.MatchedLabelName);
        Assert.Equal("JP", byWhomSubLabel.Text[0].Text);
        
        var readingsTaken = resultFull.Matches!.First(m => m.LabelGroupName == "ReadingsTakenLine");
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
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
        
        var inspectionDate = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionDate");
        Assert.NotNull(inspectionDate);
        Assert.Equal("InspectionDate", inspectionDate.LabelGroupName);
        Assert.Equal("18/12/2023", inspectionDate.Text[0].Text);
    }
    
   [Fact]
    public async Task WhenWR51_POCA_8_ThenGood()
    {
        // Arrange
        const string filename = "WR51__1041531329__dummy.pdf";

        // Act
        var (matchesResult, dmsFileData) = await GetMatchesAsync(filename);
        var resultFull = matchesResult;
        
        var sourceOfSupply = resultFull.Matches!.First(m => m.LabelGroupName == "SourceOfSupply");
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "PointOfAbstraction");
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("In", pointOfAbstraction.Text[0].Text);
        
        var meansOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfAbstraction");
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches!.First(m => m.LabelGroupName == "Purposes");
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches!.First(m => m.LabelGroupName == "Period");
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches!.First(m => m.LabelGroupName == "Quantities");
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfMeasurement");
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches!.First(m => m.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches!.First(m => m.LabelGroupName == "ProvisionOfInformation");
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("Not", provisionOfInformation.Text[0].Text);
        
        var specialConditions = resultFull.Matches!.First(m => m.LabelGroupName == "SpecialConditions");
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("N/A", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches!.First(m => m.LabelGroupName == "Land");
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches!.First(m => m.LabelGroupName == "ChargingFactors");
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches!.First(m => m.LabelGroupName == "OtherProvisions");
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches!.First(m => m.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("10/41/531329", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches!.First(m => m.LabelGroupName == "MetWith");
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Ke", metWith.Text[0].Text);
        Assert.EndsWith("rr", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches!.First(m => m.LabelGroupName == "InspectingOfficer");
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("Ja", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("on", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches!.First(m => m.LabelGroupName == "SiteAddress");
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.StartsWith("Ru", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionClass");
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches!.First(m => m.LabelGroupName == "TelephoneNumber");
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("86", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches!.First(m => m.LabelGroupName == "Position");
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal(2, position.Text.Count);
        Assert.Equal("Head of Irrigation and Senior Farm", position.Text[0].Text);
        Assert.Equal("Manager respectively", position.Text[1].Text);
        
        var time = resultFull.Matches!.First(m => m.LabelGroupName == "Time");
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("10:00", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches!.First(m => m.LabelGroupName == "NameAndAddress");
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(2, nameAndAddress.Text.Count);
        Assert.StartsWith("La", nameAndAddress.Text[0].Text);
        Assert.EndsWith("NW", nameAndAddress.Text[1].Text);
        
        var meterMake = resultFull.Matches!.First(m => m.LabelGroupName == "MeterMake");
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("Technidro", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches!.First(m => m.LabelGroupName == "SerialNumber");
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("R", serialNumber.Text[0].Text);
        Assert.EndsWith("1", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches!.First(m => m.LabelGroupName == "Reading");
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("4,714,456", reading.Text[0].Text);
        
        var units = resultFull.Matches!.First(m => m.LabelGroupName == "Units");
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("m3", units.Text[0].Text);
        
        var other = resultFull.Matches!.First(m => m.LabelGroupName == "Other");
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches!.First(m => m.LabelGroupName == "CertificatesOfRecords");
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches!.First(m => m.LabelGroupName == "DateOfCertification");
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("10/06/2020", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches!.First(m => m.LabelGroupName == "Calibration");
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("No", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches!.First(m => m.LabelGroupName == "Conformance");
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("Yes", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches!.First(m => m.LabelGroupName == "FlowVerification");
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches!.First(m => m.LabelGroupName == "MeterVerification");
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches!.First(m => m.LabelGroupName == "WhereKept");
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches!.First(m => m.LabelGroupName == "FormSentTo");
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Ge", formSentTo.Text[0].Text);
        Assert.EndsWith("nd", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches!.First(m => m.LabelGroupName == "Date");
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("27/12/2023", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentTemplateVersion");
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var documentHeader = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentHeader");
        Assert.NotNull(documentHeader);
        Assert.Equal("DocumentHeader", documentHeader.LabelGroupName);
        Assert.Single(documentHeader.Text);
        Assert.Equal("51", documentHeader.Text[0].Text);
        
        var generalComments = resultFull.Matches!.First(m => m.LabelGroupName == "GeneralComments");
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(3, generalComments.Text.Count);
        Assert.StartsWith("New", generalComments.Text[0].Text);
        Assert.EndsWith("currently.", generalComments.Text[0].Text);
        Assert.StartsWith("All", generalComments.Text[2].Text);
        Assert.EndsWith("limit.", generalComments.Text[2].Text);
        
        var maintenance = resultFull.Matches!.First(m => m.LabelGroupName == "MaintenanceLine");
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
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
        
        var readingsTaken = resultFull.Matches!.First(m => m.LabelGroupName == "ReadingsTakenLine");
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
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
        
        var inspectionDate = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionDate");
        Assert.NotNull(inspectionDate);
        Assert.Equal("InspectionDate", inspectionDate.LabelGroupName);
        Assert.Equal("18/12/2023", inspectionDate.Text[0].Text);
    }

   [Fact]
    public async Task WhenWR51_POCA_9_ThenGood()
    {
        // Arrange
        const string filename = "WR51__2569020001__dummy.pdf";

        // Act
        var (matchesResult, dmsFileData) = await GetMatchesAsync(filename);
        var resultFull = matchesResult;
        
        var sourceOfSupply = resultFull.Matches!.First(m => m.LabelGroupName == "SourceOfSupply");
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "PointOfAbstraction");
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("In", pointOfAbstraction.Text[0].Text);
        
        var meansOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfAbstraction");
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches!.First(m => m.LabelGroupName == "Purposes");
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches!.First(m => m.LabelGroupName == "Period");
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches!.First(m => m.LabelGroupName == "Quantities");
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfMeasurement");
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches!.First(m => m.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches!.First(m => m.LabelGroupName == "ProvisionOfInformation");
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("Not", provisionOfInformation.Text[0].Text);
        
        var specialConditions = resultFull.Matches!.First(m => m.LabelGroupName == "SpecialConditions");
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("N/A", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches!.First(m => m.LabelGroupName == "Land");
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches!.First(m => m.LabelGroupName == "ChargingFactors");
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches!.First(m => m.LabelGroupName == "OtherProvisions");
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches!.First(m => m.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("2569020001", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches!.First(m => m.LabelGroupName == "MetWith");
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Ni", metWith.Text[0].Text);
        Assert.EndsWith("ey", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches!.First(m => m.LabelGroupName == "InspectingOfficer");
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("Ma", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("on", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches!.First(m => m.LabelGroupName == "SiteAddress");
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.Equal("Same as above", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionClass");
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Less Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches!.First(m => m.LabelGroupName == "TelephoneNumber");
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("86", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches!.First(m => m.LabelGroupName == "Position");
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Course Manager", position.Text[0].Text);
        
        var time = resultFull.Matches!.First(m => m.LabelGroupName == "Time");
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("10:30", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches!.First(m => m.LabelGroupName == "NameAndAddress");
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(2, nameAndAddress.Text.Count);
        Assert.StartsWith("St", nameAndAddress.Text[0].Text);
        Assert.EndsWith("AY", nameAndAddress.Text[1].Text);
        
        var meterMake = resultFull.Matches!.First(m => m.LabelGroupName == "MeterMake");
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal("mega", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches!.First(m => m.LabelGroupName == "SerialNumber");
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.StartsWith("LX", serialNumber.Text[0].Text);
        Assert.EndsWith("5A", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches!.First(m => m.LabelGroupName == "Reading");
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("1876", reading.Text[0].Text);
        
        var units = resultFull.Matches!.First(m => m.LabelGroupName == "Units");
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("m3", units.Text[0].Text);
        
        var other = resultFull.Matches!.First(m => m.LabelGroupName == "Other");
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches!.First(m => m.LabelGroupName == "CertificatesOfRecords");
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches!.First(m => m.LabelGroupName == "DateOfCertification");
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("14/04/2022", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches!.First(m => m.LabelGroupName == "Calibration");
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("Yes", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches!.First(m => m.LabelGroupName == "Conformance");
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches!.First(m => m.LabelGroupName == "FlowVerification");
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("No", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches!.First(m => m.LabelGroupName == "MeterVerification");
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches!.First(m => m.LabelGroupName == "WhereKept");
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches!.First(m => m.LabelGroupName == "FormSentTo");
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Co", formSentTo.Text[0].Text);
        Assert.EndsWith("uk", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches!.First(m => m.LabelGroupName == "Date");
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("14/04/2022", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentTemplateVersion");
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var documentHeader = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentHeader");
        Assert.NotNull(documentHeader);
        Assert.Equal("DocumentHeader", documentHeader.LabelGroupName);
        Assert.Single(documentHeader.Text);
        Assert.Equal("51", documentHeader.Text[0].Text);
        
        var generalComments = resultFull.Matches!.First(m => m.LabelGroupName == "GeneralComments");
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(10, generalComments.Text.Count);
        Assert.StartsWith("Water", generalComments.Text[0].Text);
        Assert.EndsWith("in", generalComments.Text[0].Text);
        Assert.StartsWith("practicably", generalComments.Text[9].Text);
        Assert.EndsWith("possible.", generalComments.Text[9].Text);
        
        var maintenance = resultFull.Matches!.First(m => m.LabelGroupName == "MaintenanceLine");
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
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
        
        var readingsTaken = resultFull.Matches!.First(m => m.LabelGroupName == "ReadingsTakenLine");
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
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
        
        var inspectionDate = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionDate");
        Assert.NotNull(inspectionDate);
        Assert.Equal("InspectionDate", inspectionDate.LabelGroupName);
        Assert.Equal("13/04/2022", inspectionDate.Text[0].Text);
    }
    
    // Known gap: this document's MeterMake value wraps onto a continuation line ("under
    // Fish Farm RPS") below a row where "Serial number" also happens to sit further along
    // the same line. GetTextBetween finds "Serial number" as the end tag within the
    // label's own row and breaks immediately, so it never looks at the continuation line
    // to see it belongs to this field. Fixing this needs GetTextBetween's line-boundary
    // loop to distinguish "value ends here, unrelated content follows" from "value
    // continues after a same-row marker" - tried once (gating on end-tag-found-within-line
    // + LimitTo.SameColumn), but that turned out to be the *common* shape for these fields,
    // not the rare case, and broke 9 of the other 10 tests in this suite. Needs a genuine
    // redesign (e.g. a position-based check on the continuation line itself), not a patch.
    [Fact]
    public async Task WhenWR51_POCA_10_ThenGood()
    {
        // Arrange
        const string filename = "WR51__SO0420031002__dummy.pdf";

        // Act
        var (matchesResult, dmsFileData) = await GetMatchesAsync(filename);
        var resultFull = matchesResult;
        
        var sourceOfSupply = resultFull.Matches!.First(m => m.LabelGroupName == "SourceOfSupply");
        Assert.NotNull(sourceOfSupply);
        Assert.Equal("SourceOfSupply", sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "PointOfAbstraction");
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal("PointOfAbstraction", pointOfAbstraction.LabelGroupName);
        Assert.Equal("In", pointOfAbstraction.Text[0].Text);
        
        var meansOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfAbstraction");
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal("MeansOfAbstraction", meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches!.First(m => m.LabelGroupName == "Purposes");
        Assert.NotNull(purposes);
        Assert.Equal("Purposes", purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches!.First(m => m.LabelGroupName == "Period");
        Assert.NotNull(period);
        Assert.Equal("Period", period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches!.First(m => m.LabelGroupName == "Quantities");
        Assert.NotNull(quantities);
        Assert.Equal("Quantities", quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches!.First(m => m.LabelGroupName == "MeansOfMeasurement");
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal("MeansOfMeasurement", meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches!.First(m => m.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal("Records", records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches!.First(m => m.LabelGroupName == "ProvisionOfInformation");
        Assert.NotNull(provisionOfInformation);
        Assert.Equal("ProvisionOfInformation", provisionOfInformation.LabelGroupName);
        Assert.Equal("In", provisionOfInformation.Text[0].Text);
        
        var specialConditions = resultFull.Matches!.First(m => m.LabelGroupName == "SpecialConditions");
        Assert.NotNull(specialConditions);
        Assert.Equal("SpecialConditions", specialConditions.LabelGroupName);
        Assert.Equal("Not", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches!.First(m => m.LabelGroupName == "Land");
        Assert.NotNull(land);
        Assert.Equal("Land", land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches!.First(m => m.LabelGroupName == "ChargingFactors");
        Assert.NotNull(chargingFactors);
        Assert.Equal("ChargingFactors", chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches!.First(m => m.LabelGroupName == "OtherProvisions");
        Assert.NotNull(otherProvisions);
        Assert.Equal("OtherProvisions", otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches!.First(m => m.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("LicenceNumber", licenceNumber.LabelGroupName);
        Assert.Equal("SO/042/0031/002", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches!.First(m => m.LabelGroupName == "MetWith");
        Assert.NotNull(metWith);
        Assert.Equal("MetWith", metWith.LabelGroupName);
        Assert.StartsWith("Pe", metWith.Text[0].Text);
        Assert.EndsWith("ie", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches!.First(m => m.LabelGroupName == "InspectingOfficer");
        Assert.NotNull(inspectingOfficer);
        Assert.Equal("InspectingOfficer", inspectingOfficer.LabelGroupName);
        Assert.StartsWith("St", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("rt", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches!.First(m => m.LabelGroupName == "SiteAddress");
        Assert.NotNull(siteAddress);
        Assert.Equal("SiteAddress", siteAddress.LabelGroupName);
        Assert.StartsWith("Avi", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionClass");
        Assert.NotNull(inspectionClass);
        Assert.Equal("InspectionClass", inspectionClass.LabelGroupName);
        Assert.Equal("Highly Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches!.First(m => m.LabelGroupName == "TelephoneNumber");
        Assert.NotNull(telephoneNumber);
        Assert.Equal("TelephoneNumber", telephoneNumber.LabelGroupName);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("97", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches!.First(m => m.LabelGroupName == "Position");
        Assert.NotNull(position);
        Assert.Equal("Position", position.LabelGroupName);
        Assert.Equal("Estates and Fisheries Managers", position.Text[0].Text);
        
        var time = resultFull.Matches!.First(m => m.LabelGroupName == "Time");
        Assert.NotNull(time);
        Assert.Equal("Time", time.LabelGroupName);
        Assert.Equal("10:00am", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches!.First(m => m.LabelGroupName == "NameAndAddress");
        Assert.NotNull(nameAndAddress);
        Assert.Equal("NameAndAddress", nameAndAddress.LabelGroupName);
        Assert.Equal(2, nameAndAddress.Text.Count);
        Assert.StartsWith("St", nameAndAddress.Text[0].Text);
        Assert.EndsWith("AQ", nameAndAddress.Text[1].Text);
        
        var meterMake = resultFull.Matches!.First(m => m.LabelGroupName == "MeterMake");
        Assert.NotNull(meterMake);
        Assert.Equal("MeterMake", meterMake.LabelGroupName);
        Assert.Equal(2, meterMake.Text.Count);
        Assert.Equal("No meter – means of measurement", meterMake.Text[0].Text);
        Assert.Equal("under Fish Farm RPS", meterMake.Text[1].Text);
        
        var serialNumber = resultFull.Matches!.First(m => m.LabelGroupName == "SerialNumber");
        Assert.NotNull(serialNumber);
        Assert.Equal("SerialNumber", serialNumber.LabelGroupName);
        Assert.Equal("N/A", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches!.First(m => m.LabelGroupName == "Reading");
        Assert.NotNull(reading);
        Assert.Equal("Reading", reading.LabelGroupName);
        Assert.Equal("N/A", reading.Text[0].Text);
        
        var units = resultFull.Matches!.First(m => m.LabelGroupName == "Units");
        Assert.NotNull(units);
        Assert.Equal("Units", units.LabelGroupName);
        Assert.Equal("N/A", units.Text[0].Text);
        
        var other = resultFull.Matches!.First(m => m.LabelGroupName == "Other");
        Assert.NotNull(other);
        Assert.Equal("Other", other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches!.First(m => m.LabelGroupName == "CertificatesOfRecords");
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal("CertificatesOfRecords", certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches!.First(m => m.LabelGroupName == "DateOfCertification");
        Assert.NotNull(dateOfCertificate);
        Assert.Equal("DateOfCertification", dateOfCertificate.LabelGroupName);
        Assert.Equal("10/06/2020", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches!.First(m => m.LabelGroupName == "Calibration");
        Assert.NotNull(calibration);
        Assert.Equal("Calibration", calibration.LabelGroupName);
        Assert.Equal("No", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches!.First(m => m.LabelGroupName == "Conformance");
        Assert.NotNull(conformance);
        Assert.Equal("Conformance", conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches!.First(m => m.LabelGroupName == "FlowVerification");
        Assert.NotNull(flowVerification);
        Assert.Equal("FlowVerification", flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches!.First(m => m.LabelGroupName == "MeterVerification");
        Assert.NotNull(meterVerification);
        Assert.Equal("MeterVerification", meterVerification.LabelGroupName);
        Assert.Equal("No", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches!.First(m => m.LabelGroupName == "WhereKept");
        Assert.NotNull(whereKept);
        Assert.Equal("WhereKept", whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches!.First(m => m.LabelGroupName == "FormSentTo");
        Assert.NotNull(formSentTo);
        Assert.Equal("FormSentTo", formSentTo.LabelGroupName);
        Assert.StartsWith("Av", formSentTo.Text[0].Text);
        Assert.EndsWith("ry", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches!.First(m => m.LabelGroupName == "Date");
        Assert.NotNull(date);
        Assert.Equal("Date", date.LabelGroupName);
        Assert.Equal("09/04/2025", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentTemplateVersion");
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal("DocumentTemplateVersion", documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);

        var documentHeader = resultFull.Matches!.First(m => m.LabelGroupName == "DocumentHeader");
        Assert.NotNull(documentHeader);
        Assert.Equal("DocumentHeader", documentHeader.LabelGroupName);
        Assert.Single(documentHeader.Text);
        Assert.Equal("51", documentHeader.Text[0].Text);
        
        var generalComments = resultFull.Matches!.First(m => m.LabelGroupName == "GeneralComments");
        Assert.NotNull(generalComments);
        Assert.Equal("GeneralComments", generalComments.LabelGroupName);
        Assert.Equal(10, generalComments.Text.Count);
        Assert.StartsWith("Licence", generalComments.Text[0].Text);
        Assert.EndsWith("license", generalComments.Text[0].Text);
        Assert.StartsWith("Action", generalComments.Text[9].Text);
        Assert.EndsWith("returns.", generalComments.Text[9].Text);
        
        var maintenance = resultFull.Matches!.First(m => m.LabelGroupName == "MaintenanceLine");
        Assert.NotNull(maintenance);
        Assert.Equal("MaintenanceLine", maintenance.LabelGroupName);
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
        
        var readingsTaken = resultFull.Matches!.First(m => m.LabelGroupName == "ReadingsTakenLine");
        Assert.NotNull(readingsTaken);
        Assert.Equal("ReadingsTakenLine", readingsTaken.LabelGroupName);
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
        
        var inspectionDate = resultFull.Matches!.First(m => m.LabelGroupName == "InspectionDate");
        Assert.NotNull(inspectionDate);
        Assert.Equal("InspectionDate", inspectionDate.LabelGroupName);
        Assert.Equal("06/12/2024", inspectionDate.Text[0].Text);
    }
    
    [Fact]
    public async Task WhenWR51_POCA_12_NewFormatWithImages_ThenGood()
    {
        // Arrange
        const string filename = "WR51__940020238G__dummy.pdf";

        // Act
        var (matchesResult, dmsFileData) = await GetMatchesAsync(filename);
        var resultFull = matchesResult;
        
        var converted = WrInspectionReportSchemaConverter.ToForm(matchesResult, dmsFileData);
        Assert.NotNull(converted);
        Assert.NotNull(converted.Metadata);
        Assert.Equal("2026_07_10_v1", converted.Metadata.DocumentTemplateVerison);
        Assert.Equal("WR51__940020238G__dummy.pdf", converted.Metadata.Filename);
        Assert.Equal(Guid.Parse("ba80b0b1-23ed-e9eb-afe5-2c45adf74f71"), converted.Metadata.FileId);
        Assert.Equal(false, converted.Metadata.IsScan);
        Assert.Equal(12, converted.Images.Count);
        Assert.Equal("WR51__940020238G__dummy/pdfpig-page2-image1.jpg", converted.Images[0]);
        Assert.Equal("WR51__940020238G__dummy/pdfpig-page2-image2.jpg", converted.Images[1]);
        Assert.Equal("WR51__940020238G__dummy/pdfpig-page8-image1.jpg", converted.Images[11]);
    }
}
