using System.Text.Json.Serialization;
using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public class LinkedLicence
{
    public string? LicenceNumber { get; set; }
    
    public string? RawScrapedLicenceNumber { get; set; }
    
    public string? DmsPermitNumber { get; set; }
    
    public string? DmsPath { get; set; }
    
    public Guid? DmsFileId { get; set; }
    
    public string? Filename { get; set; }
    
    public LicenceVersion LicenceVersion { get; set; } = new();
    
    public Condition? Condition { get; set; }
    
    public bool? IsBecauseOfAggregate { get; set; }
    
    public ContainedInInformation[]? ContainedIn { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NaldLicenceStatus NaldStatus { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LicenceType LicenceType { get; set; }
    
    public int? RegionId { get; set; }

    public LinkedLicence Clone()
    {
        // TOOD, source gen
        return new LinkedLicence
        {
            LicenceNumber = LicenceNumber,
            RawScrapedLicenceNumber = RawScrapedLicenceNumber,
            DmsPermitNumber = DmsPermitNumber,
            DmsPath = DmsPath,
            DmsFileId = DmsFileId,
            Filename = Filename,
            LicenceVersion = LicenceVersion,
            Condition = Condition,
            IsBecauseOfAggregate = IsBecauseOfAggregate,
            ContainedIn = ContainedIn?.Select(ci => ci.Clone()).ToArray(),
            NaldStatus = NaldStatus,
            LicenceType = LicenceType,
            RegionId = RegionId
        };
    }
}