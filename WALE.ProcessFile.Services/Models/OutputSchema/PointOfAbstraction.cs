using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class PointOfAbstraction : Point
{
    public string? NaldId { get; set; }

    public string[]? PurposeIds { get; init; } = [];
    
    public TimeCutoff? TimeCutoff { get; set; }
    
    public static PointOfAbstraction Template => new()
    {
        Description = string.Empty,
        Id = string.Empty,
        NaldId = null,
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