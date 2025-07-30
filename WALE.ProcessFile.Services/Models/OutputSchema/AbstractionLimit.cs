using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class AbstractionLimit
{
    public LimitPeriodType PeriodType { get; set; }
    
    public double? Value { get; set; }
    
    public string? Units { get; set; }
    
    public Point? Point { get; set; }
    
    public Purpose? Purpose { get; set; }    
}