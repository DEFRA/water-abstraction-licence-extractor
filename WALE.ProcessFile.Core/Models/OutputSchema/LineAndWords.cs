namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class LineAndWords
{
    public string? Text { get; set; }
    public List<DocumentLineWord?>? Words { get; set; }        
}