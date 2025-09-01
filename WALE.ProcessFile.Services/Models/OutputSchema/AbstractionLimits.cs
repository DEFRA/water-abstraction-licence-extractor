namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class AbstractionLimits
{
    public AbstractionLimit[] Individual { get; init; } = [];
    
    public Aggregate[] Aggregates { get; init; } = [];
}