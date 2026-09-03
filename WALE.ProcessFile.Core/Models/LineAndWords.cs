namespace WALE.ProcessFile.Core.Models;

public class LineAndWords
{
    public string? Text
    {
        get
        {
            if (Words == null || Words.Count == 0)
            {
                return null;
            }
            
            return string.Join(" ", Words.Select(w => w!.Text));
        }
    }
    
    public List<DocumentLineWord?>? Words { get; set; }        
}