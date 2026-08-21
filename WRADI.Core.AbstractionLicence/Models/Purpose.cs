namespace WRADI.Core.AbstractionLicence.Models;

public class Purpose
{
    public string? DocumentId { get; init; }
    
    public string[]? NaldIds { get; set; }
    
    public string? DocumentDescription { get; set; }
    
    public string? NaldDescription { get; set; }
    
    public bool? IsImplicit { get; set; }
}