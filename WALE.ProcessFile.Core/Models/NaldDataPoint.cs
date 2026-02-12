namespace WALE.ProcessFile.Core.Models;

public class NaldDataPoint
{
    public int PointId { get; init; }
    public string? PointName { get; init; }
    public string? Category { get; init; }
    public string? Ngr1 { get; init; }
    public string? Ngr2 { get; init; }
    public string? Ngr3 { get; init; }
    public string? Ngr4 { get; init; }
    public string? Ngr1Cartesian { get; init; }
    public string? Ngr2Cartesian { get; init; }
    public string? Ngr3Cartesian { get; init; }
    public string? Ngr4Cartesian { get; init; }
    public string? PrimaryType { get; init; }
    public string? SecondaryType { get; init; }
    public List<int> PurposeIds { get; init; } = [];

    public override string ToString()
    {
        return $"{PointId}{PointName}{Category}{PrimaryType}{SecondaryType}{Ngr1}{Ngr2}{Ngr3}{Ngr4}{Ngr1Cartesian}{Ngr2Cartesian}{Ngr3Cartesian}{Ngr4Cartesian}";
    }
}