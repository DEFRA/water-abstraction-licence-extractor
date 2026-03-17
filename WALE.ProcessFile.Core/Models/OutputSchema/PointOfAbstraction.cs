using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class PointOfAbstraction : Point
{
    public int? NaldPointId { get; set; }
    public string? Name { get; set; }
    public string[]? PurposeIds { get; set; } = [];
    public List<NationalGridReference> NationalGridReferences { get; set; } = [];
    public List<CartesianReference> CartesianReferences { get; set; } = [];
    public TimeCutoff? TimeCutoff { get; set; }
    
    public static PointOfAbstraction Template => new()
    {
        Id = string.Empty,
        Description = string.Empty,
        Name = string.Empty,
        NaldPointId = 0,
        PurposeIds = [
            "4.1"
        ],
        NationalGridReferences = [
            new NationalGridReference
            {
                ReferenceIndex = 1,
                East = "71117",
                North = "17185",
                Sheet = "NZ"
            }
        ],
        CartesianReferences = [
            new CartesianReference
            {
                East = 471117,
                North = 517185,
                ReferenceIndex = 1
            }
        ],
        TimeCutoff = new TimeCutoff
        {
            CutoffType = CutoffType.Upto,
            Date = "31 March 2030"
        }
    };
}