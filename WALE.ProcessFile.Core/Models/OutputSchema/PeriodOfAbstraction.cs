using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class PeriodOfAbstraction : TimePeriod
{
    public string? Id { get; set; }
    
    public string? Description { get; set; }
    
    public string[]? PointIds { get; set; } = [];
    
    public string[]? PurposeIds { get; set; } = [];
    
    public TimeCutoff? TimeCutoff { get; set; }
    
    public static PeriodOfAbstraction Template => new()
    {
        Description = "All year.",
        EndDate = string.Empty,
        Id = "6",
        Inclusive = false,
        PeriodType = AbstractionPeriodType.PerYear,
        PointIds =
        [
            string.Empty
        ],
        PurposeIds =
        [
            string.Empty
        ],
        StartDate = string.Empty,
        TimeCutoff = new TimeCutoff
        {
            CutoffType = CutoffType.Upto,
            Date = "31 March 2030"
        }
    };
}