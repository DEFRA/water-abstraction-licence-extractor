namespace WRADI.DocumentType.WrInspectionReport.Models;

// One meter's worth of measurement details - see WrInspectionReportMeasurementDetails.Meters.
// Every document gets at least one entry, even when only one meter is described (e.g. two
// boreholes each with their own make/serial/reading would be two entries; today's extraction
// only ever produces one). CalibrationCertificate/VerificationCertificate have no extraction
// wired up yet - previously unrepresentable anywhere in the schema (see UnmodeledFields in
// Wr51GroundTruthAccuracyTests.cs), included here for when that's built.
public class WrInspectionReportMeter
{
    public string? MeterName { get; set; }

    public string? MeterMake { get; set; }

    public string? SerialNumber { get; set; }

    public string? MeterAssetNumber { get; set; }

    public string? Reading { get; set; }

    public string? FlowRate { get; set; }

    public string? Units { get; set; }

    public string? CalibrationCertificate { get; set; }

    public string? VerificationCertificate { get; set; }
}
