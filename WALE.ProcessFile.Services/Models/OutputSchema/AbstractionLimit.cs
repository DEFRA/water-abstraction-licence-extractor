using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class AbstractionLimit
{
    public LimitPeriodType PeriodType { get; init; }
    
    public double? Value { get; init; }
    
    public string? Units { get; init; }
    
    public Point? Point { get; set; }
    
    public Purpose? Purpose { get; set; }    
}