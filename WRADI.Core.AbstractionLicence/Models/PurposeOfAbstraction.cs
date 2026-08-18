using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public class PurposeOfAbstraction : Purpose
{
    public string[]? PointIds { get; set; }
    
    public TimeCutoff? TimeCutoff { get; set; }
    
    public ContainedInInformation[]? ContainedIn { get; set; }
    
    public static PurposeOfAbstraction Template => new()
    {
        DocumentDescription = string.Empty,
        DocumentId = string.Empty,
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