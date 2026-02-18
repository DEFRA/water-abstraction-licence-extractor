using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Constants;

namespace WALE.ProcessFile.Core.Models;

public class DocumentLineColumn(string text, List<DocumentLineWord> words)
{
    public DocumentLineColumn(string text) : this(text, TextToWords(text)) { }
    
    public DocumentLineColumn() : this(string.Empty, []) { }

    [JsonIgnore]
    public string Text { get; set; } = text; // TODO remove eventually    
    
    public List<DocumentLineWord> Words { get; set; } = words;

    public DocumentLineColumn Clone()
    {
        return new DocumentLineColumn(Text, Words.ToList());
    }

    public DocumentLine AsDocumentLine(DocumentLine line)
    {
        return new DocumentLine
        {
            LineNumber = line.LineNumber,
            PageNumber = line.PageNumber,
            Columns = [this]
        };
    }
    
    private static List<DocumentLineWord> TextToWords(string text)
    {
        return text
            .Split(' ')
            .Select(word =>
                new DocumentLineWord(
                    word,
                    null,
                    PositionConstants.UnknownCoordinates,
                    null))
            .ToList();
    }
}