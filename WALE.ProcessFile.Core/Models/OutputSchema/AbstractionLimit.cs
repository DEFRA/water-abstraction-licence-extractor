using WALE.ProcessFile.Models.Enums.OutputSchema;

namespace WALE.ProcessFile.Models.OutputSchema;

public class AbstractionLimit
{
    public LimitPeriodType PeriodType { get; init; }
    
    public double? Value { get; init; }
    
    public string? Units { get; init; }
    
    public Point[]? Points { get; set; }
    
    public Purpose[]? Purposes { get; set; }
    
    public bool? ImplicitLimit { get; set; }

    public AbstractionLimit Clone()
    {
        // TODO do this via source generator

        return new AbstractionLimit
        {
            PeriodType = PeriodType,
            Value = Value,
            Units = Units,
            Points = Points,
            Purposes = Purposes,
            ImplicitLimit = ImplicitLimit
        };
    }

    public static AbstractionLimit Template => new()
    {
        Value = 0,
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
        Units = string.Empty
    };
}