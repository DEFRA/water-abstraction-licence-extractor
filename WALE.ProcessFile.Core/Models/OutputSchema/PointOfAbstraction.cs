using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class PointOfAbstraction : Point
{
    public NaldPointData? NaldData { get; set; }

    public string[]? PurposeIds { get; init; }
    
    public string? Name { get; init; }
    
    public string? GridRef { get; init; }
    
    public TimeCutoff? TimeCutoff { get; set; }
    
    public ContainedInInformation[]? ContainedIn { get; set; }
    
    public static PointOfAbstraction Template => new()
    {
        Description = string.Empty,
        Id = string.Empty,
        NaldData = new NaldPointData
        {
            Id = "something"
        },
        PurposeIds = [
            "4.1"
        ],
        TimeCutoff = new TimeCutoff
        {
            CutoffType = CutoffType.Upto,
            Date = "31 March 2030"
        }
    };
}