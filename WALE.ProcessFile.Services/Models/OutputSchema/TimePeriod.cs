using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class TimePeriod
{
    public PeriodType? PeriodType { get; set; }
    
    public string? StartDate { get; set; }
    
    public string? EndDate { get; set; }    
}