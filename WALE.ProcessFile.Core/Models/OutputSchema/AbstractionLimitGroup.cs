using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class AbstractionLimitGroup : PeriodAndPointRestricted
{
    public string? DocumentIdentifier { get; init; }
    
    public TimePeriod? TimePeriod { get; set; }
    
    public List<AbstractionLimit> Limits { get; init; } = [];
    
    public static AbstractionLimitGroup Template => new()
    {
        TimePeriod = new TimePeriod
        {
            StartDate = null,
            EndDate = null,
            Inclusive = true,
            PeriodType = AbstractionPeriodType.SetPeriod
        },
        Limits = [AbstractionLimit.Template],
    };
}