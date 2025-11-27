using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class TimePeriod
{
    public AbstractionPeriodType? PeriodType { get; set; }
    
    public string? StartDate { get; set; }
    
    public string? EndDate { get; set; }
    
    public bool? Inclusive { get; set; }
}