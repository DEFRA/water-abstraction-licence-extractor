namespace WALE.Tools.Models;

public class TemplateFinderInput
{
    public string? PermitNumber { get; set; }
    public Guid FileId { get; set; }
    public string? FileName { get; set; }
    public long FileSize { get; set; }
}