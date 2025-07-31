namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class AggregateAbstractionLimit : AbstractionLimit
{
    public bool IsAverage { get; set; }
    
    public int? AveragePeriod { get; set; }    
}