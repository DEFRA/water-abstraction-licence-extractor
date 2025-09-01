using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

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
}