namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class PeriodOfAbstraction : TimePeriod
{
    public string? Id { get; set; }
    
    public string? NaldId { get; set; }
    
    public string[] PointIds { get; set; } = [];
    
    public string[] PurposeIds { get; set; } = [];
}