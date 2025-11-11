namespace WALE.ProcessFile.Models;

public class NaldDataPoint
{
    public string? PointName { get; set; }
    public int PointId { get; init; }
    public string? Ngr1 { get; set; }
    public string? Ngr1Cartesian { get; set; }
    
    public override string ToString()
    {
        return $"{PointId}{PointName}{Ngr1}{Ngr1Cartesian}";
    }
}