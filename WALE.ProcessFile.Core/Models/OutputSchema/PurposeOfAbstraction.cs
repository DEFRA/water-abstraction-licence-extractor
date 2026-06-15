using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class PurposeOfAbstraction : Purpose
{
    public NaldPurposeData? NaldData { get; set; }
    
    public string[]? PointIds { get; set; }
    
    public TimeCutoff? TimeCutoff { get; set; }
    
    public static PurposeOfAbstraction Template => new()
    {
        Description = string.Empty,
        Id = string.Empty,
        NaldData = new NaldPurposeData
        {
            Id = "TODO"
        },
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