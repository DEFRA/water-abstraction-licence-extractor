using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public class ContainedInInformation
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InformationSource Source { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InformationDirection? Direction { get; init; }
    
    public string? SectionName { get; init; }
    
    public string? LinkReason { get; init; }
    
    // Will only be set for Nald licences
    public string? AcinCode { get; set; }
    
    // Will only be set for Nald licences
    public List<NaldLicenceNumberHistoryOutput>? History { get; set; }
    
    public Dictionary<string, string?>? SourceFields { get; set; }
    
    public int? LineNumber { get; init; }
    
    public int? PageNumber { get; init; }

    public ContainedInInformation Clone()
    {
        return new ContainedInInformation
        {
            Source = Source,
            Direction = Direction,
            SectionName = SectionName,
            LinkReason = LinkReason,
            AcinCode = AcinCode,
            History = History,
            SourceFields = SourceFields,
            LineNumber = LineNumber,
            PageNumber = PageNumber
        };
    }
}