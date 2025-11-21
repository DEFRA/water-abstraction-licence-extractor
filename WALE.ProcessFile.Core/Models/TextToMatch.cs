namespace WALE.ProcessFile.Core.Models;

public class TextToMatch(string text)
{
    public string Text { get; } = text;

    public bool LineMustStartWith { get; init; }
    
    public bool ColumnMustStartWith { get; init; }
    
    public bool IfMultiplePreferLast { get; init; }
    
    public bool IfMultiplePreferLongest { get; init; }
    
    public bool ColumnMustHave2SequentialNumbers { get; set; }
    
    public bool RemoveWholeLine { get; init; }

    public int InstanceNumber { get; init; } = 1;
    
    public bool IsRegularExpression { get; init; }

    public TextToMatch Clone(string textToSet)
    {
        return new TextToMatch(textToSet)
        {
            LineMustStartWith = LineMustStartWith,
            ColumnMustStartWith = ColumnMustStartWith,
            IfMultiplePreferLast = IfMultiplePreferLast,
            IfMultiplePreferLongest = IfMultiplePreferLongest,
            ColumnMustHave2SequentialNumbers = ColumnMustHave2SequentialNumbers,
            RemoveWholeLine = RemoveWholeLine,
            InstanceNumber = InstanceNumber,
            IsRegularExpression = IsRegularExpression
        };
    }
}