namespace WRADI.Core.AbstractionLicence.Models;

public class NaldPurposeData
{
    public string? Id { get; set; }
    
    public string? Code { get; init; }
    
    public string? UseCode { get; init; }
    
    public string? UseDescription { get; init; }

    public string? PrimaryCategoryDescription { get; set; }
    
    public string? SecondaryCategoryDescription { get; set; }
    
    public string? QuantityIdentifier { get; set; }
}