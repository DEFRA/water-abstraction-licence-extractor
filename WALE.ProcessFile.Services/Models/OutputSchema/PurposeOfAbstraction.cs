using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class PurposeOfAbstraction : Purpose
{
    public string? NaldId { get; set; }

    public string[] PointIds { get; set; } = [];
    
    public TimeCutoff? TimeCutoff { get; set; }
    
    public static PurposeOfAbstraction Template => new()
    {
        Description = string.Empty,
        Id = string.Empty,
        NaldId = null,
        PointIds = [
            "2.1"
        ],
        TimeCutoff = new TimeCutoff
        {
            CutoffType = CutoffType.Upto,
            Date = "31 March 2030"
        }
    };
}