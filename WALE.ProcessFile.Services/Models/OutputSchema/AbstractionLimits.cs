namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class AbstractionLimits
{
    public AbstractionLimitGroup[] Individual { get; init; } = [];
    
    public Aggregate[] Aggregates { get; init; } = [];

    public static AbstractionLimits Template = new()
    {
        Aggregates = [Aggregate.Template],
        Individual = [AbstractionLimitGroup.Template]
    };
}