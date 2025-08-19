namespace WALE.ProcessFile.Services.Models;

public class TextToMatch(string text)
{
    public string Text { get; } = text;

    public bool ColumnMustStartWith { get; init; }
    
    public bool IfMultiplePreferLast { get; init; }
    
    public bool IfMultiplePreferLongest { get; init; }
    
    public bool ColumnMustHave2SequentialNumbers { get; set; }
    
    public bool RemoveWholeLine { get; init; }

    public int InstanceNumber { get; init; } = 1;

    public TextToMatch Clone(string text2)
    {
        return new TextToMatch(text2)
        {
            ColumnMustStartWith = ColumnMustStartWith,
            IfMultiplePreferLast = IfMultiplePreferLast,
            IfMultiplePreferLongest = IfMultiplePreferLongest,
            ColumnMustHave2SequentialNumbers = ColumnMustHave2SequentialNumbers,
            RemoveWholeLine = RemoveWholeLine,
            InstanceNumber = InstanceNumber
        };
    }
}