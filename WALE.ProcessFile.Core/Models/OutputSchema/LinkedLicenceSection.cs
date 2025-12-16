namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class LinkedLicenceSection
{
    public string? SectionName { get; init; }
    
    public string? LinkReason { get; init; }
    
    public int LineNumber { get; init; }
    
    public int PageNumber { get; init; }
}