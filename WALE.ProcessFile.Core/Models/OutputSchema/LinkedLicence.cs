using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class LinkedLicence
{
    public string? LicenceNumber { get; set; }
    
    public string? RawScrapedLicenceNumber { get; set; }
    
    public string? PermitNumber { get; set; }
    
    public string? Filename { get; set; }
    
    public string? DmsPath { get; set; }
    
    public Condition? Condition { get; set; }
    
    public LinkedLicenceSection[]? ContainedIn { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NaldLicenceStatus NaldStatus { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LicenceType LicenceType { get; set; }
}