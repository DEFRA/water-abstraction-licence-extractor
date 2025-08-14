using WALE.ProcessFile.Services.Constants;

namespace WALE.ProcessFile.Services.Models;

public class DocumentLineColumn(string text, List<DocumentLineWord> words)
{
    public DocumentLineColumn(string text) : this(text, TextToWords(text)) { }
    
    public DocumentLineColumn() : this(string.Empty, []) { }

    public string Text { get; set; } = text;    
    
    public List<DocumentLineWord> Words { get; set; } = words;

    public DocumentLineColumn Clone()
    {
        return new DocumentLineColumn(Text, Words.ToList());
    }
    
    private static List<DocumentLineWord> TextToWords(string text)
    {
        return text
            .Split(' ')
            .Select(word =>
                new DocumentLineWord(word, null, PositionConstants.UnknownCoordinates))
            .ToList();
    }
}