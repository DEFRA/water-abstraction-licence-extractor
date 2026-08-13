using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public class PointOfAbstraction : Point
{
    public NaldPointData? NaldData { get; set; }

    public string[]? PurposeIds { get; init; }
    
    public string? Name { get; init; }
    
    public List<NationalGridReference>? NationalGridReferences { get; set; } = [];
    
    public List<CartesianReference>? CartesianReferences { get; set; } = [];
    
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