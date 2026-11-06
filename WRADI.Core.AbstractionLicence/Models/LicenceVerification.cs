using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public class LicenceVerification
{
    public string? Section { get; init; }

    public string? Item { get; init; }

    public string? VerificationType { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VerifiedBy VerifiedBy { get; init; }

    public JsonNode? ScrapedValue { get; init; }

    public JsonNode? AppliedValue { get; init; }

    public string? Notes { get; init; }
}
