namespace WALE.ProcessFile.Services.Models;

public class TextToMatch(string text)
{
    public string Text { get; } = text;

    public bool ColumnMustStartWith { get; init; }
    
    public bool ColumnMustHave2SequentialNumbers { get; set; }
    
    public bool RemoveWholeLine { get; init; }

    public int InstanceNumber { get; init; } = 1;
}