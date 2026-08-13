using WALE.ProcessFile.Core.Models;

namespace WRADI.Core.AbstractionLicence.Models;

public class NaldDataPoint
{
    public int PointId { get; init; }
    public short RegionCode { get; init; }
    public string? PointName { get; init; }
    public string? AaptAptpCode { get; init; }
    public string? AaptAptsCode { get; init; }
    public string? AapcCode { get; init; }
    public List<WALE.ProcessFile.Core.Models.NationalGridReference> NationalGridReferences { get; init; } = [];
    public List<WALE.ProcessFile.Core.Models.CartesianReference> CartesianReferences { get; init; } = [];
    public List<int> PurposeIds { get; init; } = [];

    public override string ToString()
    {
        return $"{PointId}{RegionCode}";
    }
}