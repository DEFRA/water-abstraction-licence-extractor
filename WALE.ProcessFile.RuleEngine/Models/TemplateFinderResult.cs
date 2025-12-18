namespace WALE.ProcessFile.RuleEngine.Models;

public class TemplateFinderResult
{
    public string? FileName { get; set; } = string.Empty;
    
    public string? Header { get; set; } = string.Empty;
    
    public int NumberOfPages { get; set; }
    
    public string TemplateType { get; set; }
    
    public string? Template { get; set; } = string.Empty;
}
