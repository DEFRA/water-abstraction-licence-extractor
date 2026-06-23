using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class AbstractionLimit : PeriodAndPointRestricted
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LimitPeriodType PeriodType { get; init; }
    
    public double? Value { get; init; }
    
    public string? Units { get; init; }
    
    public bool? ImplicitLimit { get; set; }
    
    public bool IsAverage { get; set; }
    
    public int? AveragePeriod { get; set; }
    
    public string? ValueAdditionalText { get; set; }

    public AbstractionLimit Clone()
    {
        // TODO do this via source generator

        return new AbstractionLimit
        {
            PeriodType = PeriodType,
            Value = Value,
            ValueAdditionalText = ValueAdditionalText,
            Units = Units,
            Points = Points,
            Purposes = Purposes,
            ImplicitLimit = ImplicitLimit,
            IsAverage = IsAverage,
            AveragePeriod = AveragePeriod
        };
    }

    public static AbstractionLimit Template => new()
    {
        Value = 0,
        ValueAdditionalText = "Something",
        ImplicitLimit = false,
        PeriodType = LimitPeriodType.NotApplicable,
        Points =
        [
            new()
            {
                Description = string.Empty,
                Id = string.Empty
            }
        ],
        Purposes =
        [
            new()
            {
                Description = string.Empty,
                Id = string.Empty
            }
        ],
        Units = string.Empty,
        IsAverage = true,
        AveragePeriod = 5
    };
}