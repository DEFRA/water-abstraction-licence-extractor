namespace WALE.ProcessFile.Services.Models;

public class TextToMatch(string text)
{
    public string Text { get; set; } = text;

    public bool ColumnMustStartWith { get; set; }
    
    public bool RemoveWholeLine { get; set; }

    public int InstanceNumber { get; set; } = 1;
}