using System.Text.Json.Serialization;
using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public record ContainedInInformation
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InformationSource Source { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InformationDirection? Direction { get; init; }
    
    public string? SectionName { get; init; }
    
    public string? LinkReason { get; init; }
    
    public int? LineNumber { get; init; }
    
    public int? PageNumber { get; init; }
}