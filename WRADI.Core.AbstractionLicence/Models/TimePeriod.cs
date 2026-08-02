using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public class TimePeriod
{
    public AbstractionPeriodType? PeriodType { get; set; }
    
    public string? StartDate { get; set; }
    
    public string? EndDate { get; set; }
    
    public bool? Inclusive { get; set; }
}