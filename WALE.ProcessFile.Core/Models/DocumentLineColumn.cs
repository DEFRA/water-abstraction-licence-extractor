using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Constants;

namespace WALE.ProcessFile.Core.Models;

public class DocumentLineColumn(List<DocumentLineWord> words)
{
    public DocumentLineColumn(string text) : this(TextToWords(text)) { }
    
    public DocumentLineColumn() : this([]) { }

    [JsonIgnore]
    public string Text
    {
        get
        {
            return Words.Count == 0 ?
                string.Empty
                : string.Join(' ', Words.Select(column => column.Text));
        }
    }
    
    public List<DocumentLineWord> Words { get; set; } = words;

    public DocumentLineColumn Clone()
    {
        return new DocumentLineColumn(Words.ToList());
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