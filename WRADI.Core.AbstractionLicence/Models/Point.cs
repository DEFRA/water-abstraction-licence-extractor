namespace WRADI.Core.AbstractionLicence.Models;

public class Point
{
    public string? Id { get; set; }
    
    public string? AltId { get; init; }
    
    public string? Description { get; set; }
    
    public bool? IsImplicit { get; set; }
}