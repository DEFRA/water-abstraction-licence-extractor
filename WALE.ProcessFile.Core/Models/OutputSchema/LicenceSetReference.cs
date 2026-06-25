using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class LicenceSetReference
{
    public string? LicenceSetId { get; init; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LicenceSetType LicenceSetType { get; init; }
}