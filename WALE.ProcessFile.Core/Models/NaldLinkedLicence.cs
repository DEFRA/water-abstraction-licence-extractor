using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Core.Models;

public record NaldLinkedLicence
{
    public required NaldLicence NaldLicence { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required NaldLinkedLicenceType LinkType { get; init; }
    
    public string? IncomingLicenceNumber { get; init; }
    
    public required string FromField { get; init; }
    
    public required string? FromFieldText { get; init; }
}