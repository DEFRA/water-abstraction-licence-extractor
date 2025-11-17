namespace WALE.ProcessFile.Models.OutputSchema;

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
            ImplicitLimit = abstractionLimit.ImplicitLimit,
            IsAverage = false,
            AveragePeriod = null
        };
    }

    public new static AggregateAbstractionLimit Template
    {
        get
        {
            var baseTemplate = AbstractionLimit.Template;
            var returnItem = FromAbstractionLimit(baseTemplate);
            
            return returnItem;
        }
    }
}