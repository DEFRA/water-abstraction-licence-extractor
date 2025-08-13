namespace WALE.ProcessFile.Services.Models;

public class DocumentLineColumn(string text, List<DocumentLineWord> words)
{
    public DocumentLineColumn() : this(string.Empty, []) { }

    public string Text { get; set; } = text;    
    
    public List<DocumentLineWord> Words { get; set; } = words;

    public DocumentLineColumn Clone()
    {
        return new DocumentLineColumn(Text, Words.ToList());
    }
    
    public DocumentLineColumn Clone(string text)
    {
        return new DocumentLineColumn(text, Words.ToList());
    }
}