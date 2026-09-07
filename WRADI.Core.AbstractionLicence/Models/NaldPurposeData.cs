namespace WRADI.Core.AbstractionLicence.Models;

public class NaldPurposeData
{
    public string? Id { get; set; }

    public string? PrimaryCategoryCode { get; init; }
    
    public string? SecondaryCategoryCode { get; init; }
    
    public int UseCode { get; init; }
    
    public string? PrimaryCategoryDescription { get; set; }
    
    public string? SecondaryCategoryDescription { get; set; }
    
    public string? UseDescription { get; init; }
    
    public string? QuantityIdentifier { get; set; }
    
    public string? CombinedCode { get; init; }
}