using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Core.Models;

public record NaldLinkedLicence
{
    public required NaldLicence NaldLicence { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required NaldLinkedLicenceType LinkType { get; init; }
}