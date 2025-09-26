using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class AbstractionLimitGroup
{
    public TimePeriod? TimePeriod { get; set; }
    
    public List<AggregateAbstractionLimit> Limits { get; init; } = [];
    
    public static AbstractionLimitGroup Template => new()
    {
        TimePeriod = new TimePeriod
        {
            StartDate = null,
            EndDate = null,
            Inclusive = true,
            PeriodType = AbstractionPeriodType.SetPeriod
        },
        Limits = [AggregateAbstractionLimit.Template],
    };
}