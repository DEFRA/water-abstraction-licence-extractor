namespace WALE.ProcessFile.Core.Models;

public class NaldDataPoint
{
    public string? PointName { get; set; }
    
    public string? Category { get; set; }

    public long PointId { get; init; }
    
    public string? Ngr1 { get; set; }
    
    public string? Ngr2 { get; set; }
    
    public string? Ngr3 { get; set; }
    
    public string? Ngr4 { get; set; }
    
    public string? Ngr1Cartesian { get; set; }
    
    public string? Ngr2Cartesian { get; set; }
    
    public string? Ngr3Cartesian { get; set; }
    
    public string? Ngr4Cartesian { get; set; }
    
    public string? PrimaryType { get; set; }
    
    public string? SecondaryType { get; set; }

    public override string ToString()
    {
        return $"{PointId}{PointName}{Category}{PrimaryType}{SecondaryType}{Ngr1}{Ngr2}{Ngr3}{Ngr4}{Ngr1Cartesian}{Ngr2Cartesian}{Ngr3Cartesian}{Ngr4Cartesian}";
    }
}