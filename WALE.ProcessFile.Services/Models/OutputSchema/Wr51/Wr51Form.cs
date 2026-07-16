using System.Text.Json.Serialization;
using WALE.ProcessFile.Services.Enums.Wr51;

namespace WALE.ProcessFile.Services.Models.OutputSchema.Wr51;

public class Wr51Form
{
    public Wr51FormMetadata Metadata { get; set; } = new();
    
    public string? LicenceNumber { get; set; }
    
    public string? InspectionClass { get; set; }
    
    public string? NameAndAddress { get; set; }
    
    public string? TelephoneNumber { get; set; }
    
    public string? SiteAddress { get; set; }
    
    public string? MetWith { get; set; }
    
    public string? MetWithsPosition { get; set; }
    
    public string? InspectingOfficer { get; set; }
    
    public string? InspectionDate { get; set; }
    
    public string? InspectionTime { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InOrderStatus SourceOfSupply { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InOrderStatus Purposes { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InOrderStatus PointOfAbstraction { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InOrderStatus SpecialConditions { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InOrderStatus MeansOfAbstraction { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InOrderStatus Period { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InOrderStatus Quantities { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InOrderStatus MeansOfMeasurement { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InOrderStatus Records { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InOrderStatus ProvisionOfInformation { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InOrderStatus Land { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InOrderStatus ChargingFactors { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InOrderStatus OtherProvisions { get; set; }
    
    public string? MeterMake { get; set; }
    
    public string? SerialNumber { get; set; }
    
    public string? Reading { get; set; }
    
    public string? Units { get; set; }
    
    public string? Other { get; set; }
    
    public string? CertificatesOrRecordsAvailableFor { get; set; }
    
    public string? DateOfCertificateOrRecord { get; set; }
    
    public string? Calibration { get; set; }
    
    public string? Conformance { get; set; }
    
    public string? FlowVerification { get; set; }
    
    public string? MeterVerification { get; set; }

    public Wr51FormMaintenance Maintenance { get; set; } = new();
    
    public Wr51FormReadingsTaken ReadingsTaken { get; set; } = new();
    
    public string? WhereKept { get; set; }
    
    public string? GeneralComments { get; set; }
    
    public string? FormSentTo { get; set; }
    
    public string? Date { get; set; }
}