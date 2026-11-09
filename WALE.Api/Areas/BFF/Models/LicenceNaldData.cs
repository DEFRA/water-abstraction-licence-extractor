using System.Text.Json.Serialization;
using WRADI.Core.AbstractionLicence.Enums;

namespace WALE.Api.Areas.BFF.Models;

public class LicenceNaldData
{
    public string? LicenceNumber { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NaldLicenceStatus NaldStatus { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LicenceType LicenceType { get; set; }

    public int? RegionId { get; set; }
}
