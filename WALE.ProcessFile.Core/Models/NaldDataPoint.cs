namespace WALE.ProcessFile.Core.Models;

public class NaldDataPoint
{
    public int PointId { get; init; }
    public short RegionCode { get; init; }
    public string? PointName { get; init; }
    public string? Category { get; init; }
    
    public string? AaptAptpCode { get; init; }
    public string? AaptAptsCode { get; init; }
    public string? AapcCode { get; init; }
    
    public List<NationalGridReference> NationalGridReferences { get; init; } = [];
    public List<CartesianReference> CartesianReferences { get; init; } = [];
    
    [Obsolete("Use NationalGridReferences")]
    public string? Ngr1 { get; init; }
    [Obsolete("Use NationalGridReferences")]
    public string? Ngr2 { get; init; }
    [Obsolete("Use NationalGridReferences")]
    public string? Ngr3 { get; init; }
    [Obsolete("Use NationalGridReferences")]
    public string? Ngr4 { get; init; }
    
    [Obsolete("Use CartesianReferences")]
    public string? Ngr1Cartesian { get; init; }
    [Obsolete("Use CartesianReferences")]
    public string? Ngr2Cartesian { get; init; }
    [Obsolete("Use CartesianReferences")]
    public string? Ngr3Cartesian { get; init; }
    [Obsolete("Use CartesianReferences")]
    public string? Ngr4Cartesian { get; init; }
    
    public string? PrimaryType { get; init; }
    public string? SecondaryType { get; init; }
    public List<int> PurposeIds { get; init; } = [];

    public override string ToString()
    {
        return $"{PointId}{RegionCode}";
    }
}