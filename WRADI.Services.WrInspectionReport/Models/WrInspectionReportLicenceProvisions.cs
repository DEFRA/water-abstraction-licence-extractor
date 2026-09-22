using System.Text.Json.Serialization;
using WRADI.DocumentType.WrInspectionReport.Enums;

namespace WRADI.DocumentType.WrInspectionReport.Models;

public class WrInspectionReportLicenceProvisions
{
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
}