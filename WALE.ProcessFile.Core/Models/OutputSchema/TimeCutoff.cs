using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class TimeCutoff
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CutoffType? CutoffType { get; set; }

    public string? Date { get; set; }
}