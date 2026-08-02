using System.Text.Json.Serialization;
using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public class TimeCutoff
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CutoffType? CutoffType { get; set; }

    public string? Date { get; set; }
}