namespace WRADI.DocumentType.WrInspectionReport.Models.Csv;

/// <summary>
/// One row per meter, with the document's identity repeated on each.
///
/// WrInspectionReportCsvLine holds one row per document and so shows only the first meter,
/// dropping every meter beyond it. There is no upper limit on meters, so numbered columns cannot
/// carry them either.
/// </summary>
public class WrInspectionReportMeterCsvLine
{
    public string? Metadata__Filename { get; set; }
    public string? Metadata__Template { get; set; }
    public string? LicenceNumber { get; set; }
    public string? LicenceNumberCleaned { get; set; }
    public string? InspectionDate__DateTime { get; set; }

    /// <summary>1-based, so a reader can see at a glance whether a document's meters are all here.</summary>
    public int MeterNumber { get; set; }
    public int MeterCount { get; set; }

    public string? MeterName { get; set; }
    public string? MeterMake { get; set; }
    public string? SerialNumber { get; set; }
    public string? MeterAssetNumber { get; set; }
    public string? Reading { get; set; }
    public string? FlowRate { get; set; }
    public string? Units { get; set; }
    public string? CalibrationCertificate { get; set; }
    public string? VerificationCertificate { get; set; }
    public string? MaintenanceRecords { get; set; }

    /// <summary>
    /// A form from ToForm always yields at least one row: BuildMeters floors its count at one, so
    /// a document with no meter found still appears, with the meter columns blank. The null guard
    /// below is for a form built some other way, such as one deserialised without meters.
    /// </summary>
    public static IEnumerable<WrInspectionReportMeterCsvLine> FromForm(WrInspectionReport form)
    {
        var meters = form.MeasurementDetails.Meters ?? [];

        return meters.Select((meter, index) => new WrInspectionReportMeterCsvLine
        {
            Metadata__Filename = form.Metadata.Filename,
            Metadata__Template = form.Metadata.Template.ToString(),
            LicenceNumber = form.LicenceNumber,
            LicenceNumberCleaned = form.LicenceNumberCleaned.Count > 0
                ? string.Join("\n", form.LicenceNumberCleaned)
                : null,
            InspectionDate__DateTime = form.InspectionDate.DateTime?.ToString("dd/MM/yyyy HH:mm:ss"),
            MeterNumber = index + 1,
            MeterCount = meters.Count,
            MeterName = meter.MeterName,
            MeterMake = meter.MeterMake,
            SerialNumber = meter.SerialNumber,
            MeterAssetNumber = meter.MeterAssetNumber,
            Reading = meter.Reading,
            FlowRate = meter.FlowRate,
            Units = meter.Units,
            CalibrationCertificate = meter.CalibrationCertificate,
            VerificationCertificate = meter.VerificationCertificate,
            MaintenanceRecords = meter.MaintenanceRecords
        });
    }
}
