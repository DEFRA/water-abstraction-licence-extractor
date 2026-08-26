namespace WRADI.Core.AbstractionLicence.Models;

public class Purpose
{
    public string? Id { get; init; }
    
    public string? Description { get; set; }
    
    public string[]? NaldIds { get; set; }
    
    public string? NaldDescription { get; set; }
    
    public bool? IsImplicit { get; set; }
}