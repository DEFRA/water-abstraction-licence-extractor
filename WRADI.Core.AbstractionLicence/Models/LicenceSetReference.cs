using System.Text.Json.Serialization;
using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public class LicenceSetReference
{
    public string? LicenceSetId { get; init; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LicenceSetType LicenceSetType { get; init; }
}