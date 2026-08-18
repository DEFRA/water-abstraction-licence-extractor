namespace WRADI.Core.AbstractionLicence.Models;

public class Point
{
    public string? Id { get; set; }
    
    public string? AltId { get; init; }
    
    public string? Description1 { get; set; }
    
    public string? Description2 { get; set; }
    
    public bool? IsImplicit { get; set; }
}