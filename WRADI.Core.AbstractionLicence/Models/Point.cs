namespace WRADI.Core.AbstractionLicence.Models;

public class Point
{
    public string? DocumentId { get; set; }
    
    public string? NaldId { get; init; }
    
    public string? AltDocumentId { get; init; }
    
    public string? DocumentDescription { get; set; }
    
    public string? NaldDescription { get; set; }
    
    public bool? IsImplicit { get; set; }
}