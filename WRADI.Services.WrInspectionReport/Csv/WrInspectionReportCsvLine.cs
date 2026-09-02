namespace WRADI.DocumentType.WrInspectionReport.Csv;

public class WrInspectionReportCsvLine
{
    public string? Metadata__Filename { get; set; }
    public string? Metadata__DocumentTemplateVerison { get; set; }
    public string? Metadata__DocumentHeader { get; set; }
    public bool? Metadata__IsScan { get; set; }
    public string? Metadata__FormSentTo { get; set; }
    public string? Metadata__Date__Date { get; set; }
    public string? Metadata__Date__RawDate { get; set; }
    public string? LicenceNumber { get; set; }
    public string? InspectionClass { get; set; }
    public string? Address__NameAndAddress { get; set; }
    public string? Address__TelephoneNumber { get; set; }
    public string? Address__SiteAddress { get; set; }
    public string? MetWith__Name { get; set; }
    public string? MetWith__Position { get; set; }
    public string? InspectingOfficer { get; set; }
    public string? InspectionDate__DateTime { get; set; }
    public string? InspectionDate__Year { get; set; }
    public string? InspectionDate__RawDate { get; set; }
    public string? InspectionDate__RawTime { get; set; }
    public string? LicenceProvisions__SourceOfSupply { get; set; }
    public string? LicenceProvisions__Purposes { get; set; }
    public string? LicenceProvisions__PointOfAbstraction { get; set; }
    public string? LicenceProvisions__SpecialConditions { get; set; }
    public string? LicenceProvisions__MeansOfAbstraction { get; set; }
    public string? LicenceProvisions__Period { get; set; }
    public string? LicenceProvisions__Quantities { get; set; }
    public string? LicenceProvisions__MeansOfMeasurement { get; set; }
    public string? LicenceProvisions__Records { get; set; }
    public string? LicenceProvisions__ProvisionOfInformation { get; set; }
    public string? LicenceProvisions__Land { get; set; }
    public string? LicenceProvisions__ChargingFactors { get; set; }
    public string? LicenceProvisions__OtherProvisions { get; set; }
    public string? MeasurementDetails__MeterMake { get; set; }
    public string? MeasurementDetails__SerialNumber { get; set; }
    public string? MeasurementDetails__MeterAssetNumber { get; set; }
    public string? MeasurementDetails__Reading { get; set; }
    public string? MeasurementDetails__FlowRate { get; set; }
    public string? MeasurementDetails__Units { get; set; }
    public string? MeasurementDetails__Other { get; set; }
    public string? MeasurementDetails__CertificatesOrRecordsAvailableFor { get; set; }
    public string? MeasurementDetails__DateOfCertificateOrRecord__Date { get; set; }
    public string? MeasurementDetails__DateOfCertificateOrRecord__RawDate { get; set; }
    public string? MeasurementDetails__Calibration { get; set; }
    public string? MeasurementDetails__Conformance { get; set; }
    public string? MeasurementDetails__FlowVerification { get; set; }
    public string? MeasurementDetails__MeterVerification { get; set; }
    public string? MeasurementDetails__Verification { get; set; }
    public string? MeasurementDetails__SpotCheckResult { get; set; }
    public string? MeasurementDetails__Maintenance__Maintenance { get; set; }
    public string? MeasurementDetails__Maintenance__Frequency { get; set; }
    public string? MeasurementDetails__Maintenance__ByWhom { get; set; }
    public string? MeasurementDetails__ReadingsTaken__ReadingsTaken { get; set; }
    public string? MeasurementDetails__ReadingsTaken__Frequency { get; set; }
    public string? MeasurementDetails__ReadingsTaken__ByWhom { get; set; }
    public string? MeasurementDetails__WhereKept { get; set; }
    public string? GeneralComments { get; set; }
    public string? Images { get; set; }

    public static WrInspectionReportCsvLine FromForm(Models.WrInspectionReport form)
    {
        return new WrInspectionReportCsvLine
        {
            Metadata__Filename = form.Metadata.Filename,
            Metadata__DocumentTemplateVerison = form.Metadata.DocumentTemplateVerison,
            Metadata__DocumentHeader = form.Metadata.DocumentHeader,
            Metadata__IsScan = form.Metadata.IsScan,
            Metadata__FormSentTo = form.Metadata.FormSentTo,
            Metadata__Date__Date = form.Metadata.Date.Date?.ToString("dd/MM/yyyy"),
            Metadata__Date__RawDate = form.Metadata.Date.RawDate,
            LicenceNumber = form.LicenceNumber,
            InspectionClass = form.InspectionClass,
            Address__NameAndAddress = form.Address.NameAndAddress,
            Address__TelephoneNumber = form.Address.TelephoneNumber,
            Address__SiteAddress = form.Address.SiteAddress,
            MetWith__Name = form.MetWith.Name,
            MetWith__Position = form.MetWith.Position,
            InspectingOfficer = form.InspectingOfficer,
            InspectionDate__DateTime = form.InspectionDate.DateTime?.ToString("dd/MM/yyyy HH:mm:ss"),
            InspectionDate__Year = form.InspectionDate.DateTime?.Year.ToString(),
            InspectionDate__RawDate = form.InspectionDate.RawDate,
            InspectionDate__RawTime = form.InspectionDate.RawTime,
            LicenceProvisions__SourceOfSupply = form.LicenceProvisions.SourceOfSupply.ToString(),
            LicenceProvisions__Purposes = form.LicenceProvisions.Purposes.ToString(),
            LicenceProvisions__PointOfAbstraction = form.LicenceProvisions.PointOfAbstraction.ToString(),
            LicenceProvisions__SpecialConditions = form.LicenceProvisions.SpecialConditions.ToString(),
            LicenceProvisions__MeansOfAbstraction = form.LicenceProvisions.MeansOfAbstraction.ToString(),
            LicenceProvisions__Period = form.LicenceProvisions.Period.ToString(),
            LicenceProvisions__Quantities = form.LicenceProvisions.Quantities.ToString(),
            LicenceProvisions__MeansOfMeasurement = form.LicenceProvisions.MeansOfMeasurement.ToString(),
            LicenceProvisions__Records = form.LicenceProvisions.Records.ToString(),
            LicenceProvisions__ProvisionOfInformation = form.LicenceProvisions.ProvisionOfInformation.ToString(),
            LicenceProvisions__Land = form.LicenceProvisions.Land.ToString(),
            LicenceProvisions__ChargingFactors = form.LicenceProvisions.ChargingFactors.ToString(),
            LicenceProvisions__OtherProvisions = form.LicenceProvisions.OtherProvisions.ToString(),
            MeasurementDetails__MeterMake = form.MeasurementDetails.MeterMake,
            MeasurementDetails__SerialNumber = form.MeasurementDetails.SerialNumber,
            MeasurementDetails__MeterAssetNumber = form.MeasurementDetails.MeterAssetNumber,
            MeasurementDetails__Reading = form.MeasurementDetails.Reading,
            MeasurementDetails__FlowRate = form.MeasurementDetails.FlowRate,
            MeasurementDetails__Units = form.MeasurementDetails.Units,
            MeasurementDetails__Other = form.MeasurementDetails.Other,
            MeasurementDetails__CertificatesOrRecordsAvailableFor = form.MeasurementDetails.CertificatesOrRecordsAvailableFor,
            MeasurementDetails__DateOfCertificateOrRecord__Date =
                form.MeasurementDetails.DateOfCertificateOrRecord.Date?.ToString("dd/MM/yyyy"),
            MeasurementDetails__DateOfCertificateOrRecord__RawDate = form.MeasurementDetails.DateOfCertificateOrRecord.RawDate,
            MeasurementDetails__Calibration = form.MeasurementDetails.Calibration,
            MeasurementDetails__Conformance = form.MeasurementDetails.Conformance,
            MeasurementDetails__FlowVerification = form.MeasurementDetails.FlowVerification,
            MeasurementDetails__MeterVerification = form.MeasurementDetails.MeterVerification,
            MeasurementDetails__Verification = form.MeasurementDetails.Verification,
            MeasurementDetails__SpotCheckResult = form.MeasurementDetails.SpotCheckResult,
            MeasurementDetails__Maintenance__Maintenance = form.MeasurementDetails.Maintenance.Maintenance,
            MeasurementDetails__Maintenance__Frequency = form.MeasurementDetails.Maintenance.Frequency,
            MeasurementDetails__Maintenance__ByWhom = form.MeasurementDetails.Maintenance.ByWhom,
            MeasurementDetails__ReadingsTaken__ReadingsTaken = form.MeasurementDetails.ReadingsTaken.ReadingsTaken,
            MeasurementDetails__ReadingsTaken__Frequency = form.MeasurementDetails.ReadingsTaken.Frequency,
            MeasurementDetails__ReadingsTaken__ByWhom = form.MeasurementDetails.ReadingsTaken.ByWhom,
            MeasurementDetails__WhereKept = form.MeasurementDetails.WhereKept,
            GeneralComments = form.GeneralComments,
            Images = form.Images.Count > 0 ? string.Join("\n", form.Images) : null
        };
    }
}
