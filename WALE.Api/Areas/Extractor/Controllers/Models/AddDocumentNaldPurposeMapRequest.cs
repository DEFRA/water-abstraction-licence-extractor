using WRADI.Core.AbstractionLicence.Models;

namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class AddDocumentNaldPurposeMapRequest
{
    public string? documentDescription { get; set; }
    
    public NaldPurposeData? naldPurpose { get; set; }
    
    public string? matchType { get; set; }
}