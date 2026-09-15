namespace WALE.Tools._2ndHalf.Models;

public class WqFormCsvLine
{
    public string? FileName { get; set; }
    
    public bool ContainsAnyOfTheLines { get; set; }
    
    public string? LineText { get; set; }    
    
    public string? WholeSectionText { get; set; }
}