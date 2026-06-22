using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class AbstractionLimitGroup : PeriodAndPointRestricted
{
    public string? DocumentIdentifier { get; init; }
    
    public TimePeriod? TimePeriod { get; set; }
    
    public TimeCutoff? TimeCutoff { get; set; }
    
    public List<AbstractionLimit> Limits { get; init; } = [];
    
    public ContainedInInformation[]? ContainedIn { get; set; }
    
    public static AbstractionLimitGroup Template => new()
    {
        DocumentIdentifier = null,
        TimePeriod = new TimePeriod
        {
            StartDate = null,
            EndDate = null,
            Inclusive = true,
            PeriodType = AbstractionPeriodType.SetPeriod
        },
        TimeCutoff = new TimeCutoff
        {
            CutoffType = CutoffType.From,
            Date = null
        },
        Limits = [AbstractionLimit.Template],
        ContainedIn = []
    };
}