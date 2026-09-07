namespace WRADI.Core.AbstractionLicence.Models;

public class Purpose
{
    public string? Id { get; init; }
    
    public string? Description { get; set; }
    
    public string[]? NaldIds { get; set; }

    public string? NaldLevel1Description { get; set; }
    
    public string? NaldLevel2Description { get; set; }
    
    public string? NaldLevel3Description { get; set; }
    
    public bool? IsImplicit { get; set; }
}