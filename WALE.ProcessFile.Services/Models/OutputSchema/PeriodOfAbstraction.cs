namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class PeriodOfAbstraction : TimePeriod
{
    public string[] PointNumbers { get; set; } = [];
    
    public string[] PurposeNumbers { get; set; } = [];
}