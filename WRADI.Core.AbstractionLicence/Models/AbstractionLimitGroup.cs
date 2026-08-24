using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public class AbstractionLimitGroup : PeriodAndPointRestricted
{
    public string? DocumentIdentifier { get; init; }
    
    public TimePeriod? TimePeriod { get; set; }
    
    public TimeCutoff? TimeCutoff { get; set; }
    
    public List<AbstractionLimit> Limits { get; set; } = [];
    
    public ContainedInInformation[]? ContainedIn { get; set; }

    public AbstractionLimitGroup Clone()
    {
        return new AbstractionLimitGroup
        {
            DocumentIdentifier = DocumentIdentifier,
            TimePeriod = TimePeriod,
            TimeCutoff = TimeCutoff,
            Limits = Limits.ToList(),
            Points = Points?.ToArray(),
            Purposes = Purposes?.ToArray(),
            ContainedIn = ContainedIn?.ToArray()
        };
    }
    
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
        Limits = [AbstractionLimit.Template]
    };
}