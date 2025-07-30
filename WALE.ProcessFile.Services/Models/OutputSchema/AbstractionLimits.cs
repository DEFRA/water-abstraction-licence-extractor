namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class AbstractionLimits
{
    public AbstractionLimit[] Individual { get; set; } = [];
    
    public Aggregate[] Aggregates { get; set; } = [];
}