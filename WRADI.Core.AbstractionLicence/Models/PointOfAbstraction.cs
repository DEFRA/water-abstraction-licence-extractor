using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public class PointOfAbstraction : Point
{
    public string[]? PurposeIds { get; init; }
    
    public string? Name { get; init; }
    
    public string? KnownAs { get; set; }
    
    public string? Near { get; set; }
    
    public List<NationalGridReference>? NationalGridReferences { get; set; } = [];
    
    public List<CartesianReference>? CartesianReferences { get; set; } = [];
    
    public TimeCutoff? TimeCutoff { get; set; }
    
    public ContainedInInformation[]? ContainedIn { get; set; }

    public static PointOfAbstraction Template => new()
    {
        Description1 = string.Empty,
        Id = string.Empty,
        // TODO known as, Near etc...
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