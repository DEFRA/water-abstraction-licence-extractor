using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Services.Models;

public class LineAndWords
{
    public string? Text { get; set; }
    public List<DocumentLineWord?>? Words { get; set; }        
}