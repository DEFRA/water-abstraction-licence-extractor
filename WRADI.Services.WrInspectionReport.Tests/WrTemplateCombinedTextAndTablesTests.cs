using FakeItEasy;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tabula;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Constants;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.Services.WrInspectionReport.Tests.Config;
using WRADI.Services.WrInspectionReport.Tests.Helper;

namespace WRADI.Services.WrInspectionReport.Tests;

public class WrTemplateCombinedTextAndTablesTests
{
    static WrTemplateCombinedTextAndTablesTests()
    {
        //..
    }
    
    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    private static readonly IMessageQueueService MessageQueueService = A.Fake<IMessageQueueService>(); 
    
    private readonly IPdfDataExtractorService _pdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>(),
        CacheService,
        OutputService,
        DocumentService,
        DocnetAlternativeDocumentService,
        MessageQueueService);
    
    private async Task<(MatchesResult, DmsFileData)> GetMatchesAsync(string fileName, int regionCode)
    {
        var dmsFileData = new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(fileName) };
        
        var matchesResult = (await _pdfDataExtractor.GetMatchesAsync(
            fileName,
            dmsFileData,
            await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder),
            [fileName],
            0)).Item!;

        return (matchesResult, dmsFileData);
    }
    
    private static async Task<LookupConfiguration> LookupConfigurationAsync(int regionCode, string pdfFolder)
    {
        return new LookupConfiguration(
            WrInspectionReportLabelConfiguration.GetLabels(),
            await CompanyNameHelper.GetFirstNamesCsvFromFileAsync(),
            new LocalFileService(pdfFolder),
            CacheService,
            OutputService,
            null!,
            new TabulaTableExtractorService(),
            new DmsLookupService(),
            regionCode,
            DateTime.Now,
            useLockExclusivity: false,
            lineHeight: 6,
            minimumRowsForDigital: 30,
            useAnchoredLineGrouping: true);
    }
    
    [Fact]
    public async Task WhenA_B()
    {
        // Arrange
        const string filename = "WR51__121014G8__dummy.pdf";

        // Act
        var resultFullX = await GetMatchesAsync(filename, regionCode: -1);
        var resultFull = resultFullX.Item1;
        
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(42, resultList.Count);
        
        var sourceOfSupply = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.SourceOfSupply);
        Assert.NotNull(sourceOfSupply);
        Assert.Equal(WrInspectionReportFieldNames.SourceOfSupply, sourceOfSupply.LabelGroupName);
        Assert.Equal("In", sourceOfSupply.Text[0].Text);
        
        var pointOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.PointOfAbstraction);
        Assert.NotNull(pointOfAbstraction);
        Assert.Equal(WrInspectionReportFieldNames.PointOfAbstraction, pointOfAbstraction.LabelGroupName);
        Assert.Equal("In", pointOfAbstraction.Text[0].Text);
        
        var meansOfAbstraction = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.MeansOfAbstraction);
        Assert.NotNull(meansOfAbstraction);
        Assert.Equal(WrInspectionReportFieldNames.MeansOfAbstraction, meansOfAbstraction.LabelGroupName);
        Assert.Equal("In", meansOfAbstraction.Text[0].Text);
        
        var purposes = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.Purposes);
        Assert.NotNull(purposes);
        Assert.Equal(WrInspectionReportFieldNames.Purposes, purposes.LabelGroupName);
        Assert.Equal("Not", purposes.Text[0].Text);
        
        var period = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.Period);
        Assert.NotNull(period);
        Assert.Equal(WrInspectionReportFieldNames.Period, period.LabelGroupName);
        Assert.Equal("In", period.Text[0].Text);
        
        var quantities = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.Quantities);
        Assert.NotNull(quantities);
        Assert.Equal(WrInspectionReportFieldNames.Quantities, quantities.LabelGroupName);
        Assert.Equal("In", quantities.Text[0].Text);
        
        var meansOfMeasurement = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.MeansOfMeasurement);
        Assert.NotNull(meansOfMeasurement);
        Assert.Equal(WrInspectionReportFieldNames.MeansOfMeasurement, meansOfMeasurement.LabelGroupName);
        Assert.Equal("In", meansOfMeasurement.Text[0].Text);
        
        var records = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.Records);
        Assert.NotNull(records);
        Assert.Equal(WrInspectionReportFieldNames.Records, records.LabelGroupName);
        Assert.Equal("Not", records.Text[0].Text);
        
        var provisionOfInformation = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.ProvisionOfInformation);
        Assert.NotNull(provisionOfInformation);
        Assert.Equal(WrInspectionReportFieldNames.ProvisionOfInformation, provisionOfInformation.LabelGroupName);
        Assert.Equal("Not", provisionOfInformation.Text[0].Text);
        
        var specialConditions = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.SpecialConditions);
        Assert.NotNull(specialConditions);
        Assert.Equal(WrInspectionReportFieldNames.SpecialConditions, specialConditions.LabelGroupName);
        Assert.Equal("N/A", specialConditions.Text[0].Text);
        
        var land = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.Land);
        Assert.NotNull(land);
        Assert.Equal(WrInspectionReportFieldNames.Land, land.LabelGroupName);
        Assert.Equal("In", land.Text[0].Text);
        
        var chargingFactors = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.ChargingFactors);
        Assert.NotNull(chargingFactors);
        Assert.Equal(WrInspectionReportFieldNames.ChargingFactors, chargingFactors.LabelGroupName);
        Assert.Equal("Not", chargingFactors.Text[0].Text);
        
        var otherProvisions = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.OtherProvisions);
        Assert.NotNull(otherProvisions);
        Assert.Equal(WrInspectionReportFieldNames.OtherProvisions, otherProvisions.LabelGroupName);
        Assert.Equal("N/A", otherProvisions.Text[0].Text);
        
        var licenceNumber = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.LicenceNumber);
        Assert.NotNull(licenceNumber);
        Assert.Equal(WrInspectionReportFieldNames.LicenceNumber, licenceNumber.LabelGroupName);
        Assert.Equal("12/101/4/G/8", licenceNumber.Text[0].Text);
        
        var metWith = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.MetWith);
        Assert.NotNull(metWith);
        Assert.Equal(WrInspectionReportFieldNames.MetWith, metWith.LabelGroupName);
        Assert.StartsWith("Ja", metWith.Text[0].Text);
        Assert.EndsWith("or", metWith.Text[0].Text);
        
        var inspectingOfficer = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.InspectingOfficer);
        Assert.NotNull(inspectingOfficer);
        Assert.Equal(WrInspectionReportFieldNames.InspectingOfficer, inspectingOfficer.LabelGroupName);
        Assert.StartsWith("Ar", inspectingOfficer.Text[0].Text);
        Assert.EndsWith("an", inspectingOfficer.Text[0].Text);
        
        var siteAddress = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.SiteAddress);
        Assert.NotNull(siteAddress);
        Assert.Equal(WrInspectionReportFieldNames.SiteAddress, siteAddress.LabelGroupName);
        Assert.StartsWith("Ly", siteAddress.Text[0].Text);
        Assert.EndsWith("JQ", siteAddress.Text[0].Text);
        
        var inspectionClass = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.InspectionClass);
        Assert.NotNull(inspectionClass);
        Assert.Equal(WrInspectionReportFieldNames.InspectionClass, inspectionClass.LabelGroupName);
        Assert.Equal("Less Critical", inspectionClass.Text[0].Text);
        
        var telephoneNumber = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.TelephoneNumber);
        Assert.NotNull(telephoneNumber);
        Assert.Equal(WrInspectionReportFieldNames.TelephoneNumber, telephoneNumber.LabelGroupName);
        Assert.Single(telephoneNumber.Text!);
        Assert.StartsWith("07", telephoneNumber.Text[0].Text);
        Assert.EndsWith("86", telephoneNumber.Text[0].Text);
        
        var position = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.Position);
        Assert.NotNull(position);
        Assert.Equal(WrInspectionReportFieldNames.Position, position.LabelGroupName);
        Assert.Equal("Flow Measurement Coordinator", position.Text[0].Text);
        
        var time = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.Time);
        Assert.NotNull(time);
        Assert.Equal(WrInspectionReportFieldNames.Time, time.LabelGroupName);
        Assert.Equal("11:20", time.Text[0].Text);
        
        var nameAndAddress = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.NameAndAddress);
        Assert.NotNull(nameAndAddress);
        Assert.Equal(WrInspectionReportFieldNames.NameAndAddress, nameAndAddress.LabelGroupName);
        Assert.Equal(2, nameAndAddress.Text.Count);
        Assert.StartsWith("Sout", nameAndAddress.Text[0].Text);
        Assert.EndsWith("ing,", nameAndAddress.Text[0].Text);
        Assert.Equal("BN13 3NX", nameAndAddress.Text[1].Text);
        
        var meterMake = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.MeterMake);
        Assert.NotNull(meterMake);
        Assert.Equal(WrInspectionReportFieldNames.MeterMake, meterMake.LabelGroupName);
        Assert.Equal("Abstraction Flowmeter", meterMake.Text[0].Text);
        
        var serialNumber = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.SerialNumber);
        Assert.NotNull(serialNumber);
        Assert.Equal(WrInspectionReportFieldNames.SerialNumber, serialNumber.LabelGroupName);
        Assert.StartsWith("V/", serialNumber.Text[0].Text);
        Assert.EndsWith("2", serialNumber.Text[0].Text);
        
        var reading = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.Reading);
        Assert.NotNull(reading);
        Assert.Equal(WrInspectionReportFieldNames.Reading, reading.LabelGroupName);
        Assert.Equal("4,714,612", reading.Text[0].Text);
        
        var units = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.Units);
        Assert.NotNull(units);
        Assert.Equal(WrInspectionReportFieldNames.Units, units.LabelGroupName);
        Assert.Equal("m3", units.Text[0].Text);
        
        var other = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.Other);
        Assert.NotNull(other);
        Assert.Equal(WrInspectionReportFieldNames.Other, other.LabelGroupName);
        Assert.Equal("N/A", other.Text[0].Text);
        
        var certificatesOfRecord = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.CertificatesOfRecords);
        Assert.NotNull(certificatesOfRecord);
        Assert.Equal(WrInspectionReportFieldNames.CertificatesOfRecords, certificatesOfRecord.LabelGroupName);
        Assert.Equal("N/A", certificatesOfRecord.Text[0].Text);
        
        var dateOfCertificate = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.DateOfCertification);
        Assert.NotNull(dateOfCertificate);
        Assert.Equal(WrInspectionReportFieldNames.DateOfCertification, dateOfCertificate.LabelGroupName);
        Assert.Equal("30/06/2021", dateOfCertificate.Text[0].Text);
        
        var calibration = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.Calibration);
        Assert.NotNull(calibration);
        Assert.Equal(WrInspectionReportFieldNames.Calibration, calibration.LabelGroupName);
        Assert.Equal("Yes", calibration.Text[0].Text);
        
        var conformance = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.Conformance);
        Assert.NotNull(conformance);
        Assert.Equal(WrInspectionReportFieldNames.Conformance, conformance.LabelGroupName);
        Assert.Equal("No", conformance.Text[0].Text);
        
        var flowVerification = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.FlowVerification);
        Assert.NotNull(flowVerification);
        Assert.Equal(WrInspectionReportFieldNames.FlowVerification, flowVerification.LabelGroupName);
        Assert.Equal("Yes", flowVerification.Text[0].Text);
        
        var meterVerification = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.MeterVerification);
        Assert.NotNull(meterVerification);
        Assert.Equal(WrInspectionReportFieldNames.MeterVerification, meterVerification.LabelGroupName);
        Assert.Equal("Yes", meterVerification.Text[0].Text);
        
        var whereKept = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.WhereKept);
        Assert.NotNull(whereKept);
        Assert.Equal(WrInspectionReportFieldNames.WhereKept, whereKept.LabelGroupName);
        Assert.Equal("On Site", whereKept.Text[0].Text);
        
        var formSentTo = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.FormSentTo);
        Assert.NotNull(formSentTo);
        Assert.Equal(WrInspectionReportFieldNames.FormSentTo, formSentTo.LabelGroupName);
        Assert.StartsWith("Ja", formSentTo.Text[0].Text);
        Assert.EndsWith("or", formSentTo.Text[0].Text);
        
        var date = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.Date);
        Assert.NotNull(date);
        Assert.Equal(WrInspectionReportFieldNames.Date, date.LabelGroupName);
        Assert.Equal("12/04/2024", date.Text[0].Text);
        
        var documentTemplateVersion = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.DocumentTemplateVersion);
        Assert.NotNull(documentTemplateVersion);
        Assert.Equal(WrInspectionReportFieldNames.DocumentTemplateVersion, documentTemplateVersion.LabelGroupName);
        Assert.Equal("2026_07_10_v1", documentTemplateVersion.Text[0].Text);
        
        var documentHeader = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.DocumentHeader);
        Assert.NotNull(documentHeader);
        Assert.Equal(WrInspectionReportFieldNames.DocumentHeader, documentHeader.LabelGroupName);
        Assert.Single(documentHeader.Text);
        Assert.Equal("51", documentHeader.Text[0].Text);
        
        var generalComments = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.GeneralComments);
        Assert.NotNull(generalComments);
        Assert.Equal(WrInspectionReportFieldNames.GeneralComments, generalComments.LabelGroupName);
        Assert.Equal(5, generalComments.Text.Count);
        Assert.StartsWith("Licence 12/", generalComments.Text[0].Text);
        Assert.EndsWith("single borehole.", generalComments.Text[0].Text);
        Assert.StartsWith("No RTW", generalComments.Text[4].Text);
        Assert.EndsWith("inspection.", generalComments.Text[4].Text);
        
        var maintenance = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.MaintenanceLine);
        Assert.NotNull(maintenance);
        Assert.Equal(WrInspectionReportFieldNames.MaintenanceLine, maintenance.LabelGroupName);
        Assert.Equal("Maintenance: Yes Frequency: Daily By whom: JP", maintenance.Text[0].Text);
        Assert.Equal(3, maintenance.SubResults.Count);

        var maintenanceSubLabel = maintenance.SubResults[0];
        Assert.NotNull(maintenanceSubLabel);
        Assert.Equal(WrInspectionReportFieldNames.MaintenanceLineMaintenance, maintenanceSubLabel.MatchedLabelName);
        Assert.Equal("Yes", maintenanceSubLabel.Text[0].Text);
        
        var frequencySubLabel = maintenance.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal(WrInspectionReportFieldNames.MaintenanceLineFrequency, frequencySubLabel.MatchedLabelName);
        Assert.Equal("Daily", frequencySubLabel.Text[0].Text);
        
        var byWhomSubLabel = maintenance.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal(WrInspectionReportFieldNames.MaintenanceLineByWhom, byWhomSubLabel.MatchedLabelName);
        Assert.Equal("JP", byWhomSubLabel.Text[0].Text);
        
        var readingsTaken = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.ReadingsTakenLine);
        Assert.NotNull(readingsTaken);
        Assert.Equal(WrInspectionReportFieldNames.ReadingsTakenLine, readingsTaken.LabelGroupName);
        Assert.Equal("Readings taken: Yes Frequency: Fortnightly By whom: MP", readingsTaken.Text[0].Text);
        Assert.Equal(3, readingsTaken.SubResults.Count);

        var readingsTakenSubLabel = readingsTaken.SubResults[0];
        Assert.NotNull(readingsTakenSubLabel);
        Assert.Equal(WrInspectionReportFieldNames.ReadingsTakenLineReadingsTaken, readingsTakenSubLabel.MatchedLabelName);
        Assert.Equal("Yes", readingsTakenSubLabel.Text[0].Text);
        
        frequencySubLabel = readingsTaken.SubResults[1];
        Assert.NotNull(frequencySubLabel);
        Assert.Equal(WrInspectionReportFieldNames.ReadingsTakenLineFrequency, frequencySubLabel.MatchedLabelName);
        Assert.Equal("Fortnightly", frequencySubLabel.Text[0].Text);
        
        byWhomSubLabel = readingsTaken.SubResults[2];
        Assert.NotNull(byWhomSubLabel);
        Assert.Equal(WrInspectionReportFieldNames.ReadingsTakenLineByWhom, byWhomSubLabel.MatchedLabelName);
        Assert.Equal("MP", byWhomSubLabel.Text[0].Text);
        
        var inspectionDate = resultFull.Matches!.First(m => m.LabelGroupName == WrInspectionReportFieldNames.InspectionDate);
        Assert.NotNull(inspectionDate);
        Assert.Single(inspectionDate.Text!);
        Assert.Equal(WrInspectionReportFieldNames.InspectionDate, inspectionDate.LabelGroupName);
        Assert.Equal("04/03/2024", inspectionDate.Text[0].Text);

        var converted = WrInspectionReportSchemaConverter.ToForm(resultFull, resultFullX.Item2);
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
        Assert.Equal("Abstraction Flowmeter", converted.MeasurementDetails.Meters![0].MeterMake);
        Assert.StartsWith("V", converted.MeasurementDetails.Meters![0].SerialNumber);
        Assert.EndsWith("2", converted.MeasurementDetails.Meters![0].SerialNumber);
        Assert.Equal("4,714,612", converted.MeasurementDetails.Meters![0].Reading);
        Assert.Equal("m3", converted.MeasurementDetails.Meters![0].Units);
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
}