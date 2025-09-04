namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class MeanOfAbstraction
{
    public double? Id { get; set; }
    
    public string? Description { get; set; }
    
    public AbstractionLimit? AbstractionLimit { get; set; }
}