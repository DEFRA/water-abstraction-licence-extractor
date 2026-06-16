using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public record LinkedLicenceSection
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LinkedLicenceSource Source { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LinkedLicenceDirection Direction { get; init; } = LinkedLicenceDirection.Outgoing;
    
    public string? SectionName { get; init; }
    
    public string? LinkReason { get; init; }
    
    public bool? IsBecauseOfAggregate { get; init; }
    
    public int? LineNumber { get; init; }
    
    public int? PageNumber { get; init; }
}