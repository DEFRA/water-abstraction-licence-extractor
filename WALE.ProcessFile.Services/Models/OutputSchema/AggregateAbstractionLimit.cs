namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class AggregateAbstractionLimit : AbstractionLimit
{
    public bool IsAverage { get; set; }
    
    public int? AveragePeriod { get; set; }

    public static AggregateAbstractionLimit FromAbstractionLimit(AbstractionLimit abstractionLimit)
    {
        // TODO ideally do in a different way

        return new AggregateAbstractionLimit
        {
            PeriodType = abstractionLimit.PeriodType,
            Value = abstractionLimit.Value,
            Units = abstractionLimit.Units,
            Points = abstractionLimit.Points,
            Purposes = abstractionLimit.Purposes,
            ImplicitLimit = abstractionLimit.ImplicitLimit
        };
    }
}