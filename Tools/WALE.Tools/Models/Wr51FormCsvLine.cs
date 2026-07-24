namespace WALE.Tools.Models;

public class Wr51CsvLine
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
    
    public string? MeasurementDetails__Reading { get; set; }
    
    public string? MeasurementDetails__Units { get; set; }
    
    public string? MeasurementDetails__Other { get; set; }
    
    public string? MeasurementDetails__CertificatesOrRecordsAvailableFor { get; set; }

    public string? MeasurementDetails__DateOfCertificateOrRecord__Date { get; set; }
    
    public string? MeasurementDetails__DateOfCertificateOrRecord__RawDate { get; set; }
    
    public string? MeasurementDetails__Calibration { get; set; }
    
    public string? MeasurementDetails__Conformance { get; set; }
    
    public string? MeasurementDetails__FlowVerification { get; set; }
    
    public string? MeasurementDetails__MeterVerification { get; set; }

    public string? MeasurementDetails__Maintenance__Maintenance { get; set; }
    
    public string? MeasurementDetails__Maintenance__Frequency { get; set; }
    
    public string? MeasurementDetails__Maintenance__ByWhom { get; set; }
    
    public string? MeasurementDetails__ReadingsTaken__ReadingsTaken { get; set; }
    
    public string? MeasurementDetails__ReadingsTaken__Frequency { get; set; }
    
    public string? MeasurementDetails__ReadingsTaken__ByWhom { get; set; }
    
    public string? MeasurementDetails__WhereKept { get; set; }
    
    public string? GeneralComments { get; set; }
    
    public string? Images { get; set; }
}