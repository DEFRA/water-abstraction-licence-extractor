using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public class AbstractionLimitGroup : PeriodAndPointRestricted
{
    public string? DocumentIdentifier { get; set; }
    
    public TimePeriod? TimePeriod { get; set; }
    
    public TimeCutoff? TimeCutoff { get; set; }
    
    public List<AbstractionLimit> Limits { get; set; } = [];
    
    public ContainedInInformation[]? ContainedIn { get; set; }

    public AbstractionLimitGroup Clone()
    {
        var returnItem = new AbstractionLimitGroup();
        CloneProperties(this, returnItem);
        
        return returnItem;
    }
    
    public static void CloneProperties(
        AbstractionLimitGroup sourceGroup,
        AbstractionLimitGroup destinationGroup)
    {
        destinationGroup.DocumentIdentifier = sourceGroup.DocumentIdentifier;
        destinationGroup.TimePeriod = sourceGroup.TimePeriod;
        destinationGroup.TimeCutoff = sourceGroup.TimeCutoff;
        destinationGroup.Limits = sourceGroup.Limits.ToList();
        destinationGroup.ContainedIn = sourceGroup.ContainedIn?.ToArray();
        
        PeriodAndPointRestricted.CloneProperties(sourceGroup, destinationGroup);
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
        Limits = [AbstractionLimit.Template],
        ContainedIn = []
    };
}