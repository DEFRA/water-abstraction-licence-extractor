using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class LinkedLicence
{
    public string? LicenceNumber { get; set; }
    
    public string? RawScrapedLicenceNumber { get; set; }
    
    public string? DmsPermitNumber { get; set; }
    
    public string? DmsPath { get; set; }
    
    public Guid? DmsFileId { get; set; }
    
    public string? Filename { get; set; }
    
    public LicenceVersion LicenceVersion { get; init; } = new();
    
    public Condition? Condition { get; set; }
    
    public ContainedInInformation[]? ContainedIn { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NaldLicenceStatus NaldStatus { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LicenceType LicenceType { get; set; }
    
    public int? RegionId { get; set; }
}