namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class NaldPointData
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public List<NaldNationalGridReference> NationalGridReferences { get; set; } = [];
    public List<NaldCartesianReference> CartesianReferences { get; set; } = [];
    public List<int> NaldPurposeIds { get; set; } = [];
}