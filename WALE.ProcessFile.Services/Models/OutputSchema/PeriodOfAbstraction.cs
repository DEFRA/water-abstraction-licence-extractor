namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class PeriodOfAbstraction : TimePeriod
{
    public double? Id { get; set; }
    
    public string? Description { get; set; }
    
    public string? NaldId { get; set; }
    
    public string[] PointIds { get; set; } = [];
    
    public string[] PurposeIds { get; set; } = [];
}