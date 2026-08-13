namespace WRADI.Core.AbstractionLicence.Models;

public class NaldPointData
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public List<NationalGridReference> NationalGridReferences { get; set; } = [];
    public List<CartesianReference> CartesianReferences { get; set; } = [];
    public List<int> NaldPurposeIds { get; set; } = [];
}