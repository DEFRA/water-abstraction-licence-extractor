namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class AbstractionLimit
{
    public string? Name { get; set; }
    
    public double? Value { get; set; }
    
    public string? Units { get; set; }
    
    public Point? Point { get; set; }
    
    public Purpose? Purpose { get; set; }    
}