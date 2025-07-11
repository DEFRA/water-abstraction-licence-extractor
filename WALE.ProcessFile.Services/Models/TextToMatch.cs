namespace WALE.ProcessFile.Services.Models;

public class TextToMatch(string text)
{
    public string Text { get; set; } = text;

    public bool LineMustStartWith { get; set; }
    
    public bool RemoveWholeLine { get; set; }
}